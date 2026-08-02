using System;
using System.Globalization;
using System.Text;

namespace Soundpacks_Framework.Serialization.Json
{
    public sealed class JsonParseException : Exception
    {
        public int Position { get; }

        public JsonParseException(string message, int position) : base($"{message} (at position {position})")
        {
            Position = position;
        }
    }

    public static class JsonParser
    {
        public static JsonValue Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            int pos = 0;
            SkipWhitespace(text, ref pos);
            JsonValue value = ParseValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length)
            {
                throw new JsonParseException("Unexpected trailing content", pos);
            }
            return value;
        }

        public static bool TryParse(string text, out JsonValue value, out string error)
        {
            try
            {
                value = Parse(text);
                error = null;
                return true;
            }
            catch (JsonParseException ex)
            {
                value = null;
                error = ex.Message;
                return false;
            }
        }

        private static JsonValue ParseValue(string s, ref int pos)
        {
            if (pos >= s.Length)
            {
                throw new JsonParseException("Unexpected end of input", pos);
            }

            char c = s[pos];
            switch (c)
            {
                case '{':
                    return ParseObject(s, ref pos);
                case '[':
                    return ParseArray(s, ref pos);
                case '"':
                    return JsonValue.Of(ParseString(s, ref pos));
                case 't':
                    Expect(s, ref pos, "true");
                    return JsonValue.Of(true);
                case 'f':
                    Expect(s, ref pos, "false");
                    return JsonValue.Of(false);
                case 'n':
                    Expect(s, ref pos, "null");
                    return JsonValue.Null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return ParseNumber(s, ref pos);
                    }
                    throw new JsonParseException($"Unexpected character '{c}'", pos);
            }
        }

        private static void Expect(string s, ref int pos, string literal)
        {
            if (pos + literal.Length > s.Length || string.CompareOrdinal(s, pos, literal, 0, literal.Length) != 0)
            {
                throw new JsonParseException($"Expected literal '{literal}'", pos);
            }
            pos += literal.Length;
        }

        private static JsonValue ParseObject(string s, ref int pos)
        {
            pos++;
            var obj = JsonValue.NewObject();
            SkipWhitespace(s, ref pos);
            if (Peek(s, pos) == '}')
            {
                pos++;
                return obj;
            }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                if (Peek(s, pos) != '"')
                {
                    throw new JsonParseException("Expected string key", pos);
                }
                string key = ParseString(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (Peek(s, pos) != ':')
                {
                    throw new JsonParseException("Expected ':' after object key", pos);
                }
                pos++;
                SkipWhitespace(s, ref pos);
                JsonValue value = ParseValue(s, ref pos);
                obj.Set(key, value);
                SkipWhitespace(s, ref pos);
                char next = Peek(s, pos);
                if (next == ',')
                {
                    pos++;
                    continue;
                }
                if (next == '}')
                {
                    pos++;
                    break;
                }
                throw new JsonParseException("Expected ',' or '}' in object", pos);
            }
            return obj;
        }

        private static JsonValue ParseArray(string s, ref int pos)
        {
            pos++;
            var array = JsonValue.NewArray();
            SkipWhitespace(s, ref pos);
            if (Peek(s, pos) == ']')
            {
                pos++;
                return array;
            }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                array.Add(ParseValue(s, ref pos));
                SkipWhitespace(s, ref pos);
                char next = Peek(s, pos);
                if (next == ',')
                {
                    pos++;
                    continue;
                }
                if (next == ']')
                {
                    pos++;
                    break;
                }
                throw new JsonParseException("Expected ',' or ']' in array", pos);
            }
            return array;
        }

        private static string ParseString(string s, ref int pos)
        {
            pos++;
            var sb = new StringBuilder();
            while (true)
            {
                if (pos >= s.Length)
                {
                    throw new JsonParseException("Unterminated string", pos);
                }
                char c = s[pos++];
                if (c == '"')
                {
                    break;
                }
                if (c == '\\')
                {
                    if (pos >= s.Length)
                    {
                        throw new JsonParseException("Unterminated escape sequence", pos);
                    }
                    char esc = s[pos++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > s.Length)
                            {
                                throw new JsonParseException("Truncated unicode escape", pos);
                            }
                            string hex = s.Substring(pos, 4);
                            if (!ushort.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code))
                            {
                                throw new JsonParseException("Invalid unicode escape", pos);
                            }
                            sb.Append((char)code);
                            pos += 4;
                            break;
                        default:
                            throw new JsonParseException($"Invalid escape character '\\{esc}'", pos);
                    }
                    continue;
                }
                if (c < 0x20)
                {
                    throw new JsonParseException("Unescaped control character in string", pos);
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static JsonValue ParseNumber(string s, ref int pos)
        {
            int start = pos;
            if (Peek(s, pos) == '-')
            {
                pos++;
            }
            if (Peek(s, pos) == '0')
            {
                pos++;
            }
            else if (IsDigit(Peek(s, pos)))
            {
                while (IsDigit(Peek(s, pos))) pos++;
            }
            else
            {
                throw new JsonParseException("Invalid number", pos);
            }

            if (Peek(s, pos) == '.')
            {
                pos++;
                if (!IsDigit(Peek(s, pos)))
                {
                    throw new JsonParseException("Invalid number fraction", pos);
                }
                while (IsDigit(Peek(s, pos))) pos++;
            }

            char e = Peek(s, pos);
            if (e == 'e' || e == 'E')
            {
                pos++;
                char sign = Peek(s, pos);
                if (sign == '+' || sign == '-')
                {
                    pos++;
                }
                if (!IsDigit(Peek(s, pos)))
                {
                    throw new JsonParseException("Invalid number exponent", pos);
                }
                while (IsDigit(Peek(s, pos))) pos++;
            }

            string token = s.Substring(start, pos - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                throw new JsonParseException("Unparsable number token", start);
            }
            return JsonValue.Of(result);
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static char Peek(string s, int pos) => pos < s.Length ? s[pos] : '\0';

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    pos++;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
