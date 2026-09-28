using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RescueAnimalMatch.Backend
{
    /// <summary>
    /// Minimal JSON parser/serializer (sin dependencias externas) usado por
    /// FirebaseBackend para convertir payloads de Cloud Functions
    /// (Dictionary&lt;string, object&gt;) a DTOs C# vía JsonUtility.
    /// Parsea: null, bool, number (double), string, array (List&lt;object&gt;),
    /// object (Dictionary&lt;string, object&gt;). Serializa los mismos tipos.
    /// </summary>
    public static class MiniJson
    {
        // ------------------------------------------------------------------
        // Parse
        // ------------------------------------------------------------------
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int pos = 0;
            SkipWhitespace(json, ref pos);
            var value = ParseValue(json, ref pos);
            SkipWhitespace(json, ref pos);
            return value;
        }

        private static object ParseValue(string s, ref int pos)
        {
            if (pos >= s.Length) throw new FormatException("JSON unexpected end");
            char c = s[pos];
            switch (c)
            {
                case '{': return ParseObject(s, ref pos);
                case '[': return ParseArray(s, ref pos);
                case '"': return ParseString(s, ref pos);
                case 't': Expect(s, ref pos, "true"); return true;
                case 'f': Expect(s, ref pos, "false"); return false;
                case 'n': Expect(s, ref pos, "null"); return null;
                default: return ParseNumber(s, ref pos);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int pos)
        {
            var dict = new Dictionary<string, object>();
            pos++; // '{'
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == '}') { pos++; return dict; }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                string key = ParseString(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != ':') throw new FormatException("expected ':'");
                pos++;
                dict[key] = ParseValue(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length) throw new FormatException("unterminated object");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == '}') { pos++; return dict; }
                throw new FormatException("expected ',' or '}'");
            }
        }

        private static List<object> ParseArray(string s, ref int pos)
        {
            var list = new List<object>();
            pos++; // '['
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == ']') { pos++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref pos));
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length) throw new FormatException("unterminated array");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == ']') { pos++; return list; }
                throw new FormatException("expected ',' or ']'");
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            if (s[pos] != '"') throw new FormatException("expected string");
            pos++;
            var sb = new StringBuilder();
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (pos >= s.Length) break;
                    char e = s[pos++];
                    switch (e)
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
                            if (pos + 4 > s.Length) throw new FormatException("bad \\u escape");
                            sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber));
                            pos += 4;
                            break;
                        default: throw new FormatException("bad escape \\" + e);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            throw new FormatException("unterminated string");
        }

        private static double ParseNumber(string s, ref int pos)
        {
            int start = pos;
            if (pos < s.Length && (s[pos] == '-' || s[pos] == '+')) pos++;
            while (pos < s.Length && char.IsDigit(s[pos])) pos++;
            if (pos < s.Length && s[pos] == '.')
            {
                pos++;
                while (pos < s.Length && char.IsDigit(s[pos])) pos++;
            }
            if (pos < s.Length && (s[pos] == 'e' || s[pos] == 'E'))
            {
                pos++;
                if (pos < s.Length && (s[pos] == '-' || s[pos] == '+')) pos++;
                while (pos < s.Length && char.IsDigit(s[pos])) pos++;
            }
            string token = s.Substring(start, pos - start);
            double value;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new FormatException("invalid number '" + token + "'");
            }
            return value;
        }

        private static void Expect(string s, ref int pos, string literal)
        {
            if (pos + literal.Length > s.Length ||
                string.CompareOrdinal(s, pos, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("expected '" + literal + "'");
            }
            pos += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
        }

        // ------------------------------------------------------------------
        // Serialize (soporta los tipos que produce Parse + primitivas .NET)
        // ------------------------------------------------------------------
        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            if (value is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (value is string str) { WriteString(sb, str); return; }
            if (value is IDictionary<string, object> dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key);
                    sb.Append(':');
                    WriteValue(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            if (value is IEnumerable enumerable && !(value is string))
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteValue(sb, item);
                }
                sb.Append(']');
                return;
            }
            if (value is double d) { sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (value is float f) { sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (value is int || value is long || value is short || value is byte)
            {
                sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                return;
            }
            WriteString(sb, value.ToString());
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
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
