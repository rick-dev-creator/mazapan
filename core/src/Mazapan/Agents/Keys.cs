using System.Diagnostics;
using Mazapan.Util;

namespace Mazapan.Agents;

/// <summary>A provider whose API key the keyring can keep: the variable agents read it from (none: an admin key, for its spend only).</summary>
public sealed record Provider(string Id, string Name, string Env, bool Admin = false);

/// <summary>
/// API keys in the keyring (the Secret Service: GNOME Keyring here, opened
/// with the login), never in a plain file. A key goes in through stdin,
/// never a command's arguments; it comes out only into the environment of
/// an agent launched with `mazapan agents run`, and to its own provider to
/// read the spend. Nothing prints one.
/// </summary>
public static class Keys
{
    public static readonly Provider[] Providers =
    [
        new("anthropic", "Anthropic", "ANTHROPIC_API_KEY"),
        new("openai", "OpenAI", "OPENAI_API_KEY"),
        new("openrouter", "OpenRouter", "OPENROUTER_API_KEY"),
        new("google", "Google Gemini", "GEMINI_API_KEY"),
        new("groq", "Groq", "GROQ_API_KEY"),
        new("mistral", "Mistral", "MISTRAL_API_KEY"),
        new("deepseek", "DeepSeek", "DEEPSEEK_API_KEY"),
        new("xai", "xAI", "XAI_API_KEY"),
        // The organization's spend, by day (Anthropic's and OpenAI's own reports want an admin key).
        new("anthropic-admin", "Anthropic (admin)", "", Admin: true),
        new("openai-admin", "OpenAI (admin)", "", Admin: true),
    ];

    public static Provider Of(string id) => Providers.FirstOrDefault(p => p.Id == id)
        ?? throw new MazapanException($"no provider \"{id}\": one of {string.Join(", ", Providers.Select(p => p.Id))}");

    /// <summary>
    /// Agents that sign in themselves and would take an API key over their
    /// subscription (Claude Code with ANTHROPIC_API_KEY, Codex with
    /// OPENAI_API_KEY): given none.
    /// </summary>
    public static bool Takes(string agent) => agent is not ("claude" or "codex");

    static string[] Attributes(string id) => ["service", "mazapan-agents", "provider", id];

    static (int Code, string Out) SecretTool(string[] args, string? input = null)
    {
        var psi = new ProcessStartInfo("secret-tool") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = input != null };
        foreach (var a in args) psi.ArgumentList.Add(a);
        Process p;
        try { p = Process.Start(psi)!; }
        catch (System.ComponentModel.Win32Exception) { throw new MazapanException("secret-tool isn't installed (libsecret)"); }
        using (p)
        {
            if (input != null)
            {
                p.StandardInput.Write(input);
                p.StandardInput.Close();
            }
            var out_ = p.StandardOutput.ReadToEndAsync();
            var err = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(15000))
            {
                try { p.Kill(); } catch (InvalidOperationException) { }
                throw new MazapanException("the keyring didn't answer (locked?)");
            }
            return (p.ExitCode, out_.GetAwaiter().GetResult());
        }
    }

    public static void Set(string id, string key)
    {
        var p = Of(id);
        key = key.Trim();
        if (key.Length < 8 || key.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) throw new MazapanException("that doesn't look like an API key");
        // ASCII: secret-tool turns a label down in a session without a UTF-8 locale.
        var (code, _) = SecretTool(["store", $"--label=Mazapan: {p.Name} API key", .. Attributes(id)], key);
        if (code != 0) throw new MazapanException("the keyring didn't keep it (locked?)");
    }

    /// <summary>The key, or null.</summary>
    public static string? Get(string id)
    {
        var (code, out_) = SecretTool(["lookup", .. Attributes(id)]);
        return code == 0 && out_.Trim() is { Length: > 0 } k ? k : null;
    }

    public static bool Remove(string id)
    {
        Of(id);
        return Get(id) != null && SecretTool(["clear", .. Attributes(id)]).Code == 0;
    }

    /// <summary>The providers that have a key in the keyring.</summary>
    public static List<Provider> Kept()
    {
        try { return Providers.Where(p => Get(p.Id) != null).ToList(); }
        catch (MazapanException) { return []; }
    }

    /// <summary>For an agent: each key it can take, as its variable (one already set is left).</summary>
    public static Dictionary<string, string> Environment(string agent)
    {
        var out_ = new Dictionary<string, string>();
        if (!Takes(agent)) return out_;
        try
        {
            foreach (var p in Providers.Where(p => !p.Admin))
                if (System.Environment.GetEnvironmentVariable(p.Env) is not { Length: > 0 } && Get(p.Id) is { } k)
                    out_[p.Env] = k;
        }
        catch (MazapanException) { } // no keyring (secret-tool missing, locked): the agent's own setup
        return out_;
    }
}
