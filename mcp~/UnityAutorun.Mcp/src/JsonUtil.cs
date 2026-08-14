using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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

        public static bool SemanticallyEquals(JsonNode left, JsonNode right)
        {
            return JsonNode.DeepEquals(left, right);
        }

        public static JsonObject ApplyMergePatch(JsonObject baseline, JsonObject patch)
        {
            var result = baseline != null
                ? baseline.DeepClone().AsObject()
                : new JsonObject();
            if (patch == null)
            {
                return result;
            }

            foreach (var property in patch)
            {
                if (property.Value == null)
                {
                    result.Remove(property.Key);
                    continue;
                }

                JsonObject patchObject = property.Value as JsonObject;
                JsonObject baselineObject = result[property.Key] as JsonObject;
                result[property.Key] = patchObject != null
                    ? ApplyMergePatch(baselineObject, patchObject)
                    : property.Value.DeepClone();
            }

            return result;
        }

        public static string StableSemanticHash(JsonNode node)
        {
            string canonicalJson = Canonicalize(node).ToJsonString();
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalJson));
                return string.Concat(hash.Take(16).Select(value => value.ToString("x2")));
            }
        }

        private static JsonNode Canonicalize(JsonNode node)
        {
            if (node == null)
            {
                return null;
            }

            JsonObject sourceObject = node as JsonObject;
            if (sourceObject != null)
            {
                var result = new JsonObject();
                foreach (var property in sourceObject.OrderBy(
                    item => item.Key,
                    StringComparer.Ordinal))
                {
                    result[property.Key] = Canonicalize(property.Value);
                }

                return result;
            }

            JsonArray sourceArray = node as JsonArray;
            if (sourceArray != null)
            {
                var result = new JsonArray();
                foreach (JsonNode item in sourceArray)
                {
                    result.Add(Canonicalize(item));
                }

                return result;
            }

            return node.DeepClone();
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

