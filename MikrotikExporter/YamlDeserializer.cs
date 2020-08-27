using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace MikrotikExporter
{
    class YamlDeserializer
    {
        internal static T Parse<T>(StreamReader streamReader)
        {
            var deserializer = new DeserializerBuilder();
            deserializer.WithTypeConverter(new YamlTypeConverterRegex());
            deserializer.WithTypeConverter(new YamlTypeConverterTupleRegexString());
            deserializer.WithNodeDeserializer(inner => new ValidatingNodeDeserializer(inner), s => s.InsteadOf<ObjectNodeDeserializer>());
            return deserializer.Build().Deserialize<T>(streamReader);
        }
    }
}
