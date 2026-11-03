// A small JSON reader and writer for the creator tool (Phase 11 step 4), outside Unity. It binds the same shapes Unity's
// JsonUtility does: public fields of classes, arrays and List<T>, enums by name or number, nested objects; missing
// fields keep their defaults. Not part of the game.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace UtsavNights.Creator
{
    public static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            object value = Value(text, ref i);
            Skip(text, ref i);
            if (i != text.Length)
            {
                throw new FormatException($"unexpected text at {i}");
            }

            return value;
        }

        public static T Read<T>(string text) where T : new() => (T)Bind(Parse(text), typeof(T));

        public static object Bind(object node, Type type)
        {
            if (node == null)
            {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            if (type == typeof(string))
            {
                return Convert.ToString(node, CultureInfo.InvariantCulture);
            }

            if (type == typeof(bool))
            {
                return node is bool b ? b : Convert.ToDouble(node, CultureInfo.InvariantCulture) != 0;
            }

            if (type.IsEnum)
            {
                return node is string s ? Enum.Parse(type, s, true) : Enum.ToObject(type, Convert.ToInt32(node, CultureInfo.InvariantCulture));
            }

            if (type.IsPrimitive || type == typeof(decimal))
            {
                return Convert.ChangeType(node is bool bb ? (bb ? 1 : 0) : node, type, CultureInfo.InvariantCulture);
            }

            if (type.IsArray)
            {
                var list = (List<object>)node;
                Array array = Array.CreateInstance(type.GetElementType(), list.Count);
                for (int k = 0; k < list.Count; k++)
                {
                    array.SetValue(Bind(list[k], type.GetElementType()), k);
                }

                return array;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(type);
                foreach (object item in (List<object>)node)
                {
                    list.Add(Bind(item, type.GetGenericArguments()[0]));
                }

                return list;
            }

            var obj = (Dictionary<string, object>)node;
            object instance = Activator.CreateInstance(type);
            foreach (FieldInfo f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (obj.TryGetValue(f.Name, out object v) && !f.IsInitOnly)
                {
                    f.SetValue(instance, Bind(v, f.FieldType));
                }
            }

            return instance;
        }

        /// <summary>Writes an object (public fields), a parsed node or a primitive, indented by four like JsonUtility's pretty print.</summary>
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object v, int depth)
        {
            switch (v)
            {
                case null:
                    sb.Append("null");
                    return;
                case string s:
                    WriteString(sb, s);
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case Enum e:
                    sb.Append(Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                    return;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case IDictionary dict:
                    Open(sb, '{', dict.Count, depth, i =>
                    {
                        int k = 0;
                        foreach (DictionaryEntry entry in dict)
                        {
                            if (k++ == i)
                            {
                                WriteString(sb, (string)entry.Key);
                                sb.Append(": ");
                                WriteValue(sb, entry.Value, depth + 1);
                                return;
                            }
                        }
                    });
                    return;
                case IList list:
                    Open(sb, '[', list.Count, depth, i => WriteValue(sb, list[i], depth + 1));
                    return;
            }

            if (v.GetType().IsPrimitive)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                return;
            }

            FieldInfo[] fields = v.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            Open(sb, '{', fields.Length, depth, i =>
            {
                WriteString(sb, fields[i].Name);
                sb.Append(": ");
                WriteValue(sb, fields[i].GetValue(v), depth + 1);
            });
        }

        private static void Open(StringBuilder sb, char bracket, int count, int depth, Action<int> item)
        {
            sb.Append(bracket);
            if (count == 0)
            {
                sb.Append(bracket == '{' ? '}' : ']');
                return;
            }

            for (int i = 0; i < count; i++)
            {
                sb.Append('\n').Append(' ', (depth + 1) * 4);
                item(i);
                if (i < count - 1)
                {
                    sb.Append(',');
                }
            }

            sb.Append('\n').Append(' ', depth * 4).Append(bracket == '{' ? '}' : ']');
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
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
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

        private static object Value(string t, ref int i)
        {
            Skip(t, ref i);
            if (i >= t.Length)
            {
                throw new FormatException("unexpected end");
            }

            char c = t[i];
            if (c == '{')
            {
                var obj = new Dictionary<string, object>();
                i++;
                Skip(t, ref i);
                if (t[i] == '}')
                {
                    i++;
                    return obj;
                }

                while (true)
                {
                    Skip(t, ref i);
                    string key = (string)Value(t, ref i);
                    Skip(t, ref i);
                    Expect(t, ref i, ':');
                    obj[key] = Value(t, ref i);
                    Skip(t, ref i);
                    if (t[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    Expect(t, ref i, '}');
                    return obj;
                }
            }

            if (c == '[')
            {
                var list = new List<object>();
                i++;
                Skip(t, ref i);
                if (t[i] == ']')
                {
                    i++;
                    return list;
                }

                while (true)
                {
                    list.Add(Value(t, ref i));
                    Skip(t, ref i);
                    if (t[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    Expect(t, ref i, ']');
                    return list;
                }
            }

            if (c == '"')
            {
                var sb = new StringBuilder();
                i++;
                while (t[i] != '"')
                {
                    if (t[i] == '\\')
                    {
                        i++;
                        char e = t[i];
                        if (e == 'u')
                        {
                            sb.Append((char)int.Parse(t.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                        }
                        else
                        {
                            sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                        }
                    }
                    else
                    {
                        sb.Append(t[i]);
                    }

                    i++;
                }

                i++;
                return sb.ToString();
            }

            if (t.Substring(i).StartsWith("true", StringComparison.Ordinal))
            {
                i += 4;
                return true;
            }

            if (t.Substring(i).StartsWith("false", StringComparison.Ordinal))
            {
                i += 5;
                return false;
            }

            if (t.Substring(i).StartsWith("null", StringComparison.Ordinal))
            {
                i += 4;
                return null;
            }

            int start = i;
            while (i < t.Length && "+-0123456789.eE".IndexOf(t[i]) >= 0)
            {
                i++;
            }

            if (start == i)
            {
                throw new FormatException($"unexpected '{c}' at {i}");
            }

            return double.Parse(t.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void Skip(string t, ref int i)
        {
            while (i < t.Length && char.IsWhiteSpace(t[i]))
            {
                i++;
            }
        }

        private static void Expect(string t, ref int i, char c)
        {
            if (i >= t.Length || t[i] != c)
            {
                throw new FormatException($"expected '{c}' at {i}");
            }

            i++;
        }
    }
}
