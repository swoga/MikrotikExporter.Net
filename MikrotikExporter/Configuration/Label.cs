using System.ComponentModel.DataAnnotations;
using System.Globalization;
using tik4net;
using YamlDotNet.Serialization;

namespace MikrotikExporter.Configuration
{
    public class Label : Param
    {
        /// <summary>
        /// Use different name for label than parameter name
        /// </summary>
        [YamlMember(Alias = "label_name")]
        public string LabelName { get; private set; }

        [Required]
        [RegularExpression(@"^(?!__)[a-zA-Z_:][a-zA-Z0-9_:]*$")]
        [YamlIgnore]
        public string LabelNameOrName
        {
            get
            {
                return LabelName ?? Name;
            }
        }

        internal string AsString(Log log, ITikReSentence tikSentence)
        {
            log.Debug2($"try to get '{Name}'");

            if (ParamType == ParamType.String && tikSentence.TryGetResponseField(Name, out var word))
            {
                log.Debug2($"got '{Name}' as string from api response");
                return word;
            }
            else if (TryGetValue(log, tikSentence, out var value))
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                log.Debug2($"use default value for '{Name}'");
                return Default ?? "";
            }
        }
    }
}
