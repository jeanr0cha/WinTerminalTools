using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

/// <summary>Leitura da tabela do "winget search" (saídas reais copiadas do winget; nada é executado).</summary>
public class WingetSearchTests
{
    // Trecho real de "winget search obs" (2026-11), com nomes em chinês e japonês que ocupam 2 colunas.
    private const string SaidaReal = """
        Name                                      Id                                Version                 Match                         Source
        -----------------------------------------------------------------------------------------------------------------------------------------
        OBS Studio                                OBSProject.OBSStudio              33.0.3                  Moniker: obs                  winget
        哔哩哔哩直播姬                            Bilibili.Livehime                 8.16.0.11349            Tag: obs                      winget
        N Air 実験版                              DWANGO.NAIR.experimental          1.1.20261119-unstable.1 Tag: obs                      winget
        Idvevent导播端                            luolangaga.ASG.Director           3.1.3                   Tag: obs                      winget
        AMD Encoder for OBS Studio                OBSProject.obs-amd-encoder        2.6.0.0                 Tag: obs                      winget

        """;

    [Fact]
    public void Parse_LeNomeIdVersaoEOrigem()
    {
        var pacotes = WingetSearchParser.Parse(SaidaReal);
        Assert.Equal(5, pacotes.Count);
        Assert.Equal(new WingetPackage("OBS Studio", "OBSProject.OBSStudio", "33.0.3", "winget"), pacotes[0]);
    }

    [Fact]
    public void Parse_NomesComCaracteresLargosNaoDesalinhamOId()
    {
        var pacotes = WingetSearchParser.Parse(SaidaReal);
        Assert.Equal("Bilibili.Livehime", pacotes[1].Id);
        Assert.Equal("哔哩哔哩直播姬", pacotes[1].Name);
        Assert.Equal("DWANGO.NAIR.experimental", pacotes[2].Id);
        Assert.Equal("1.1.20261119-unstable.1", pacotes[2].Version);
        Assert.Equal("luolangaga.ASG.Director", pacotes[3].Id);
        Assert.All(pacotes, p => Assert.True(p.CanInstall));
    }

    [Fact]
    public void Parse_SemColunaMatchETambemComLinhasDeProgressoAntes()
    {
        var saida = "   -\r\n   \\\r\nName           Id                  Version  Source\r\n" +
                    "---------------------------------------------------\r\n" +
                    "Brave Browser  XP8C9QZMS2PC1T      Unknown  msstore\r\n" +
                    "Brave          Brave.Brave         158.1.1  winget\r\n";
        var pacotes = WingetSearchParser.Parse(saida);
        Assert.Equal(new[] { "XP8C9QZMS2PC1T", "Brave.Brave" }, pacotes.Select(p => p.Id));
        Assert.Equal("msstore", pacotes[0].Source);
    }

    [Fact]
    public void Parse_IdCortadoPeloWingetNaoPodeSerInstalado()
    {
        var saida = "Name   Id                    Version  Source\n" +
                    "--------------------------------------------\n" +
                    "Algo   Empresa.ProgramaMuit… 1.0      winget\n";
        var pacote = WingetSearchParser.Parse(saida).Single();
        Assert.False(pacote.CanInstall);
    }

    [Theory]
    [InlineData("No package found matching input criteria.")]
    [InlineData("")]
    public void Parse_SemTabela_DevolveListaVazia(string saida) => Assert.Empty(WingetSearchParser.Parse(saida));

    [Fact]
    public void Populares_TemIdsValidosEUnicos()
    {
        Assert.All(PopularApps.All, a => Assert.True(InputValidator.IsValidWingetId(a.Id), a.Id));
        Assert.Equal(PopularApps.All.Count, PopularApps.All.Select(a => a.Id).Distinct().Count());
    }
}
