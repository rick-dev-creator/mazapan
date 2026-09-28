namespace MyArch.Util;

public static class Paths
{
    public static string Home => Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } h
        ? h : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>ExpandHome turns a leading "~/" into the user's home directory.</summary>
    public static string ExpandHome(string p) => p.StartsWith("~/") ? Clean(Path.Join(Home, p[2..])) : p;

    /// <summary>~ for the home directory, as people write it.</summary>
    public static string Tilde(string p)
    {
        var home = Home;
        return p.StartsWith(home + "/") ? "~" + p[home.Length..] : p;
    }

    /// <summary>Go's filepath.Clean: no double slashes, no "." or "..".</summary>
    public static string Clean(string path)
    {
        if (path == "") return ".";
        var rooted = path.StartsWith('/');
        var parts = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..")
            {
                if (parts.Count > 0 && parts[^1] != "..") parts.RemoveAt(parts.Count - 1);
                else if (!rooted) parts.Add("..");
                continue;
            }
            parts.Add(part);
        }
        var s = string.Join('/', parts);
        return rooted ? "/" + s : s == "" ? "." : s;
    }

    /// <summary>Go's filepath.Dir.</summary>
    public static string Dir(string path)
    {
        var i = path.LastIndexOf('/');
        return Clean(i < 0 ? "." : path[..(i + 1)]);
    }

    /// <summary>Go's filepath.Base.</summary>
    public static string Base(string path)
    {
        if (path == "") return ".";
        path = path.TrimEnd('/');
        if (path == "") return "/";
        var i = path.LastIndexOf('/');
        return i < 0 ? path : path[(i + 1)..];
    }

    /// <summary>Go's filepath.Join: joined and cleaned.</summary>
    public static string Join(params string[] parts) =>
        Clean(string.Join('/', parts.Where(p => p != "")));

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "realpath")]
    static extern IntPtr RealPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path, IntPtr resolved);

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "free")]
    static extern void Free(IntPtr p);

    /// <summary>
    /// Real resolves every symlink in path, like Go's EvalSymlinks (the C
    /// library's realpath: loops and dangling links are null, not a crash);
    /// null when it doesn't exist.
    /// </summary>
    public static string? Real(string path)
    {
        if (path == "") return null;
        var p = RealPath(path, IntPtr.Zero);
        if (p == IntPtr.Zero) return null;
        try
        {
            return System.Runtime.InteropServices.Marshal.PtrToStringUTF8(p);
        }
        finally
        {
            Free(p);
        }
    }

    /// <summary>A path exists, following symlinks as Go's os.Stat does: a dangling link doesn't.</summary>
    public static bool StatExists(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (fi.LinkTarget != null)
            {
                var t = fi.ResolveLinkTarget(returnFinalTarget: true);
                return t != null && (File.Exists(t.FullName) || Directory.Exists(t.FullName));
            }
            return fi.Exists || Directory.Exists(path);
        }
        catch (IOException)
        {
            return false; // a symlink loop
        }
    }

    /// <summary>A path exists (file, folder, or a symlink, even a broken one).</summary>
    public static bool Exists(string path) =>
        File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget != null;
}
