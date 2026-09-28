namespace MyArch.Tests.Foundation;

/// <summary>A temporary folder, deleted after the test.</summary>
sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("myarch-test-").FullName;

    public string Write(string rel, string content)
    {
        var p = System.IO.Path.Join(Path, rel);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
        return p;
    }

    public string Dir(string rel)
    {
        var p = System.IO.Path.Join(Path, rel);
        Directory.CreateDirectory(p);
        return p;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch (IOException) { }
    }
}

static class Repo
{
    /// <summary>The repository's root: themes/ and plugins/ are there.</summary>
    public static string Root
    {
        get
        {
            var d = AppContext.BaseDirectory;
            while (d != null && !Directory.Exists(System.IO.Path.Join(d, "themes"))) d = System.IO.Path.GetDirectoryName(d);
            return d ?? throw new InvalidOperationException("repo root not found");
        }
    }

    public static string Themes => System.IO.Path.Join(Root, "themes");
}
