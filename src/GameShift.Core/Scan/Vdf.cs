using System.Text;

namespace GameShift.Core;

static class Vdf
{
    public static Dictionary<string, object> Parse(string text)
    {
        var pos = 0;
        var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        ReadBody(text, ref pos, root, top: true);
        return root;
    }

    static void ReadBody(string t, ref int pos, Dictionary<string, object> into, bool top)
    {
        while (true)
        {
            var key = Next(t, ref pos);
            if (key is null) return;
            if (key.Kind == Token.Close)
            {
                if (top) continue;
                return;
            }
            if (key.Kind != Token.Text) continue;
            var value = Next(t, ref pos);
            if (value is null) return;
            if (value.Kind == Token.Text) into[key.Text] = value.Text;
            else if (value.Kind == Token.Open)
            {
                var child = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                ReadBody(t, ref pos, child, top: false);
                into[key.Text] = child;
            }
        }
    }

    enum Token { Text, Open, Close }

    sealed record Tok(Token Kind, string Text);

    static Tok? Next(string t, ref int pos)
    {
        while (pos < t.Length)
        {
            var c = t[pos];
            if (char.IsWhiteSpace(c)) { pos++; continue; }
            if (c == '/' && pos + 1 < t.Length && t[pos + 1] == '/')
            {
                while (pos < t.Length && t[pos] != '\n') pos++;
                continue;
            }
            if (c == '{') { pos++; return new Tok(Token.Open, ""); }
            if (c == '}') { pos++; return new Tok(Token.Close, ""); }
            if (c == '"') return new Tok(Token.Text, Quoted(t, ref pos));
            var start = pos;
            while (pos < t.Length && !char.IsWhiteSpace(t[pos]) && t[pos] is not ('{' or '}' or '"')) pos++;
            return new Tok(Token.Text, t[start..pos]);
        }
        return null;
    }

    static string Quoted(string t, ref int pos)
    {
        pos++;
        var sb = new StringBuilder();
        while (pos < t.Length && t[pos] != '"')
        {
            if (t[pos] == '\\' && pos + 1 < t.Length)
            {
                pos++;
                sb.Append(t[pos] switch { 'n' => '\n', 't' => '\t', var o => o });
            }
            else sb.Append(t[pos]);
            pos++;
        }
        pos++;
        return sb.ToString();
    }
}
