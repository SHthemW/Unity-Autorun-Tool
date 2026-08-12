using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class AutoRunUiStateService
{
    private const int DefaultLimit = 100;
    private const int MaximumLimit = 500;

    private static readonly HashSet<string> SupportedComparisons =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "exists",
            "notexists",
            "equals",
            "notequals",
            "contains",
            "notcontains",
            "startswith",
            "endswith",
            "greaterthan",
            "greaterthanorequal",
            "lessthan",
            "lessthanorequal",
            "isempty",
            "isnotempty",
        };

    public static AutoRunUiStateQueryResult Query(AutoRunUiStateQuery query)
    {
        query = query ?? new AutoRunUiStateQuery();
        var elements = new List<AutoRunUiElementState>();
        elements.AddRange(AutoRunUGuiStateAdapter.Read(
            query.IncludeInactive,
            query.IncludeSensitive));
        elements.AddRange(AutoRunTmpUiStateAdapter.Read(
            query.IncludeInactive,
            query.IncludeSensitive));

        IEnumerable<AutoRunUiElementState> matches = elements
            .Where(element => MatchesScope(element, query.Scope))
            .Where(element => MatchesTypes(element, query.Types))
            .Where(element => MatchesQuery(element, query.Query, query.Exact))
            .OrderBy(element => element.path, StringComparer.Ordinal)
            .ThenBy(element => element.type, StringComparer.Ordinal);
        List<AutoRunUiElementState> allMatches = matches.ToList();
        int limit = Mathf.Clamp(
            query.Limit <= 0 ? DefaultLimit : query.Limit,
            1,
            MaximumLimit);
        List<AutoRunUiElementState> returned = allMatches.Take(limit).ToList();
        return new AutoRunUiStateQueryResult
        {
            Total = allMatches.Count,
            Truncated = allMatches.Count > returned.Count,
            Elements = returned,
            AllElements = allMatches,
        };
    }

    public static bool TryValidateComparison(
        string comparison,
        out string normalized,
        out string error)
    {
        normalized = NormalizeComparison(comparison);
        if (SupportedComparisons.Contains(normalized))
        {
            error = null;
            return true;
        }

        error = "Unsupported UI state comparison: " + comparison + ".";
        return false;
    }

    public static AutoRunUiStateAssertionResult Evaluate(
        AutoRunUiStateQueryResult queryResult,
        string propertyName,
        string comparison,
        string expected)
    {
        var result = new AutoRunUiStateAssertionResult();
        string normalizedComparison = NormalizeComparison(comparison);
        string requestedProperty = string.IsNullOrWhiteSpace(propertyName)
            ? "value"
            : propertyName;
        expected = expected ?? string.Empty;

        if (normalizedComparison == "exists"
            || normalizedComparison == "notexists")
        {
            List<AutoRunUiElementState> existing = queryResult.AllElements
                .Where(element =>
                    string.Equals(
                        requestedProperty,
                        "value",
                        StringComparison.OrdinalIgnoreCase)
                    || element.FindProperty(requestedProperty) != null)
                .ToList();
            bool exists = existing.Count > 0;
            result.Matched = normalizedComparison == "exists" ? exists : !exists;
            result.MatchedElements = result.Matched && exists
                ? existing.Take(1).ToList()
                : new List<AutoRunUiElementState>();
            result.ActualValue = exists ? "exists" : "not-exists";
            return result;
        }

        foreach (AutoRunUiElementState element in queryResult.AllElements)
        {
            AutoRunUiStateProperty property = element.FindProperty(
                requestedProperty);
            if (property == null)
            {
                continue;
            }

            if (result.ActualValue == null)
            {
                result.ActualValue = property.value;
            }

            if (!Compare(property, normalizedComparison, expected))
            {
                continue;
            }

            result.Matched = true;
            result.ActualValue = property.value;
            result.MatchedElements.Add(element);
            return result;
        }

        return result;
    }

    private static bool MatchesScope(
        AutoRunUiElementState element,
        string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return true;
        }

        if (ContainsIgnoreCase(element.path, scope))
        {
            return true;
        }

        string normalizedScope = NormalizeIdentity(scope);
        return normalizedScope.Length > 0
            && NormalizeIdentity(element.path).Contains(normalizedScope);
    }

    private static bool MatchesTypes(
        AutoRunUiElementState element,
        List<string> types)
    {
        if (types == null || types.Count == 0)
        {
            return true;
        }

        string normalizedType = NormalizeIdentity(element.type);
        return types.Any(type =>
            !string.IsNullOrWhiteSpace(type)
            && string.Equals(
                normalizedType,
                NormalizeIdentity(type),
                StringComparison.Ordinal));
    }

    private static bool MatchesQuery(
        AutoRunUiElementState element,
        string query,
        bool exact)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        IEnumerable<string> candidates = new[]
            {
                element.name,
                element.path,
                element.type,
            }
            .Concat(element.properties.Select(property =>
                property?.ComparableValue));
        return exact
            ? candidates.Any(candidate => string.Equals(
                candidate,
                query,
                StringComparison.OrdinalIgnoreCase))
            : candidates.Any(candidate => ContainsIgnoreCase(candidate, query));
    }

    private static bool Compare(
        AutoRunUiStateProperty property,
        string comparison,
        string expected)
    {
        if (property.masked)
        {
            return false;
        }

        string actual = property.ComparableValue
            ?? property.value
            ?? string.Empty;
        switch (comparison)
        {
            case "equals":
                return AreEqual(property.valueType, actual, expected);
            case "notequals":
                return !AreEqual(property.valueType, actual, expected);
            case "contains":
                return actual.IndexOf(expected, StringComparison.Ordinal) >= 0;
            case "notcontains":
                return actual.IndexOf(expected, StringComparison.Ordinal) < 0;
            case "startswith":
                return actual.StartsWith(expected, StringComparison.Ordinal);
            case "endswith":
                return actual.EndsWith(expected, StringComparison.Ordinal);
            case "greaterthan":
                return CompareNumbers(actual, expected, value => value > 0);
            case "greaterthanorequal":
                return CompareNumbers(actual, expected, value => value >= 0);
            case "lessthan":
                return CompareNumbers(actual, expected, value => value < 0);
            case "lessthanorequal":
                return CompareNumbers(actual, expected, value => value <= 0);
            case "isempty":
                return actual.Length == 0;
            case "isnotempty":
                return actual.Length > 0;
            default:
                return false;
        }
    }

    private static bool AreEqual(
        string valueType,
        string actual,
        string expected)
    {
        if (string.Equals(
                valueType,
                "boolean",
                StringComparison.OrdinalIgnoreCase)
            && bool.TryParse(actual, out bool actualBool)
            && bool.TryParse(expected, out bool expectedBool))
        {
            return actualBool == expectedBool;
        }

        if ((string.Equals(
                    valueType,
                    "number",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    valueType,
                    "integer",
                    StringComparison.OrdinalIgnoreCase))
            && TryParseNumber(actual, out decimal actualNumber)
            && TryParseNumber(expected, out decimal expectedNumber))
        {
            return actualNumber == expectedNumber;
        }

        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static bool CompareNumbers(
        string actual,
        string expected,
        Func<int, bool> predicate)
    {
        return TryParseNumber(actual, out decimal actualNumber)
            && TryParseNumber(expected, out decimal expectedNumber)
            && predicate(actualNumber.CompareTo(expectedNumber));
    }

    private static bool TryParseNumber(string value, out decimal number)
    {
        return decimal.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static string NormalizeComparison(string comparison)
    {
        string normalized = string.IsNullOrWhiteSpace(comparison)
            ? "equals"
            : comparison.ToLowerInvariant();
        return normalized
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);
    }

    private static string NormalizeIdentity(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.ToLowerInvariant().Replace("(clone)", string.Empty);
        return Regex.Replace(
            normalized,
            @"[^\p{L}\p{Nd}]",
            string.Empty);
    }

    private static bool ContainsIgnoreCase(string value, string query)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
