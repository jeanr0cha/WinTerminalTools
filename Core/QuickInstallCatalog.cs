using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WinTerminalTools.Core;

public sealed record QuickInstallApp(string Id, string Name, bool Checked = false);

public sealed record QuickInstallGroup(string Title, IReadOnlyList<QuickInstallApp> Apps);

/// <summary>
/// Lista de apps da "Instalação Rápida Pós-Formatação". O usuário pode personalizar em
/// %APPDATA%\WinTerminalTools\apps.json; sem o arquivo, vale a lista padrão.
/// </summary>
public static class QuickInstallCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // Mantém acentos legíveis no arquivo que a pessoa vai editar.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string UserFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinTerminalTools", "apps.json");

    public static IReadOnlyList<QuickInstallGroup> Defaults { get; } = new QuickInstallGroup[]
    {
        new("🌐 Navegadores", new QuickInstallApp[]
        {
            new("Google.Chrome", "Google Chrome", true),
            new("Brave.Brave", "Brave Browser"),
            new("Mozilla.Firefox", "Mozilla Firefox"),
            new("Opera.OperaGX", "Opera GX"),
        }),
        new("🎮 Mídia & Mensagens", new QuickInstallApp[]
        {
            new("Spotify.Spotify", "Spotify", true),
            new("Discord.Discord", "Discord"),
            new("VideoLAN.VLC", "VLC Player", true),
            new("Valve.Steam", "Steam"),
            new("9NKSQGP7F2NH", "WhatsApp (Microsoft Store)"),
        }),
        new("💻 Ferramentas & Dev", new QuickInstallApp[]
        {
            new("Microsoft.VisualStudioCode", "VS Code", true),
            new("Git.Git", "Git for Windows", true),
            new("7zip.7zip", "7-Zip", true),
            new("Notepad++.Notepad++", "Notepad++"),
            new("Docker.DockerDesktop", "Docker Desktop"),
        }),
    };

    /// <summary>
    /// IDs que saíram do winget e o substituto atual. Listas salvas antes da troca são corrigidas ao carregar.
    /// (WhatsApp agora só existe pela Microsoft Store.)
    /// </summary>
    public static IReadOnlyDictionary<string, string> RenamedIds { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WhatsApp.WhatsApp"] = "9NKSQGP7F2NH",
            ["BraveSoftware.BraveBrowser"] = "Brave.Brave",
        };

    public static string Serialize(IReadOnlyList<QuickInstallGroup> groups) => JsonSerializer.Serialize(groups, JsonOptions);

    /// <summary>
    /// Lê e valida a lista. Lança <see cref="FormatException"/> com uma mensagem em português
    /// quando o JSON é inválido ou algum ID não é um ID de winget válido.
    /// </summary>
    public static IReadOnlyList<QuickInstallGroup> Parse(string json)
    {
        List<QuickInstallGroup>? groups;
        try
        {
            groups = JsonSerializer.Deserialize<List<QuickInstallGroup>>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"O arquivo não é um JSON válido (linha {ex.LineNumber + 1}).", ex);
        }

        if (groups == null || groups.Count == 0)
            throw new FormatException("A lista está vazia: inclua pelo menos um grupo com apps.");

        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.Title) || group.Apps == null || group.Apps.Count == 0)
                throw new FormatException("Todo grupo precisa de \"title\" e de pelo menos um app em \"apps\".");

            foreach (var app in group.Apps)
            {
                if (!InputValidator.IsValidWingetId(app.Id))
                    throw new FormatException($"ID de winget inválido: \"{app.Id}\" (grupo {group.Title}).");
                if (string.IsNullOrWhiteSpace(app.Name))
                    throw new FormatException($"O app \"{app.Id}\" está sem \"name\".");
            }
        }

        return groups
            .Select(g => g with
            {
                Apps = g.Apps.Select(a => RenamedIds.TryGetValue(a.Id, out var novo) ? a with { Id = novo } : a).ToList()
            })
            .ToList();
    }

    /// <summary>Carrega a lista do usuário, ou a padrão se o arquivo não existir.</summary>
    public static IReadOnlyList<QuickInstallGroup> Load(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : Defaults;

    /// <summary>Cria o arquivo com a lista padrão se ainda não existir (para a pessoa editar).</summary>
    public static void EnsureUserFile(string path)
    {
        if (File.Exists(path))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Serialize(Defaults));
    }
}
