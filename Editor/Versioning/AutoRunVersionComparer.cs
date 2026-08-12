using System;
using System.Collections.Generic;

public static class AutoRunVersionComparer
{
    public static bool TryCompare(string left, string right, out int comparison)
    {
        comparison = 0;
        if (!TryParse(left, out ParsedVersion leftVersion)
            || !TryParse(right, out ParsedVersion rightVersion))
        {
            return false;
        }

        int numericCount = Math.Max(
            leftVersion.Numbers.Count,
            rightVersion.Numbers.Count);
        for (int index = 0; index < numericCount; index++)
        {
            int leftNumber = index < leftVersion.Numbers.Count
                ? leftVersion.Numbers[index]
                : 0;
            int rightNumber = index < rightVersion.Numbers.Count
                ? rightVersion.Numbers[index]
                : 0;
            if (leftNumber == rightNumber)
            {
                continue;
            }

            comparison = leftNumber > rightNumber ? 1 : -1;
            return true;
        }

        if (leftVersion.PreRelease.Count == 0
            && rightVersion.PreRelease.Count == 0)
        {
            return true;
        }

        if (leftVersion.PreRelease.Count == 0)
        {
            comparison = 1;
            return true;
        }

        if (rightVersion.PreRelease.Count == 0)
        {
            comparison = -1;
            return true;
        }

        int preReleaseCount = Math.Max(
            leftVersion.PreRelease.Count,
            rightVersion.PreRelease.Count);
        for (int index = 0; index < preReleaseCount; index++)
        {
            if (index >= leftVersion.PreRelease.Count)
            {
                comparison = -1;
                return true;
            }

            if (index >= rightVersion.PreRelease.Count)
            {
                comparison = 1;
                return true;
            }

            string leftIdentifier = leftVersion.PreRelease[index];
            string rightIdentifier = rightVersion.PreRelease[index];
            bool leftNumeric = int.TryParse(leftIdentifier, out int leftNumber);
            bool rightNumeric = int.TryParse(rightIdentifier, out int rightNumber);
            if (leftNumeric && rightNumeric)
            {
                if (leftNumber == rightNumber)
                {
                    continue;
                }

                comparison = leftNumber > rightNumber ? 1 : -1;
                return true;
            }

            if (leftNumeric != rightNumeric)
            {
                comparison = leftNumeric ? -1 : 1;
                return true;
            }

            int identifierComparison = string.Compare(
                leftIdentifier,
                rightIdentifier,
                StringComparison.OrdinalIgnoreCase);
            if (identifierComparison != 0)
            {
                comparison = identifierComparison > 0 ? 1 : -1;
                return true;
            }
        }

        return true;
    }

    private static bool TryParse(string value, out ParsedVersion version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(1);
        }

        int metadataSeparator = value.IndexOf('+');
        if (metadataSeparator >= 0)
        {
            value = value.Substring(0, metadataSeparator);
        }

        string core = value;
        string preRelease = "";
        int preReleaseSeparator = value.IndexOf('-');
        if (preReleaseSeparator >= 0)
        {
            core = value.Substring(0, preReleaseSeparator);
            preRelease = value.Substring(preReleaseSeparator + 1);
            if (string.IsNullOrEmpty(preRelease))
            {
                return false;
            }
        }

        string[] numberParts = core.Split(
            new[] { '.' },
            StringSplitOptions.None);
        if (numberParts.Length == 0)
        {
            return false;
        }

        var numbers = new List<int>();
        foreach (string numberPart in numberParts)
        {
            if (!int.TryParse(numberPart, out int number) || number < 0)
            {
                return false;
            }

            numbers.Add(number);
        }

        var preReleaseParts = new List<string>();
        if (!string.IsNullOrEmpty(preRelease))
        {
            foreach (string identifier in preRelease.Split(
                new[] { '.' },
                StringSplitOptions.None))
            {
                if (string.IsNullOrEmpty(identifier))
                {
                    return false;
                }

                preReleaseParts.Add(identifier);
            }
        }

        version = new ParsedVersion(numbers, preReleaseParts);
        return true;
    }

    private sealed class ParsedVersion
    {
        public ParsedVersion(List<int> numbers, List<string> preRelease)
        {
            Numbers = numbers;
            PreRelease = preRelease;
        }

        public List<int> Numbers { get; }
        public List<string> PreRelease { get; }
    }
}
