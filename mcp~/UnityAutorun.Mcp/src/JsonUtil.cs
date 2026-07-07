using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace UnityAutorun.Mcp
{
    public static class JsonUtil
    {
        public static readonly JsonSerializerOptions PrettyOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        public static string Pretty(JsonNode node)
        {
            return node != null ? node.ToJsonString(PrettyOptions) : "null";
        }

        public static JsonObject Obj(params (string Key, JsonNode Value)[] values)
        {
            var obj = new JsonObject();
            foreach ((string key, JsonNode value) in values)
            {
                obj[key] = value;
            }

            return obj;
        }

        public static JsonArray CloneArray(JsonArray source)
        {
            var array = new JsonArray();
            foreach (JsonNode item in source)
            {
                array.Add(item != null ? item.DeepClone() : null);
            }

            return array;
        }
    }
}

