using System.Collections.Generic;
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
        public string LabelName { get; protected set; }

        [Required]
        [RegularExpression(@"^(?!__)[a-zA-Z_:][a-zA-Z0-9_:]*$")]
        [YamlIgnore]
        public string LabelNameOrName => LabelName ?? Name.Replace("-", "_", System.StringComparison.InvariantCulture);

        internal string AsString(Log log, ITikReSentence tikSentence, Dictionary<string, string> variables)
        {
            if (ParamType == ParamType.String)
            {
                if (PreprocessValue(log, tikSentence, variables, out var word))
                {
                    return word;
                }
                else
                {
                    return Substitute(Default, variables) ?? "";
                }
            }
            else
            {
                // TryGetValue also handles default values for non-string parameters
                if (TryGetValue(log, tikSentence, variables, out var value))
                {
                    return value.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    return "";
                }
            }
        }
    }
}
