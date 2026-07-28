using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace UnityAutorun.Mcp
{
    public static partial class UiNavMapSourceScanner
    {
        private static readonly Regex YamlDocumentHeaderRegex = new Regex(
            @"^--- !u!(?<classId>\d+) &(?<fileId>-?\d+)",
            RegexOptions.Compiled);

        private static readonly Regex YamlFileIdRegex = new Regex(
            @"\{fileID:\s*(?<fileId>-?\d+)",
            RegexOptions.Compiled);

        private static void ResolveSerializedControlEvidence(
            string assetsRoot,
            IEnumerable<ButtonBinding> bindings)
        {
            List<ButtonBinding> bindingList = bindings == null
                ? new List<ButtonBinding>()
                : bindings.ToList();
            if (!Directory.Exists(assetsRoot) || bindingList.Count == 0)
            {
                return;
            }

            Dictionary<string, List<string>> prefabPathsByName = Directory
                .EnumerateFiles(assetsRoot, "*.prefab", SearchOption.AllDirectories)
                .GroupBy(
                    path => Path.GetFileNameWithoutExtension(path),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, ButtonBinding> ownerBindings in bindingList
                .GroupBy(item => item.OwnerType, StringComparer.OrdinalIgnoreCase))
            {
                List<string> prefabPaths;
                if (!prefabPathsByName.TryGetValue(ownerBindings.Key, out prefabPaths))
                {
                    continue;
                }

                var properties = new HashSet<string>(
                    ownerBindings.Select(item => item.ControlProperty),
                    StringComparer.OrdinalIgnoreCase);
                var resolvedByProperty = properties.ToDictionary(
                    property => property,
                    property => new List<SerializedControlEvidence>(),
                    StringComparer.OrdinalIgnoreCase);

                foreach (string prefabPath in prefabPaths)
                {
                    Dictionary<string, List<SerializedControlEvidence>> prefabEvidence =
                        ReadSerializedControlsFromPrefab(
                            assetsRoot,
                            prefabPath,
                            ownerBindings.Key,
                            properties);
                    foreach (KeyValuePair<string, List<SerializedControlEvidence>> pair in prefabEvidence)
                    {
                        resolvedByProperty[pair.Key].AddRange(pair.Value);
                    }
                }

                foreach (ButtonBinding binding in ownerBindings)
                {
                    binding.ReplaceSerializedControls(
                        resolvedByProperty[binding.ControlProperty]
                            .OrderBy(item => item.PrefabPath, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(item => item.ObjectPath, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(item => item.ReferencedComponentFileId, StringComparer.Ordinal)
                            .ToList());
                }
            }
        }

        private static Dictionary<string, List<SerializedControlEvidence>>
            ReadSerializedControlsFromPrefab(
                string assetsRoot,
                string prefabPath,
                string ownerType,
                HashSet<string> controlProperties)
        {
            var result = controlProperties.ToDictionary(
                property => property,
                property => new List<SerializedControlEvidence>(),
                StringComparer.OrdinalIgnoreCase);
            var documents = new List<PrefabYamlDocument>();
            PrefabYamlDocument current = null;

            foreach (string line in File.ReadLines(prefabPath))
            {
                Match header = YamlDocumentHeaderRegex.Match(line);
                if (header.Success)
                {
                    if (current != null)
                    {
                        documents.Add(current);
                    }

                    int classId;
                    int.TryParse(header.Groups["classId"].Value, out classId);
                    current = new PrefabYamlDocument(
                        classId,
                        header.Groups["fileId"].Value);
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                if (line.StartsWith("  m_Name:", StringComparison.Ordinal))
                {
                    current.Name = Unquote(line.Substring("  m_Name:".Length).Trim());
                    continue;
                }

                if (line.StartsWith("  m_GameObject:", StringComparison.Ordinal))
                {
                    current.GameObjectFileId = ExtractYamlFileId(line);
                    continue;
                }

                if (line.StartsWith("  m_Father:", StringComparison.Ordinal))
                {
                    current.ParentTransformFileId = ExtractYamlFileId(line);
                    continue;
                }

                if (!line.StartsWith("  m_", StringComparison.Ordinal))
                {
                    continue;
                }

                int separator = line.IndexOf(':', 2);
                if (separator <= 4)
                {
                    continue;
                }

                string serializedField = line.Substring(2, separator - 2);
                string controlProperty = serializedField.StartsWith(
                    "m_",
                    StringComparison.Ordinal)
                    ? serializedField.Substring(2)
                    : serializedField;
                if (!controlProperties.Contains(controlProperty))
                {
                    continue;
                }

                string referencedFileId = ExtractYamlFileId(line);
                if (referencedFileId != null)
                {
                    current.ControlReferences[controlProperty] = referencedFileId;
                }
            }

            if (current != null)
            {
                documents.Add(current);
            }

            var gameObjectNames = documents
                .Where(item => item.ClassId == 1 && item.Name != null)
                .GroupBy(item => item.FileId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().Name,
                    StringComparer.Ordinal);
            var componentGameObjects = documents
                .Where(item => item.GameObjectFileId != null)
                .GroupBy(item => item.FileId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().GameObjectFileId,
                    StringComparer.Ordinal);
            var transformsByGameObject = documents
                .Where(item =>
                    (item.ClassId == 4 || item.ClassId == 224)
                    && item.GameObjectFileId != null)
                .GroupBy(item => item.GameObjectFileId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last(),
                    StringComparer.Ordinal);

            foreach (PrefabYamlDocument document in documents)
            {
                foreach (KeyValuePair<string, string> reference in document.ControlReferences)
                {
                    string gameObjectFileId;
                    componentGameObjects.TryGetValue(reference.Value, out gameObjectFileId);
                    string objectName;
                    gameObjectNames.TryGetValue(gameObjectFileId ?? "", out objectName);
                    string objectPath = BuildPrefabObjectPath(
                        gameObjectFileId,
                        gameObjectNames,
                        transformsByGameObject,
                        componentGameObjects);
                    string status = reference.Value == "0"
                        ? "null-reference"
                        : objectName != null
                            ? "resolved"
                            : "serialized-reference";

                    result[reference.Key].Add(new SerializedControlEvidence(
                        status,
                        ownerType,
                        "m_" + reference.Key,
                        RelativeToAssets(assetsRoot, prefabPath),
                        reference.Value,
                        objectName,
                        objectPath,
                        "owner-type-prefab-name-and-serialized-field"));
                }
            }

            return result;
        }

        private static string BuildPrefabObjectPath(
            string gameObjectFileId,
            Dictionary<string, string> gameObjectNames,
            Dictionary<string, PrefabYamlDocument> transformsByGameObject,
            Dictionary<string, string> componentGameObjects)
        {
            if (string.IsNullOrWhiteSpace(gameObjectFileId))
            {
                return null;
            }

            var names = new List<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string currentGameObject = gameObjectFileId;
            for (int depth = 0;
                depth < 128
                && !string.IsNullOrWhiteSpace(currentGameObject)
                && visited.Add(currentGameObject);
                depth++)
            {
                string name;
                if (gameObjectNames.TryGetValue(currentGameObject, out name)
                    && !string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }

                PrefabYamlDocument transform;
                if (!transformsByGameObject.TryGetValue(currentGameObject, out transform)
                    || string.IsNullOrWhiteSpace(transform.ParentTransformFileId)
                    || transform.ParentTransformFileId == "0")
                {
                    break;
                }

                string parentGameObject;
                if (!componentGameObjects.TryGetValue(
                    transform.ParentTransformFileId,
                    out parentGameObject))
                {
                    break;
                }

                currentGameObject = parentGameObject;
            }

            names.Reverse();
            return names.Count == 0 ? null : string.Join("/", names);
        }

        private static string ExtractYamlFileId(string line)
        {
            Match match = YamlFileIdRegex.Match(line ?? "");
            return match.Success ? match.Groups["fileId"].Value : null;
        }

        private static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2)
            {
                return value;
            }

            return (value[0] == '"' && value[value.Length - 1] == '"')
                || (value[0] == '\'' && value[value.Length - 1] == '\'')
                    ? value.Substring(1, value.Length - 2)
                    : value;
        }

        private sealed class PrefabYamlDocument
        {
            public PrefabYamlDocument(int classId, string fileId)
            {
                ClassId = classId;
                FileId = fileId;
            }

            public int ClassId { get; }
            public string FileId { get; }
            public string Name { get; set; }
            public string GameObjectFileId { get; set; }
            public string ParentTransformFileId { get; set; }
            public Dictionary<string, string> ControlReferences { get; } =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class SerializedControlEvidence
        {
            public SerializedControlEvidence(
                string status,
                string ownerType,
                string serializedField,
                string prefabPath,
                string referencedComponentFileId,
                string objectName,
                string objectPath,
                string basis)
            {
                Status = status;
                OwnerType = ownerType;
                SerializedField = serializedField;
                PrefabPath = prefabPath;
                ReferencedComponentFileId = referencedComponentFileId;
                ObjectName = objectName;
                ObjectPath = objectPath;
                Basis = basis;
            }

            public string Status { get; }
            public string OwnerType { get; }
            public string SerializedField { get; }
            public string PrefabPath { get; }
            public string ReferencedComponentFileId { get; }
            public string ObjectName { get; }
            public string ObjectPath { get; }
            public string Basis { get; }

            public string Signature
            {
                get
                {
                    return Status
                        + "|"
                        + PrefabPath
                        + "|"
                        + SerializedField
                        + "|"
                        + ReferencedComponentFileId
                        + "|"
                        + ObjectName
                        + "|"
                        + ObjectPath;
                }
            }

            public JsonObject ToJson()
            {
                var result = JsonUtil.Obj(
                    ("status", Status),
                    ("ownerType", OwnerType),
                    ("serializedField", SerializedField),
                    ("prefabPath", PrefabPath),
                    ("referencedComponentFileId", ReferencedComponentFileId),
                    ("basis", Basis)
                );
                if (!string.IsNullOrWhiteSpace(ObjectName))
                {
                    result["objectName"] = ObjectName;
                }

                if (!string.IsNullOrWhiteSpace(ObjectPath))
                {
                    result["objectPath"] = ObjectPath;
                }

                return result;
            }
        }
    }
}
