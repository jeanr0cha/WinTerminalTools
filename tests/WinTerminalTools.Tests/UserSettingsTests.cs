using System.IO;
using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

/// <summary>Favoritos e histórico. Usa só arquivos temporários (nunca o %APPDATA% real).</summary>
public class UserSettingsTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), $"wtt-{Guid.NewGuid():N}");
    private string Arquivo => Path.Combine(_pasta, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_pasta))
            Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    public void ToggleFavorite_AdicionaERemove()
    {
        var settings = new UserSettings();
        Assert.True(settings.ToggleFavorite("ipconfig"));
        Assert.Equal(new[] { "ipconfig" }, settings.Favorites);
        Assert.False(settings.ToggleFavorite("ipconfig"));
        Assert.Empty(settings.Favorites);
    }

    [Fact]
    public void AddHistory_MaisRecentePrimeiroELimitado()
    {
        var settings = new UserSettings();
        for (int i = 0; i < UserSettings.MaxHistory + 5; i++)
            settings.AddHistory(new HistoryEntry(DateTime.Now, $"cmd {i}", 0, false));

        Assert.Equal(UserSettings.MaxHistory, settings.History.Count);
        Assert.Equal($"cmd {UserSettings.MaxHistory + 4}", settings.History[0].Display);
    }

    [Fact]
    public void Store_GravaELeDeVolta()
    {
        var store = new UserSettingsStore(Arquivo);
        var settings = new UserSettings();
        settings.ToggleFavorite("netstat");
        settings.AddHistory(new HistoryEntry(new DateTime(2026, 11, 25, 10, 30, 0), "Diagnóstico", 1, false, "ipconfig-all"));

        Assert.True(store.Save(settings));
        var lido = store.Load();

        Assert.Equal(new[] { "netstat" }, lido.Favorites);
        Assert.Equal(settings.History, lido.History);
        Assert.False(File.Exists(Arquivo + ".tmp"));
    }

    [Fact]
    public void Store_SemArquivo_DevolvePreferenciasVazias()
    {
        var lido = new UserSettingsStore(Arquivo).Load();
        Assert.Empty(lido.Favorites);
        Assert.Empty(lido.History);
    }

    [Theory]
    [InlineData("{ corrompido")]
    [InlineData("null")]
    [InlineData("""{ "favorites": null, "history": null }""")]
    public void Store_ArquivoCorrompido_NaoImpedeOAppDeAbrir(string conteudo)
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Arquivo, conteudo);

        var lido = new UserSettingsStore(Arquivo).Load();
        Assert.NotNull(lido.Favorites);
        Assert.NotNull(lido.History);
    }
}
