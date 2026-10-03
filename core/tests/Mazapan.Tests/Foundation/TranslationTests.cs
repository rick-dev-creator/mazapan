using System.Text.RegularExpressions;
using Mazapan.Util;
using Tomlyn.Model;

namespace Mazapan.Tests.Foundation;

/// <summary>
/// Every translation says everything, and only what there is to say: the
/// languages mazapan is translated to (every locales/*.toml besides en) have
/// every key their plugin has (en.toml's, and the name, description and
/// settings es.toml gives), none it doesn't, and the same %1, %2 as English.
/// </summary>
public partial class TranslationTests
{
    [GeneratedRegex(@"%\d")] private static partial Regex Placeholder();

    static Dictionary<string, string> Keys(string path)
    {
        var t = Toml.ReadFile(path) ?? new TomlTable();
        return t.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
    }

    static IEnumerable<string> LocaleDirs() =>
        Directory.GetDirectories(Path.Join(Repo.Root, "plugins")).Concat(Directory.GetDirectories(Path.Join(Repo.Root, "community")))
            .Select(d => Path.Join(d, "locales")).Where(Directory.Exists);

    // The boot screen's text is drawn before anything but the initramfs is
    // there, by a renderer that knows only ASCII.
    [Fact]
    public void TheBootScreenSaysItInAscii()
    {
        foreach (var f in Directory.GetFiles(Path.Join(Repo.Root, "plugins", "theme-plymouth", "locales"), "*.toml"))
            Assert.True(Keys(f)["password"].All(c => c >= ' ' && c <= '~'), $"{f}: \"password\" isn't ASCII");
    }

    [Fact]
    public void EveryLanguageSaysEverything()
    {
        var langs = LocaleDirs().SelectMany(d => Directory.GetFiles(d, "*.toml")).Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(l => l != "en").Distinct().ToList();
        var problems = new List<string>();
        foreach (var dir in LocaleDirs())
        {
            var en = File.Exists(Path.Join(dir, "en.toml")) ? Keys(Path.Join(dir, "en.toml")) : [];
            var es = File.Exists(Path.Join(dir, "es.toml")) ? Keys(Path.Join(dir, "es.toml")) : [];
            var want = en.Keys.Union(es.Keys).Where(k => k != "self").ToHashSet();
            foreach (var lang in langs)
            {
                var f = Path.Join(dir, lang + ".toml");
                if (!File.Exists(f)) { problems.Add($"{dir}: no {lang}.toml"); continue; }
                var have = Keys(f);
                foreach (var k in want.Where(k => !have.ContainsKey(k))) problems.Add($"{f}: missing {k}");
                foreach (var k in have.Keys.Where(k => k != "self" && !want.Contains(k))) problems.Add($"{f}: {k} is no key of this plugin's");
                foreach (var (k, v) in have)
                    if (en.TryGetValue(k, out var src) &&
                        !Placeholder().Matches(src).Select(m => m.Value).Order().SequenceEqual(Placeholder().Matches(v).Select(m => m.Value).Order()))
                        problems.Add($"{f}: {k} doesn't keep {string.Join(" ", Placeholder().Matches(src).Select(m => m.Value))}");
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(40)));
    }

    [Fact]
    public void TheInstallerNamesEachLanguageInItsOwnWords()
    {
        foreach (var f in Directory.GetFiles(Path.Join(Repo.Root, "plugins", "installer", "locales"), "*.toml"))
            Assert.False(string.IsNullOrWhiteSpace(Keys(f).GetValueOrDefault("self")), f);
    }
}
