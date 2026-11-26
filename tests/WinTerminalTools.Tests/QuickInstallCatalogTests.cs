using System.IO;
using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

/// <summary>Lista de apps da instalação rápida. Usa só arquivos temporários (nunca o %APPDATA% real).</summary>
public class QuickInstallCatalogTests
{
    [Fact]
    public void ListaPadrao_SobreviveAIdaEVoltaPeloJson()
    {
        var json = QuickInstallCatalog.Serialize(QuickInstallCatalog.Defaults);
        var lida = QuickInstallCatalog.Parse(json);

        Assert.Equal(QuickInstallCatalog.Defaults.Count, lida.Count);
        Assert.Equal(QuickInstallCatalog.Defaults.SelectMany(g => g.Apps), lida.SelectMany(g => g.Apps));
        Assert.Contains("Mídia", json); // acentos legíveis para quem for editar
    }

    [Fact]
    public void Parse_AceitaComentariosEVirgulaSobrando()
    {
        var lida = QuickInstallCatalog.Parse("""
            // meus apps
            [ { "title": "Trabalho", "apps": [ { "id": "Zoom.Zoom", "name": "Zoom", "checked": true }, ] } ]
            """);
        Assert.Equal(new QuickInstallApp("Zoom.Zoom", "Zoom", true), lida.Single().Apps.Single());
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("[]")]
    [InlineData("""[ { "title": "X", "apps": [] } ]""")]
    [InlineData("""[ { "title": "X", "apps": [ { "id": "a&calc", "name": "Mal" } ] } ]""")]
    [InlineData("""[ { "title": "X", "apps": [ { "id": "Git.Git", "name": "" } ] } ]""")]
    public void Parse_RecusaListaInvalidaComMensagem(string json)
    {
        var erro = Assert.Throws<FormatException>(() => QuickInstallCatalog.Parse(json));
        Assert.False(string.IsNullOrWhiteSpace(erro.Message));
    }

    [Fact]
    public void Load_SemArquivo_UsaListaPadrao()
        => Assert.Same(QuickInstallCatalog.Defaults, QuickInstallCatalog.Load(Path.Combine(Path.GetTempPath(), $"wtt-{Guid.NewGuid():N}.json")));

    [Fact]
    public void EnsureUserFile_CriaArquivoComPadraoSemSobrescrever()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"wtt-{Guid.NewGuid():N}");
        var arquivo = Path.Combine(pasta, "apps.json");
        try
        {
            QuickInstallCatalog.EnsureUserFile(arquivo);
            Assert.Equal(QuickInstallCatalog.Defaults.Count, QuickInstallCatalog.Load(arquivo).Count);

            File.WriteAllText(arquivo, """[ { "title": "Só um", "apps": [ { "id": "Git.Git", "name": "Git" } ] } ]""");
            QuickInstallCatalog.EnsureUserFile(arquivo);
            Assert.Equal("Só um", QuickInstallCatalog.Load(arquivo).Single().Title);
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }
}
