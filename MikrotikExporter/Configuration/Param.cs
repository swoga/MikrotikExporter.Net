using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
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
        /// </summary>
        [Required]
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

        private static readonly Regex regexTimepan = new Regex(@"(?:(\d+)(w))?(?:(\d+)(d))?(?:(\d+)(h))?(?:(\d+)(m))?(?:(\d+)(s))?", RegexOptions.Compiled);

        /// <summary>
        /// Tries to get the parameter from the <paramref name="tikSentence"/>, if omitted uses the default value and parses it to double.
        /// Returns <c>false</c> if parsing fails
        /// </summary>
        /// <param name="tikSentence"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal bool TryGetValue(Log log, ITikReSentence tikSentence, out double value)
        {
            log.Debug2($"try to get '{Name}'");

            if (!tikSentence.TryGetResponseField(Name, out string word))
            {
                log.Debug2($"'{Name}' not found in response, use default");
                word = Default;
            }

            if (word != null)
            {
                log.Debug2($"parse '{Name}' as {ParamType}");

                switch (ParamType)
                {
                    case ParamType.Int:
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
                            log.Error($"failed to parse Timespan '{word}' from '{Name}'");
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
                            log.Error($"failed to parse DateTime '{word}' from '{Name}'");
                        }
                        break;
                    case ParamType.Enum:
                        if (EnumValues.TryGetValue(word, out value)) {
                            return true;
                        }
                        break;
                }
            }

            log.Debug2($"failed to get a value for '{Name}'");
            value = 0;
            return false;
        }
    }
}
