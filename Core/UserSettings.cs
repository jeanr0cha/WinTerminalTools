using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WinTerminalTools.Core;

/// <summary>Um comando executado. Só entradas com <see cref="DefinitionId"/> podem ser repetidas.</summary>
public sealed record HistoryEntry(DateTime When, string Display, int ExitCode, bool Cancelled, string? DefinitionId = null);

/// <summary>Preferências salvas entre execuções: favoritos e histórico recente.</summary>
public sealed class UserSettings
{
    public const int MaxHistory = 30;

    public List<string> Favorites { get; set; } = new();
    public List<HistoryEntry> History { get; set; } = new();

    /// <summary>Console quebra linhas longas (true) ou mantém tabelas alinhadas com rolagem horizontal (false).</summary>
    public bool ConsoleWrap { get; set; } = true;

    /// <summary>Adiciona ou remove dos favoritos; devolve true se ficou favorito.</summary>
    public bool ToggleFavorite(string definitionId)
    {
        if (Favorites.Remove(definitionId))
            return false;

        Favorites.Add(definitionId);
        return true;
    }

    /// <summary>Registra no topo do histórico, mantendo só os mais recentes.</summary>
    public void AddHistory(HistoryEntry entry)
    {
        History.Insert(0, entry);
        if (History.Count > MaxHistory)
            History.RemoveRange(MaxHistory, History.Count - MaxHistory);
    }
}

/// <summary>Lê e grava o settings.json. Arquivo ausente ou corrompido nunca impede o app de abrir.</summary>
public sealed class UserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinTerminalTools", "settings.json");

    public string FilePath { get; }

    public UserSettingsStore(string filePath) => FilePath = filePath;

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new UserSettings();

            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new UserSettings();
            settings.Favorites ??= new();
            settings.History ??= new();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new UserSettings();
        }
    }

    /// <summary>Grava; devolve false se não conseguiu (ex.: disco cheio, sem permissão).</summary>
    public bool Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, overwrite: true); // troca atômica: não deixa o arquivo pela metade
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
