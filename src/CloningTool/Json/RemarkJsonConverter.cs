using System;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CloningTool.Json
{
    internal class RemarkJsonConverter : JsonConverter
    {
        public override bool CanRead => false;

        public override bool CanConvert(Type objectType) => objectType == typeof(Remark);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => throw new NotSupportedException();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Remark remark)
            {
                var jsonSerializer = new JsonSerializer
                    {
                        ContractResolver = serializer.ContractResolver,
                        Culture = serializer.Culture
                    };

                foreach (var converter in serializer.Converters.Where(c => c.GetType() != typeof(RemarkJsonConverter)))
                {
                    jsonSerializer.Converters.Add(converter);
                }

                var json = JObject.FromObject(remark, jsonSerializer);
                if (remark.Category != null)
                {
                    json[nameof(remark.Category).ToLowerInvariant()] = remark.Category.Id;
                }

                json.WriteTo(writer);
            }
        }
    }
}
