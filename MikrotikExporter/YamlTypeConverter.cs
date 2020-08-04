using System;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace MikrotikExporter
{
    class YamlTypeConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return type == typeof(Regex);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            var scalar = parser.Consume<Scalar>();

            if (scalar != null)
            {
                return new Regex(scalar.Value);
            }
            return null;
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            throw new NotImplementedException();
        }
    }
}
