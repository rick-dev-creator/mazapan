using System.Text.Json;
using Mazapan.Util;

namespace Mazapan.Applying;

/// <summary>
/// SystemState remembers, for each system file mazapan wrote, the command
/// that makes it count (mkinitcpio -P after a MODULES drop-in): removing the
/// file, or undoing it, must run it too, when the plugin that had it is off
/// or gone and no longer says.
/// </summary>
public static class SystemState
{
    static string Path() => Paths.ExpandHome("~/.local/state/mazapan/system.json");

    public static Dictionary<string, string> Reloads()
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path()));
            return doc.RootElement.EnumerateObject()
                .Where(p => AsRoot.IsSystem(p.Name) && p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString()!);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [];
        }
    }

    public static void Save(Dictionary<string, string> reloads) =>
        Files.WriteAtomic(Path(), GoJson.Marshal(new SortedDictionary<string, object?>(
            reloads.ToDictionary(kv => kv.Key, kv => (object?)kv.Value), StringComparer.Ordinal)) + "\n");
}
