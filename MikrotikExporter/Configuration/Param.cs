using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using tik4net;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "<Pending>")]
    public enum ParamType
    {
        /// <summary>
        /// Only for labels
        /// </summary>
        String,
        /// <summary>
        /// For all types of numeric values (Integers, Doubles)
        /// </summary>
        Int,
        Bool,
        /// <summary>
        /// e.g. 20d5h16m39s
        /// </summary>
        Timespan,
        /// <summary>
        /// e.g. may/03/2020 17:41:00
        /// </summary>
        DateTime,
        /// <summary>
        /// For string to value mapping (e.g. psu-state: ok, fail)
        /// </summary>
        Enum
    }

    public enum DateTimeType
    {
        ToNow,
        FromNow
    }

    public class Param
    {
        /// <summary>
        /// Name of the parameter in the MikroTik API
        /// If empty, default value is used
        /// </summary>
        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        [Required]
        [YamlMember(Alias = "param_type")]
        public ParamType ParamType { get; set; }

        /// <summary>
        /// Only relevant for <c>ParamType.Bool</c>
        /// </summary>
        [YamlMember(Alias = "negate")]
        public bool Negate { get; set; }

        /// <summary>
        /// Only relevant for <c>ParamType.DateTime</c>
        /// </summary>
        [YamlMember(Alias = "datetime_type")]
        public DateTimeType? DateTimeType { get; set; }

        /// <summary>
        /// Default value if omitted in the MikroTik API
        /// </summary>
        [YamlMember(Alias = "default")]
        public string Default { get; set; }

        /// <summary>
        /// Only relevant for <c>ParamType.Enum</c>
        /// Maps strings to a value
        /// </summary>
        [YamlMember(Alias="enum_values")]
        public Dictionary<string, double> EnumValues {get; set; }

        /// <summary>
        /// Only relevant for <c>ParamType.Enum</c>
        /// Maps strings to a value
        /// </summary>
        [YamlMember(Alias = "enum_values_re")]
        public Dictionary<Regex, double> EnumValuesRegex { get; set; }

        /// <summary>
        /// Only relevant for <c>ParamType.Enum</c>
        /// Fallback value if string is not found in mapping
        /// </summary>
        [YamlMember(Alias ="enum_fallback")]
        public double? EnumFallback { get; set; }

        private static readonly Regex regexTimepan = new Regex(@"(?:(\d+)(w))?(?:(\d+)(d))?(?:(\d+)(h))?(?:(\d+)(m))?(?:(\d+)(s))?", RegexOptions.Compiled);
        private static readonly Regex regexAZ = new Regex(@"[a-zA-Z]", RegexOptions.Compiled);

        /// <summary>
        /// Tries to get the parameter from the <paramref name="tikSentence"/>, if omitted uses the default value and parses it to double.
        /// Returns <c>false</c> if parsing fails
        /// </summary>
        /// <param name="tikSentence"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal bool TryGetValue(Log log, ITikReSentence tikSentence, out double value)
        {
            string word = null;

            log.Debug2("try to get value");

            if (Name == null)
            {
                log.Debug2("static parameter, use default");
                word = Default;
            }
            else if (!tikSentence.TryGetResponseField(Name, out word))
            {
                log.Debug2($"'{Name}' not found in response, use default");
                word = Default;
            }

            if (word != null)
            {
                log.Debug2($"parse as {ParamType}");

                switch (ParamType)
                {
                    case ParamType.Int:
                        // remove potential units
                        word = regexAZ.Replace(word, "");
                        value = double.Parse(word, CultureInfo.InvariantCulture);
                        return true;
                    case ParamType.Bool:
                        var @bool = string.Equals(word, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(word, "yes", StringComparison.OrdinalIgnoreCase);
                        value = (@bool ^ Negate) ? 1 : 0;
                        return true;
                    case ParamType.Timespan:
                        Match match = regexTimepan.Match(word);
                        if (match.Success)
                        {
                            value = 0;
                            var enumerator = match.Groups.Values.Where(group => group.Length > 0).GetEnumerator();

                            // skip first group which contains entire expression
                            enumerator.MoveNext();

                            while (enumerator.MoveNext())
                            {
                                double groupValue = double.Parse(enumerator.Current.Value, CultureInfo.InvariantCulture);
                                enumerator.MoveNext();
                                string groupSuffix = enumerator.Current.Value;

                                value += groupSuffix switch
                                {
                                    "w" => groupValue * 604800,
                                    "d" => groupValue * 86400,
                                    "h" => groupValue * 3600,
                                    "m" => groupValue * 60,
                                    "s" => groupValue,
                                    _ => throw new NotImplementedException()
                                };
                            }
                            return true;
                        }
                        else
                        {
                            log.Error($"failed to parse Timespan '{word}'");
                        }
                        break;
                    case ParamType.DateTime:
                        if (DateTime.TryParseExact(word, "MMM/dd/yyyy HH:mm:ss", new CultureInfo("en-US"), DateTimeStyles.None, out var dateTime))
                        {
                            var now = DateTime.Now;
                            var timeSpan = DateTimeType.Value switch
                            {
                                Configuration.DateTimeType.FromNow => now - dateTime,
                                Configuration.DateTimeType.ToNow => dateTime - now,
                                _ => throw new NotImplementedException($"unknown DateTimeType: {DateTimeType}"),
                            };
                            value = timeSpan.TotalSeconds;
                            return true;
                        }
                        else
                        {
                            log.Error($"failed to parse DateTime '{word}'");
                        }
                        break;
                    case ParamType.Enum:
                        if (EnumValues != null && EnumValues.TryGetValue(word, out value))
                        {
                            log.Debug2($"'{value}' found in enum mapping");

                            return true;
                        }
                        else
                        {
                            var matchedValue = EnumValuesRegex?.Where(kvp => kvp.Key.IsMatch(word))?.Select(kvp => new { kvp.Value })?.FirstOrDefault();
                            if (matchedValue != null)
                            {
                                value = matchedValue.Value;
                                log.Debug2($"'{value}' found in enum regex mapping");
                                return true;
                            }
                            else if (EnumFallback.HasValue)
                            {
                                value = EnumFallback.Value;
                                log.Debug1($"'{value}' not found in enum mapping, use fallback value");
                                return true;
                            }
                        }
                        break;
                }
            }

            log.Debug2("failed to get value");
            value = 0;
            return false;
        }
    }
}
