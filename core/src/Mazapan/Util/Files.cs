using System.Text;

namespace Mazapan.Util;

public static class Files
{
    /// <summary>UTF-8 without a byte order mark: how every file mazapan writes is encoded.</summary>
    public static readonly UTF8Encoding Utf8 = new(false);

    static readonly UTF8Encoding Strict = new(false, throwOnInvalidBytes: true);

    /// <summary>
    /// TryText decodes b as UTF-8, refusing bytes that aren't: a file mazapan
    /// merges into is written back with the person's bytes as they were, or
    /// not at all (a Latin-1 "Canción" must not become "Canci�n").
    /// </summary>
    public static bool TryText(byte[] b, out string text)
    {
        try
        {
            text = Strict.GetString(b);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = "";
            return false;
        }
    }

    /// <summary>
    /// WriteAtomic writes through a temporary file, so a reader never sees
    /// half a file. A file that's there keeps its permissions (a browser's
    /// 0600 Preferences); a new one is 0644.
    /// </summary>
    public static void WriteAtomic(string path, byte[] b)
    {
        var dir = Paths.Dir(path);
        CreateDirectory(dir);
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        try
        {
            if (File.Exists(path)) mode = File.GetUnixFileMode(path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            // A dangling symlink: replaced, as Go's rename did.
        }
        var tmp = Paths.Join(dir, "." + Paths.Base(path) + ".tmp-" + Guid.NewGuid().ToString("N")[..10]);
        try
        {
            using (var f = new FileStream(tmp, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            }))
            {
                f.Write(b);
            }
            File.SetUnixFileMode(tmp, mode);
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>MkdirAll(dir, 0o755): folders mazapan makes are 0755, whatever the umask.</summary>
    public static void CreateDirectory(string dir) =>
        Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

    public static void WriteAtomic(string path, string s) => WriteAtomic(path, Utf8.GetBytes(s));
}
