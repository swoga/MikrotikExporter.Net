﻿using System.Collections.Generic;
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
        public string LabelNameOrName => LabelName ?? Name.Replace("-", "_", System.StringComparison.InvariantCulture);

        internal string AsString(Log log, ITikReSentence tikSentence, Dictionary<string, string> variables)
        {
            log.Debug2("try to get value");

            if (ParamType == ParamType.String && Name != null && tikSentence.TryGetResponseField(Name, out var word))
            {
                log.Debug2("got as string from api response");
                return word;
            }
            else if (ParamType != ParamType.String && TryGetValue(log, tikSentence, variables, out var value))
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                log.Debug2("use default value");
                return GetDefaultWithSubstitution(variables) ?? "";
            }
        }
    }
}
