using System;

public static class JsonObjectUpdater
{
    public static string UpsertMcpServer(string json, string serverName, string serverEntry)
    {
        int mcpName = json.IndexOf("\"mcpServers\"", StringComparison.Ordinal);
        if (mcpName < 0)
        {
            return InsertTopLevelMcpServers(json, serverEntry);
        }

        int objectStart = json.IndexOf('{', mcpName);
        int objectEnd = FindMatchingBrace(json, objectStart);
        if (objectStart < 0 || objectEnd < 0)
        {
            return "{\n  \"mcpServers\": {\n    " + serverEntry + "\n  }\n}\n";
        }

        string objectBody = json.Substring(objectStart + 1, objectEnd - objectStart - 1);
        string serverKey = "\"" + serverName + "\"";
        int existingServer = objectBody.IndexOf(serverKey, StringComparison.Ordinal);
        string updatedBody;
        if (existingServer >= 0)
        {
            int valueStart = objectBody.IndexOf('{', existingServer);
            int valueEnd = FindMatchingBrace(objectBody, valueStart);
            if (valueStart >= 0 && valueEnd >= 0)
            {
                updatedBody = objectBody.Substring(0, existingServer)
                    + serverEntry
                    + objectBody.Substring(valueEnd + 1);
            }
            else
            {
                updatedBody = BuildInsertedBody(objectBody, serverEntry);
            }
        }
        else
        {
            updatedBody = BuildInsertedBody(objectBody, serverEntry);
        }

        return json.Substring(0, objectStart + 1)
            + updatedBody
            + json.Substring(objectEnd);
    }

    private static string InsertTopLevelMcpServers(string json, string serverEntry)
    {
        int rootStart = json.IndexOf('{');
        if (rootStart < 0)
        {
            return "{\n  \"mcpServers\": {\n    " + serverEntry + "\n  }\n}\n";
        }

        string insert = "\n  \"mcpServers\": {\n    " + serverEntry + "\n  }";
        string tail = json.Substring(rootStart + 1).TrimStart();
        if (tail.Length > 0 && tail[0] != '}')
        {
            insert += ",";
        }

        return json.Substring(0, rootStart + 1) + insert + "\n" + json.Substring(rootStart + 1);
    }

    private static string BuildInsertedBody(string objectBody, string serverEntry)
    {
        if (string.IsNullOrWhiteSpace(objectBody))
        {
            return "\n    " + serverEntry + "\n  ";
        }

        string trimmed = objectBody.TrimEnd();
        return trimmed + ",\n    " + serverEntry + "\n  ";
    }

    private static int FindMatchingBrace(string text, int start)
    {
        if (start < 0 || start >= text.Length || text[start] != '{')
        {
            return -1;
        }

        bool inString = false;
        bool escaped = false;
        int depth = 0;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }
}
