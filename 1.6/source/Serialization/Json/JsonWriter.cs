using System;
using System.Globalization;
using System.Text;

namespace Soundpacks_Framework.Serialization.Json
{
    public static class JsonWriter
    {
        public static string Write(JsonValue value, bool canonical = true, bool indent = true)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, canonical, indent, 0);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, JsonValue value, bool canonical, bool indent, int depth)
        {
            switch (value.Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Bool:
                    sb.Append(value.AsBool() ? "true" : "false");
                    break;
                case JsonKind.Number:
                    sb.Append(FormatNumber(value.AsNumber()));
                    break;
                case JsonKind.String:
                    WriteString(sb, value.AsString(string.Empty));
                    break;
                case JsonKind.Array:
                    WriteArray(sb, value, canonical, indent, depth);
                    break;
                case JsonKind.Object:
                    WriteObject(sb, value, canonical, indent, depth);
                    break;
                default:
                    throw new InvalidOperationException("Unknown JsonKind: " + value.Kind);
            }
        }

        private static void WriteArray(StringBuilder sb, JsonValue array, bool canonical, bool indent, int depth)
        {
            var items = array.ArrayItems;
            if (items.Count == 0)
            {
                sb.Append("[]");
                return;
            }
            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                NewlineIndent(sb, indent, depth + 1);
                WriteValue(sb, items[i], canonical, indent, depth + 1);
            }
            NewlineIndent(sb, indent, depth);
            sb.Append(']');
        }

        private static void WriteObject(StringBuilder sb, JsonValue obj, bool canonical, bool indent, int depth)
        {
            var members = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, JsonValue>>(obj.ObjectMembers);
            if (members.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            if (canonical)
            {
                members.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            }
            sb.Append('{');
            for (int i = 0; i < members.Count; i++)
            {
                if (i > 0) sb.Append(',');
                NewlineIndent(sb, indent, depth + 1);
                WriteString(sb, members[i].Key);
                sb.Append(':');
                if (indent) sb.Append(' ');
                WriteValue(sb, members[i].Value, canonical, indent, depth + 1);
            }
            NewlineIndent(sb, indent, depth);
            sb.Append('}');
        }

        private static void NewlineIndent(StringBuilder sb, bool indent, int depth)
        {
            if (!indent) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        private static string FormatNumber(double number)
        {
            if (number == Math.Floor(number) && !double.IsInfinity(number) && Math.Abs(number) < 1e15)
            {
                return ((long)number).ToString(CultureInfo.InvariantCulture);
            }
            return number.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
