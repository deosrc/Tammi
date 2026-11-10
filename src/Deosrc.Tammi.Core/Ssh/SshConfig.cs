namespace Deosrc.Tammi.Core.Ssh;

/// <summary>
/// The user's SSH client config (<c>~/.ssh/config</c>), as far as it decides which keys SSH offers: the
/// <c>IdentityFile</c> lines, and the <c>Host</c> entries they're in.
/// </summary>
public sealed class SshConfig
{
    private readonly List<string> _lines;
    private readonly string _homeDirectory;
    private readonly List<Entry> _entries = [];

    private SshConfig(string text, string homeDirectory)
    {
        _homeDirectory = homeDirectory;
        _lines = [.. text.Split('\n').Select(line => line.TrimEnd('\r'))];
        if (_lines.Count > 0 && _lines[^1].Length == 0)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }

        NewLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        Parse();
    }

    /// <summary>
    /// The line ending the file uses, or <c>\n</c> if it's empty.
    /// </summary>
    public string NewLine { get; }

    /// <summary>
    /// Reads config text. <c>~</c> in <c>IdentityFile</c> paths and relative paths mean <paramref name="homeDirectory"/>.
    /// </summary>
    public static SshConfig Parse(string? text, string homeDirectory) => new(text ?? "", homeDirectory);

    /// <summary>
    /// Whether any <c>IdentityFile</c> in the config names the private key file, under any <c>Host</c>.
    /// </summary>
    public bool NamesKey(string privateKeyPath) => _entries.Any(entry => entry.Names(this, privateKeyPath));

    /// <summary>
    /// Whether an entry that applies to the host, or to every host, names the private key file.
    /// </summary>
    public bool NamesKeyForHost(string privateKeyPath, string host) =>
        _entries.Any(entry => entry.Applies(host) && entry.Names(this, privateKeyPath));

    /// <summary>
    /// Returns the config text with a <c>Host</c> entry for each host added, naming the key and setting
    /// <c>IdentitiesOnly</c>. SSH uses the first value it finds for each option, so the entries go before the first
    /// <c>Host *</c> entry (or at the end if there is none). Existing content is kept as it is.
    /// </summary>
    /// <param name="privateKeyPath">The private key file.</param>
    /// <param name="hosts">The hosts to add entries for.</param>
    /// <param name="identityFile">How the entry names the key, e.g. <c>~/.ssh/github</c>.</param>
    public string AddHostEntries(IEnumerable<string> hosts, string identityFile)
    {
        List<string> block = [];
        foreach (string host in hosts)
        {
            block.Add("Host " + host);
            block.Add("\tIdentityFile " + (identityFile.Contains(' ', StringComparison.Ordinal) ? $"\"{identityFile}\"" : identityFile));
            block.Add("\tIdentitiesOnly yes");
            block.Add("");
        }

        List<string> lines = [.. _lines];
        int insertAt = _entries.FirstOrDefault(entry => entry.IsHostWildcard)?.LineIndex ?? lines.Count;
        if (insertAt == lines.Count)
        {
            // Appended: separate it from what's above.
            if (lines.Count > 0 && lines[^1].Trim().Length > 0)
            {
                block.Insert(0, "");
            }

            if (block.Count > 0 && block[^1].Length == 0)
            {
                block.RemoveAt(block.Count - 1);
            }
        }
        else
        {
            // Inserted above a wildcard entry: pull it above that entry's comments too.
            while (insertAt > 0 && lines[insertAt - 1].TrimStart().StartsWith('#'))
            {
                insertAt--;
            }
        }

        lines.InsertRange(insertAt, block);
        return string.Join(NewLine, lines) + NewLine;
    }

    private void Parse()
    {
        // Lines before the first Host or Match apply to every host.
        var current = new Entry(-1, ["*"], isHostWildcard: false);
        _entries.Add(current);
        for (int index = 0; index < _lines.Count; index++)
        {
            string[] words = Tokenize(_lines[index]);
            if (words.Length == 0)
            {
                continue;
            }

            switch (words[0].ToUpperInvariant())
            {
                case "HOST":
                    current = new Entry(index, words[1..], isHostWildcard: words[1..].Contains("*"));
                    _entries.Add(current);
                    break;
                case "MATCH":
                    // Its conditions aren't evaluated, so it's treated as matching nothing in particular.
                    current = new Entry(index, [], isHostWildcard: false);
                    _entries.Add(current);
                    break;
                case "IDENTITYFILE" when words.Length > 1:
                    current.IdentityFiles.Add(words[1]);
                    break;
            }
        }
    }

    /// <summary>
    /// Splits a line into words: <c>Keyword value</c> or <c>Keyword=value</c>, with quotes, ignoring comments.
    /// </summary>
    private static string[] Tokenize(string line)
    {
        List<string> words = [];
        var word = new System.Text.StringBuilder();
        bool quoted = false;
        bool any = false;
        foreach (char c in line.Trim())
        {
            if (c == '"')
            {
                quoted = !quoted;
                any = true;
            }
            else if (!quoted && c == '#' && !any && word.Length == 0)
            {
                break;
            }
            else if (!quoted && (char.IsWhiteSpace(c) || (c == '=' && words.Count == 0)))
            {
                if (word.Length > 0 || any)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    any = false;
                }
            }
            else
            {
                word.Append(c);
            }
        }

        if (word.Length > 0 || any)
        {
            words.Add(word.ToString());
        }

        return [.. words];
    }

    private string Resolve(string path)
    {
        path = path.Replace("%d", _homeDirectory, StringComparison.Ordinal)
            .Replace("${HOME}", _homeDirectory, StringComparison.Ordinal);
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            path = _homeDirectory + path[1..];
        }

        // SSH treats relative paths as relative to the home folder.
        return Path.GetFullPath(path, _homeDirectory);
    }

    private sealed class Entry(int lineIndex, string[] patterns, bool isHostWildcard)
    {
        public int LineIndex { get; } = lineIndex;

        public bool IsHostWildcard { get; } = isHostWildcard;

        public List<string> IdentityFiles { get; } = [];

        public bool Names(SshConfig config, string privateKeyPath)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            string expected = Path.GetFullPath(privateKeyPath);
            return IdentityFiles.Any(file => string.Equals(config.Resolve(file), expected, comparison));
        }

        public bool Applies(string host)
        {
            bool matched = false;
            foreach (string pattern in patterns)
            {
                if (pattern.StartsWith('!'))
                {
                    if (Matches(pattern[1..], host))
                    {
                        return false;
                    }
                }
                else if (Matches(pattern, host))
                {
                    matched = true;
                }
            }

            return matched;
        }

        private static bool Matches(string pattern, string host) =>
            System.Text.RegularExpressions.Regex.IsMatch(
                host,
                "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}
