using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class AutoRunJsonParser
{
    public static object Parse(string json)
    {
        if (json == null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        var parser = new Parser(json);
        object value = parser.ParseValue();
        parser.SkipWhitespace();
        if (!parser.IsAtEnd)
        {
            throw parser.Error("Unexpected trailing content");
        }

        return value;
    }

    private sealed class Parser
    {
        private readonly string _json;
        private int _index;

        public Parser(string json)
        {
            _json = json;
        }

        public bool IsAtEnd => _index >= _json.Length;

        public object ParseValue()
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                throw Error("Expected a JSON value");
            }

            switch (_json[_index])
            {
                case '{':
                    return ParseObject();
                case '[':
                    return ParseArray();
                case '"':
                    return ParseString();
                case 't':
                    ConsumeLiteral("true");
                    return true;
                case 'f':
                    ConsumeLiteral("false");
                    return false;
                case 'n':
                    ConsumeLiteral("null");
                    return null;
                default:
                    return ParseNumber();
            }
        }

        public void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(_json[_index]))
            {
                _index++;
            }
        }

        public FormatException Error(string message)
        {
            return new FormatException(message + " at JSON offset " + _index + ".");
        }

        private Dictionary<string, object> ParseObject()
        {
            Expect('{');
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                if (IsAtEnd || _json[_index] != '"')
                {
                    throw Error("Expected an object property name");
                }

                string key = ParseString();
                SkipWhitespace();
                Expect(':');
                result[key] = ParseValue();
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private List<object> ParseArray()
        {
            Expect('[');
            var result = new List<object>();
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return result;
            }

            while (true)
            {
                result.Add(ParseValue());
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private string ParseString()
        {
            Expect('"');
            var result = new StringBuilder();
            while (!IsAtEnd)
            {
                char value = _json[_index++];
                if (value == '"')
                {
                    return result.ToString();
                }

                if (value != '\\')
                {
                    if (value < 0x20)
                    {
                        throw Error("Unescaped control character in string");
                    }

                    result.Append(value);
                    continue;
                }

                if (IsAtEnd)
                {
                    throw Error("Incomplete string escape");
                }

                char escaped = _json[_index++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        result.Append(escaped);
                        break;
                    case 'b':
                        result.Append('\b');
                        break;
                    case 'f':
                        result.Append('\f');
                        break;
                    case 'n':
                        result.Append('\n');
                        break;
                    case 'r':
                        result.Append('\r');
                        break;
                    case 't':
                        result.Append('\t');
                        break;
                    case 'u':
                        result.Append(ParseUnicodeEscape());
                        break;
                    default:
                        throw Error("Invalid string escape");
                }
            }

            throw Error("Unterminated string");
        }

        private char ParseUnicodeEscape()
        {
            if (_index + 4 > _json.Length)
            {
                throw Error("Incomplete unicode escape");
            }

            string digits = _json.Substring(_index, 4);
            _index += 4;
            if (!ushort.TryParse(
                    digits,
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out ushort value))
            {
                throw Error("Invalid unicode escape");
            }

            return (char)value;
        }

        private object ParseNumber()
        {
            int start = _index;
            TryConsume('-');
            ConsumeDigits();
            bool fractional = TryConsume('.');
            if (fractional)
            {
                ConsumeDigits();
            }

            if (TryConsume('e') || TryConsume('E'))
            {
                fractional = true;
                if (!TryConsume('+'))
                {
                    TryConsume('-');
                }
                ConsumeDigits();
            }

            if (start == _index)
            {
                throw Error("Expected a JSON value");
            }

            string token = _json.Substring(start, _index - start);
            if (!fractional
                && long.TryParse(
                    token,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long integer))
            {
                return integer;
            }

            if (double.TryParse(
                    token,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double number))
            {
                return number;
            }

            throw Error("Invalid JSON number");
        }

        private void ConsumeDigits()
        {
            int start = _index;
            while (!IsAtEnd && _json[_index] >= '0' && _json[_index] <= '9')
            {
                _index++;
            }

            if (start == _index)
            {
                throw Error("Expected a digit");
            }
        }

        private void ConsumeLiteral(string literal)
        {
            if (_index + literal.Length > _json.Length
                || string.CompareOrdinal(_json, _index, literal, 0, literal.Length) != 0)
            {
                throw Error("Invalid JSON literal");
            }

            _index += literal.Length;
        }

        private void Expect(char expected)
        {
            SkipWhitespace();
            if (!TryConsume(expected))
            {
                throw Error("Expected '" + expected + "'");
            }
        }

        private bool TryConsume(char expected)
        {
            if (IsAtEnd || _json[_index] != expected)
            {
                return false;
            }

            _index++;
            return true;
        }
    }
}
