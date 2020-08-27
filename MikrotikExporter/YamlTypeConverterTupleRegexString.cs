using System;
using System.Diagnostics.Tracing;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace MikrotikExporter
{
    class YamlTypeConverterTupleRegexString : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return type == typeof(Tuple<Regex, string>);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            parser.Consume<MappingStart>();
            var regex = parser.Consume<Scalar>();
            var value = parser.Consume<Scalar>();
            parser.Consume<MappingEnd>();

            return new Tuple<Regex, string>(new Regex(regex.Value), value.Value);
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            throw new NotImplementedException();
        }
    }
}
