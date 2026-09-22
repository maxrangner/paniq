// Checks Assets/Paniq/Content/FireReactionScenario.asset against the code
// defaults in FireReactionScenarioData.
//
// This stands in for ScenarioAsset_MatchesTheCodeDefaults, which cannot run
// outside the editor because it loads the asset through AssetDatabase. Rather
// than skip the check, we read the saved YAML directly and compare it with the
// same values Unity would have serialized, found by reflection the way Unity's
// own serializer finds them: public instance fields, plus private ones marked
// [SerializeField], by name.
//
// The asset is a saved copy of the code defaults -- Editor/RewriteScenarioAsset.cs
// regenerates it -- so any difference means the copy is stale and the scene is
// running different content from the tests.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Paniq.Simulation;

internal static class ScenarioAssetCheck
{
    private const string AssetPath = "Assets/Paniq/Content/FireReactionScenario.asset";

    /// <summary>Compares the asset with the code defaults; returns the differences, empty when they agree.</summary>
    public static List<string> Run(string repositoryRoot, out int compared)
    {
        compared = 0;
        var differences = new List<string>();
        string path = Path.Combine(repositoryRoot, AssetPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            differences.Add("Missing " + AssetPath + ".");
            return differences;
        }

        Node saved;
        try
        {
            saved = Yaml.ParseBlock(File.ReadAllLines(path), "scenario");
        }
        catch (Exception parse)
        {
            differences.Add("Could not read " + AssetPath + ": " + parse.Message);
            return differences;
        }

        if (saved == null)
        {
            differences.Add("No 'scenario' block in " + AssetPath + ".");
            return differences;
        }

        int counted = 0;
        Compare(saved, new FireReactionScenarioData(), "scenario", differences, ref counted);
        compared = counted;
        return differences;
    }

    // ------------------------------------------------------------- comparing

    private static void Compare(Node saved, object fromCode, string path, List<string> differences, ref int compared)
    {
        if (fromCode == null)
        {
            return;
        }

        Type type = fromCode.GetType();

        if (type.IsArray)
        {
            var array = (Array)fromCode;
            if (saved.Items == null && saved.Value == "[]")
            {
                // Unity writes an empty array inline.
                if (array.Length != 0)
                {
                    differences.Add(path + ": the asset has no entries, the code has " + array.Length + ".");
                }

                return;
            }

            if (saved.Items == null)
            {
                differences.Add(path + ": the asset has no list here, but the code has " + array.Length + " entries.");
                return;
            }

            if (saved.Items.Count != array.Length)
            {
                differences.Add(path + ": the asset has " + saved.Items.Count +
                    " entries, the code has " + array.Length + ".");
                return;
            }

            for (int i = 0; i < array.Length; i++)
            {
                Compare(saved.Items[i], array.GetValue(i), path + "[" + i + "]", differences, ref compared);
            }

            return;
        }

        if (IsScalar(type))
        {
            compared++;
            string expected = Scalar(fromCode);
            string actual = saved.Value ?? "(a block)";
            if (!SameScalar(expected, actual))
            {
                differences.Add(path + ": asset has " + actual + ", code has " + expected + ".");
            }

            return;
        }

        foreach (FieldInfo field in SerializedFields(type))
        {
            Node child = saved.Child(field.Name);
            if (child == null)
            {
                differences.Add(path + "." + field.Name + ": missing from the asset.");
                continue;
            }

            Compare(child, field.GetValue(fromCode), path + "." + field.Name, differences, ref compared);
        }
    }

    /// <summary>
    /// The fields Unity's serializer would write: public instance fields, and
    /// private ones marked [SerializeField]. Static, const and readonly fields
    /// are not serialized.
    /// </summary>
    private static IEnumerable<FieldInfo> SerializedFields(Type type)
    {
        return type
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => !f.IsInitOnly)
            .Where(f => f.IsPublic || f.GetCustomAttributes()
                .Any(a => a.GetType().Name == "SerializeField"));
    }

    private static bool IsScalar(Type type)
    {
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
    }

    private static string Scalar(object value)
    {
        if (value is bool)
        {
            // Unity writes booleans as 1 and 0.
            return (bool)value ? "1" : "0";
        }

        if (value is Enum)
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>Unity may quote a string or leave it bare; either spelling means the same value.</summary>
    private static bool SameScalar(string expected, string actual)
    {
        return Unquote(expected) == Unquote(actual);
    }

    private static string Unquote(string text)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length >= 2 &&
            ((text[0] == '"' && text[text.Length - 1] == '"') ||
             (text[0] == '\'' && text[text.Length - 1] == '\'')))
        {
            return text.Substring(1, text.Length - 2);
        }

        return text;
    }

    // ---------------------------------------------------------------- nodes

    /// <summary>One value in the asset: a scalar, a map of named children, or a list.</summary>
    internal sealed class Node
    {
        public string Value;
        public List<KeyValuePair<string, Node>> Fields;
        public List<Node> Items;

        public Node Child(string name)
        {
            if (Fields == null)
            {
                return null;
            }

            foreach (KeyValuePair<string, Node> field in Fields)
            {
                if (field.Key == name)
                {
                    return field.Value;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Just enough YAML for what Unity writes: indented maps, "- " lists, and
    /// plain scalars. No anchors, tags, flow collections or multi-line strings,
    /// none of which appear in a serialized scenario.
    /// </summary>
    private static class Yaml
    {
        public static Node ParseBlock(string[] lines, string rootKey)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimEnd();
                if (trimmed.TrimStart() != rootKey + ":")
                {
                    continue;
                }

                int indent = Indent(trimmed);
                int at = i + 1;
                return ParseNode(lines, ref at, indent + 1);
            }

            return null;
        }

        /// <summary>Reads every line indented at least <paramref name="minimumIndent"/> into one node.</summary>
        private static Node ParseNode(string[] lines, ref int at, int minimumIndent)
        {
            var node = new Node();
            while (at < lines.Length)
            {
                string raw = lines[at].TrimEnd();
                if (raw.Length == 0)
                {
                    at++;
                    continue;
                }

                int indent = Indent(raw);
                if (indent < minimumIndent)
                {
                    break;
                }

                string content = raw.Substring(indent);
                if (content.StartsWith("- ", StringComparison.Ordinal) || content == "-")
                {
                    node.Items ??= new List<Node>();
                    node.Items.Add(ParseListItem(lines, ref at, indent));
                    continue;
                }

                at++;
                ReadField(node, lines, ref at, content, indent);
            }

            return node;
        }

        /// <summary>
        /// Every "- " entry at exactly this indent, and nothing else. Stopping
        /// at the first line that is not a dash keeps the key that follows the
        /// list -- which Unity writes at the same indent -- out of it.
        /// </summary>
        private static Node ParseList(string[] lines, ref int at, int indent)
        {
            var list = new Node { Items = new List<Node>() };
            while (at < lines.Length)
            {
                string raw = lines[at].TrimEnd();
                if (raw.Length == 0)
                {
                    at++;
                    continue;
                }

                if (Indent(raw) != indent || !raw.Substring(indent).StartsWith("-", StringComparison.Ordinal))
                {
                    break;
                }

                list.Items.Add(ParseListItem(lines, ref at, indent));
            }

            return list;
        }

        /// <summary>
        /// A list entry. Its first field sits on the dash line itself, and any
        /// further fields are indented to line up with it.
        /// </summary>
        private static Node ParseListItem(string[] lines, ref int at, int dashIndent)
        {
            string content = lines[at].TrimEnd().Substring(dashIndent);
            string first = content == "-" ? string.Empty : content.Substring(2);
            int fieldIndent = dashIndent + 2;
            at++;

            var item = new Node();
            if (first.Length > 0)
            {
                ReadField(item, lines, ref at, first, fieldIndent);
            }

            while (at < lines.Length)
            {
                string raw = lines[at].TrimEnd();
                if (raw.Length == 0)
                {
                    at++;
                    continue;
                }

                int indent = Indent(raw);
                if (indent < fieldIndent || raw.Substring(indent).StartsWith("- ", StringComparison.Ordinal))
                {
                    break;
                }

                string line = raw.Substring(indent);
                at++;
                ReadField(item, lines, ref at, line, indent);
            }

            return item;
        }

        /// <summary>
        /// Stores "key: value", or "key:" followed by an indented block. The
        /// cursor has already moved past the key's own line.
        /// </summary>
        private static void ReadField(Node into, string[] lines, ref int at, string line, int indent)
        {
            int colon = line.IndexOf(':');
            if (colon < 0)
            {
                return;
            }

            string key = line.Substring(0, colon).Trim();
            string value = line.Substring(colon + 1).Trim();
            into.Fields ??= new List<KeyValuePair<string, Node>>();

            if (value.Length > 0)
            {
                into.Fields.Add(new KeyValuePair<string, Node>(key, new Node { Value = value }));
                return;
            }

            // An empty value means a nested block, a list, or genuinely nothing.
            // YAML lets a list sit at the same indent as the key that owns it,
            // and that is how Unity writes arrays, so look for that first.
            int peek = at;
            while (peek < lines.Length && lines[peek].TrimEnd().Length == 0)
            {
                peek++;
            }

            bool listAtSameIndent = peek < lines.Length &&
                Indent(lines[peek].TrimEnd()) == indent &&
                lines[peek].TrimEnd().Substring(indent).StartsWith("-", StringComparison.Ordinal);

            Node child = listAtSameIndent
                ? ParseList(lines, ref at, indent)
                : ParseNode(lines, ref at, indent + 1);
            if (child.Fields == null && child.Items == null)
            {
                child.Value = string.Empty;
            }

            into.Fields.Add(new KeyValuePair<string, Node>(key, child));
        }

        private static int Indent(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ')
            {
                i++;
            }

            return i;
        }
    }
}
