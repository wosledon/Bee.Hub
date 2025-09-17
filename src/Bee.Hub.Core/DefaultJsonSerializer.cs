using System.Text.Json;

namespace Bee.Hub.Core
{
    public class DefaultJsonSerializer : IMessageSerializer
    {
        private readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public DefaultJsonSerializer()
        {
        }

        public byte[] Serialize<T>(T obj)
        {
            return JsonSerializer.SerializeToUtf8Bytes(obj, _opts);
        }

        public T Deserialize<T>(byte[] data)
        {
            return JsonSerializer.Deserialize<T>(data, _opts)!;
        }
    }
}