using MyArch.Util;

namespace MyArch.Cli;

/// <summary>
/// Flags as Go's flag package took them: -name or --name, -name=value or
/// -name value for strings, stopping at the first argument that isn't a
/// flag. An unknown flag prints the command's flags and exits 2.
/// </summary>
sealed class Flags(string command)
{
    readonly Dictionary<string, (string Help, bool IsBool)> known = [];
    readonly Dictionary<string, string> values = [];

    public List<string> Rest { get; private set; } = [];

    public Flags Bool(string name, string help)
    {
        known[name] = (help, true);
        return this;
    }

    public Flags String(string name, string help)
    {
        known[name] = (help, false);
        return this;
    }

    public bool IsSet(string name) => values.TryGetValue(name, out var v) && v == "true";

    public string Get(string name) => values.GetValueOrDefault(name, "");

    public Flags Parse(IReadOnlyList<string> args)
    {
        var i = 0;
        for (; i < args.Count; i++)
        {
            var a = args[i];
            if (a == "--")
            {
                i++;
                break;
            }
            if (a.Length < 2 || a[0] != '-') break;
            var dashes = a[1] == '-' ? 2 : 1;
            var name = a[dashes..];
            if (name == "" || name[0] == '-' || name[0] == '=')
            {
                Console.Error.WriteLine($"bad flag syntax: {a}");
                Usage(2);
            }
            string? value = null;
            var eq = name.IndexOf('=');
            if (eq >= 0)
            {
                value = name[(eq + 1)..];
                name = name[..eq];
            }
            if (name is "h" or "help") Usage(0);
            if (!known.TryGetValue(name, out var f))
            {
                Console.Error.WriteLine($"flag provided but not defined: -{name}");
                Usage(2);
            }
            if (f.IsBool)
            {
                // strconv.ParseBool: -y=0 is no, as with Go's flag package.
                values[name] = value switch
                {
                    null or "1" or "t" or "T" or "TRUE" or "true" or "True" => "true",
                    "0" or "f" or "F" or "FALSE" or "false" or "False" => "false",
                    _ => Invalid(name, value),
                };
                continue;
            }
            if (value == null)
            {
                if (i + 1 >= args.Count)
                {
                    Console.Error.WriteLine($"flag needs an argument: -{name}");
                    Usage(2);
                }
                value = args[++i];
            }
            values[name] = value;
        }
        Rest = args.Skip(i).ToList();
        return this;
    }

    string Invalid(string name, string value)
    {
        Console.Error.WriteLine($"invalid boolean value \"{value}\" for -{name}: parse error");
        Usage(2);
        return "";
    }

    void Usage(int code)
    {
        var w = code == 0 ? Console.Out : Console.Error;
        w.WriteLine($"Usage of {command}:");
        foreach (var (name, (help, isBool)) in known.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            w.WriteLine(isBool ? $"  -{name}" : $"  -{name} string");
            w.WriteLine($"    \t{help}");
        }
        Environment.Exit(code);
    }
}
