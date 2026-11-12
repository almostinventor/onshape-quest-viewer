using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// Tiny JSON reader: objects -> Dictionary&lt;string, object&gt;, arrays -> List&lt;object&gt;, strings, double, bool, null.
public static class MiniJson
{
    public static object Parse(string json)
    {
        int i = 0;
        var v = Value(json, ref i);
        return v;
    }

    static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
        char c = s[i];
        if (c == '{') return Obj(s, ref i);
        if (c == '[') return Arr(s, ref i);
        if (c == '"') return Str(s, ref i);
        if (Lit(s, ref i, "true")) return true;
        if (Lit(s, ref i, "false")) return false;
        if (Lit(s, ref i, "null")) return null;
        return Num(s, ref i);
    }

    static Dictionary<string, object> Obj(string s, ref int i)
    {
        var d = new Dictionary<string, object>();
        i++; Ws(s, ref i);
        if (s[i] == '}') { i++; return d; }
        while (true)
        {
            Ws(s, ref i);
            var key = Str(s, ref i);
            Ws(s, ref i);
            if (s[i++] != ':') throw new FormatException("Expected ':' at " + (i - 1));
            d[key] = Value(s, ref i);
            Ws(s, ref i);
            char c = s[i++];
            if (c == '}') return d;
            if (c != ',') throw new FormatException("Expected ',' or '}' at " + (i - 1));
        }
    }

    static List<object> Arr(string s, ref int i)
    {
        var l = new List<object>();
        i++; Ws(s, ref i);
        if (s[i] == ']') { i++; return l; }
        while (true)
        {
            l.Add(Value(s, ref i));
            Ws(s, ref i);
            char c = s[i++];
            if (c == ']') return l;
            if (c != ',') throw new FormatException("Expected ',' or ']' at " + (i - 1));
        }
    }

    static string Str(string s, ref int i)
    {
        if (s[i] != '"') throw new FormatException("Expected string at " + i);
        i++;
        var sb = new StringBuilder();
        while (true)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
    }

    static double Num(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        if (i == start) throw new FormatException("Unexpected character at " + i);
        return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    static bool Lit(string s, ref int i, string lit)
    {
        if (string.CompareOrdinal(s, i, lit, 0, lit.Length) != 0) return false;
        i += lit.Length;
        return true;
    }

    static void Ws(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    // Helpers for reading parsed values.
    public static Dictionary<string, object> AsObj(object o) => o as Dictionary<string, object>;
    public static List<object> AsList(object o) => o as List<object>;
    public static object Get(object o, string key) => AsObj(o) != null && AsObj(o).TryGetValue(key, out var v) ? v : null;
    public static double Num(object o, double fallback = double.NaN) => o is double d ? d : fallback;
}
