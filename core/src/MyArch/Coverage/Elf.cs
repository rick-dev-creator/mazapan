using System.Buffers.Binary;
using System.Text;

namespace MyArch.Coverage;

/// <summary>
/// The little of Go's debug/elf that coverage uses: whether a file opens as
/// ELF (elf.Open), and the libraries it needs (ImportedLibraries: DT_NEEDED
/// in the SHT_DYNAMIC section, named through the string table it links to).
/// </summary>
internal sealed class Elf
{
    const int ShtDynamic = 6;
    const int DtNeeded = 1;

    readonly FileStream file;
    readonly bool is64;
    readonly bool big;
    readonly List<(uint Type, ulong Offset, ulong Size, uint Link)> sections = [];

    Elf(FileStream file, bool is64, bool big)
    {
        this.file = file;
        this.is64 = is64;
        this.big = big;
    }

    /// <summary>
    /// Open reads the ELF header and the section headers, and refuses what Go's
    /// elf.NewFile refuses: another magic, class, byte order or version, a
    /// bad section header table or section name. Null when it isn't ELF.
    /// </summary>
    internal static Elf? Open(string path)
    {
        FileStream f;
        try
        {
            f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        try
        {
            var ident = new byte[16];
            if (f.Read(ident, 0, 16) != 16 || ident[0] != 0x7f || ident[1] != 'E' || ident[2] != 'L' || ident[3] != 'F')
                throw new InvalidDataException("bad magic number");
            if (ident[4] is not (1 or 2)) throw new InvalidDataException("unknown ELF class");
            if (ident[5] is not (1 or 2)) throw new InvalidDataException("unknown ELF data encoding");
            if (ident[6] != 1) throw new InvalidDataException("unknown ELF version");
            var e = new Elf(f, ident[4] == 2, ident[5] == 2);
            e.ReadSections();
            return e;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or OverflowException)
        {
            f.Dispose();
            return null;
        }
    }

    internal void Close() => file.Dispose();

    byte[] ReadAt(ulong offset, ulong size, bool exact = true)
    {
        if (size > int.MaxValue) throw new InvalidDataException("section too big");
        var b = new byte[size];
        file.Position = checked((long)offset);
        var n = 0;
        while (n < b.Length)
        {
            var r = file.Read(b, n, b.Length - n);
            if (r == 0) break;
            n += r;
        }
        if (n < b.Length)
        {
            if (exact) throw new InvalidDataException("unexpected EOF");
            Array.Resize(ref b, n); // a section's data: Go's io.ReadAll stops at the end of the file
        }
        return b;
    }

    ushort U16(byte[] b, int o) => big ? BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(o)) : BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o));
    uint U32(byte[] b, int o) => big ? BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o)) : BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    ulong U64(byte[] b, int o) => big ? BinaryPrimitives.ReadUInt64BigEndian(b.AsSpan(o)) : BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o));

    void ReadSections()
    {
        var hdr = ReadAt(0, is64 ? 64UL : 52UL);
        if (U32(hdr, 20) != 1) throw new InvalidDataException("mismatched ELF version");
        ulong shoff;
        int shentsize, shnum, shstrndx;
        if (is64)
        {
            shoff = U64(hdr, 40);
            shentsize = U16(hdr, 58);
            shnum = U16(hdr, 60);
            shstrndx = U16(hdr, 62);
        }
        else
        {
            shoff = U32(hdr, 32);
            shentsize = U16(hdr, 46);
            shnum = U16(hdr, 48);
            shstrndx = U16(hdr, 50);
        }
        // Program headers are read too, as Go does: a table past the end of
        // the file makes it not ELF.
        var (phoff, phentsize, phnum) = is64
            ? (U64(hdr, 32), (int)U16(hdr, 54), (int)U16(hdr, 56))
            : (U32(hdr, 28), (int)U16(hdr, 42), (int)U16(hdr, 44));
        if (phnum > 0)
        {
            if (phentsize < (is64 ? 56 : 32)) throw new InvalidDataException("invalid ELF phentsize");
            ReadAt(phoff, checked((ulong)phnum * (ulong)phentsize));
        }
        var want = is64 ? 64 : 40;
        // Extended numbering: more sections than fit in e_shnum live in section 0.
        if (shnum == 0 && shoff != 0)
        {
            if (shentsize < want) throw new InvalidDataException("invalid ELF shentsize");
            var s0 = ReadAt(shoff, (ulong)shentsize);
            var n = is64 ? U64(s0, 32) : U32(s0, 20);
            if (n > int.MaxValue) throw new InvalidDataException("invalid ELF shnum");
            shnum = (int)n;
            if (shstrndx == 0xffff) shstrndx = (int)U32(s0, is64 ? 40 : 24);
        }
        if (shnum > 0 && shentsize < want) throw new InvalidDataException("invalid ELF shentsize");
        if (shnum > 0 && shoff == 0) throw new InvalidDataException("invalid ELF shnum for shoff=0");
        if (shnum > 0 && shstrndx >= shnum) throw new InvalidDataException("invalid ELF shstrndx");
        if (shnum == 0) return;
        var table = ReadAt(shoff, checked((ulong)shnum * (ulong)shentsize));
        var names = new List<uint>();
        for (var i = 0; i < shnum; i++)
        {
            var o = i * shentsize;
            names.Add(U32(table, o));
            if (is64)
                sections.Add((U32(table, o + 4), U64(table, o + 24), U64(table, o + 32), U32(table, o + 40)));
            else
                sections.Add((U32(table, o + 4), U32(table, o + 16), U32(table, o + 20), U32(table, o + 24)));
        }
        // Every section's name has to be in the section name table.
        if (sections[shstrndx].Type != 3) throw new InvalidDataException("invalid ELF section name string table type");
        var shstr = SectionData(shstrndx);
        foreach (var n in names)
            if (GetString(shstr, n) == null) throw new InvalidDataException("bad section name index");
    }

    byte[] SectionData(int i)
    {
        var s = sections[i];
        if (s.Type == 8) return new byte[s.Size > int.MaxValue ? 0 : s.Size]; // SHT_NOBITS: zeros
        return ReadAt(s.Offset, s.Size, exact: false);
    }

    static string? GetString(byte[] b, ulong start)
    {
        if (start >= (ulong)b.Length) return null;
        var end = Array.IndexOf(b, (byte)0, (int)start);
        if (end < 0) return null;
        return Encoding.UTF8.GetString(b, (int)start, end - (int)start);
    }

    /// <summary>
    /// ImportedLibraries: the DT_NEEDED strings. Not dynamic: none. Every
    /// entry is read (Go doesn't stop at DT_NULL). Null on a malformed
    /// dynamic section, which Go returns as an error.
    /// </summary>
    internal List<string>? ImportedLibraries()
    {
        try
        {
            var ds = sections.FindIndex(s => s.Type == ShtDynamic);
            if (ds < 0) return [];
            var d = SectionData(ds);
            var size = is64 ? 16 : 8;
            if (d.Length % size != 0) return null;
            var link = sections[ds].Link;
            if (link <= 0 || link >= sections.Count) return null;
            var str = SectionData((int)link);
            var all = new List<string>();
            for (var o = 0; o < d.Length; o += size)
            {
                ulong tag, val;
                if (is64)
                {
                    tag = U64(d, o);
                    val = U64(d, o + 8);
                }
                else
                {
                    tag = U32(d, o);
                    val = U32(d, o + 4);
                }
                if (tag == DtNeeded && GetString(str, val) is { } s) all.Add(s);
            }
            return all;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or OverflowException)
        {
            return null;
        }
    }
}
