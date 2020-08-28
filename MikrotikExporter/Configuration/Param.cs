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
        public string Name { get; protected set; }

        [YamlMember(Alias = "param_type")]
        public ParamType ParamType { get; protected set; } = ParamType.Int;

        /// <summary>
        /// Only relevant for <c>ParamType.Bool</c>
        /// </summary>
        [YamlMember(Alias = "negate")]
        public bool Negate { get; protected set; }

        /// <summary>
        /// Only relevant for <c>ParamType.DateTime</c>
        /// </summary>
        [YamlMember(Alias = "datetime_type")]
        public DateTimeType? DateTimeType { get; protected set; }

        /// <summary>
        /// Default value if omitted in the MikroTik API
        /// </summary>
        [YamlMember(Alias = "default")]
        public string Default { get; protected set; }

        /// <summary>
        /// If set, always used instead of value returned by API
        /// </summary>
        [YamlMember(Alias = "value")]
        public string Value { get; protected set; }

        public bool IsTrue { get; } = true;
        [Compare(nameof(IsTrue), ErrorMessage = "either name or value must be set")]
        public bool HasNameOrValue
        {
            get
            {
                return Name != null || Value != null;
            }
        }

        [YamlMember(Alias="remap_values")]
        public Dictionary<string, string> RemapValues {get; protected set; }

        [YamlMember(Alias = "remap_values_re")]
        public List<Tuple<Regex, string>> RemapValuesRegex { get; protected set; }

        private static readonly Regex regexTimepan = new Regex(@"(?:(\d+)(w))?(?:(\d+)(d))?(?:(\d+)(h))?(?:(\d+)(m))?(?:(\d+)(s))?", RegexOptions.Compiled);
        private static readonly Regex regexAZ = new Regex(@"[a-zA-Z]", RegexOptions.Compiled);

        internal static string Substitute(string value, Dictionary<string, string> variables)
        {
            if (value == null)
            {
                return null;
            }

            foreach (var variable in variables)
            {
                value = value.Replace($"{{{variable.Key}}}", variable.Value, true, CultureInfo.InvariantCulture);
            }

            return value;
        }

        private static bool TryParseDouble(string word, out double value)
        {
            if (word == null)
            {
                value = 0;
                return false;
            }

            // remove potential units
            word = regexAZ.Replace(word, "");
            return double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        internal bool PreprocessValue(Log log, ITikReSentence tikSentence, Dictionary<string, string> variables, out string word)
        {
            log.Debug2("try to get value");

            if (Value != null)
            {
                log.Debug2("static parameter");
                word = Substitute(Value, variables);
            }
            else if (!tikSentence.TryGetResponseField(Name, out word))
            {
                log.Debug1($"field not found in API response");
                return false;
            }

            if (RemapValues != null && RemapValues.TryGetValue(word, out word))
            {
                if (word == null)
                {
                    log.Debug2("remapped to null");
                    return false;
                }
                log.Debug2($"remapped to '{word}'");
            }
            else
            {
                var tmpWord = word;
                var firstMatch = RemapValuesRegex?.Where(kvp => kvp.Item1.IsMatch(tmpWord))?.FirstOrDefault();
                if (firstMatch != null)
                {
                    if (firstMatch.Item2 == null)
                    {
                        log.Debug2("regex remapped to null");
                        return false;
                    }
                    word = firstMatch.Item1.Replace(word, firstMatch.Item2);
                    log.Debug2($"regex remapped to '{word}'");
                }
            }

            return true;
        }

        internal bool TryGetValue(Log log, ITikReSentence tikSentence, Dictionary<string, string> variables, out double value)
        {
            if (PreprocessValue(log, tikSentence, variables, out var word))
            {
                log.Debug2($"parse as {ParamType}");

                switch (ParamType)
                {
                    case ParamType.Int:
                        if (!TryParseDouble(word, out value))
                        {
                            log.Error($"failed to parse value '{word}' to double");
                            return false;
                        }
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
                            value = 0;
                            return false;
                        }
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
                            value = 0;
                            return false;
                        }
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                if (Default == null)
                {
                    log.Debug2($"no default value set");
                    value = 0;
                    return false;
                }

                word = Substitute(Default, variables);
                if (!TryParseDouble(word, out value))
                {
                    log.Error($"failed to parse default '{word}' to double");
                    return false;
                }

                return true;
            }
        }
    }
}
