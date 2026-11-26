using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

/// <summary>
/// Regras do catálogo da interface. Nenhum teste aqui executa comandos: só confere os dados.
/// </summary>
public class CatalogTests
{
    public static TheoryData<string> TodosOsIds()
    {
        var data = new TheoryData<string>();
        foreach (var d in CommandCatalog.Definitions)
            data.Add(d.Id);
        return data;
    }

    [Fact]
    public void Ids_SaoUnicos()
    {
        var repetidos = CommandCatalog.Definitions.GroupBy(d => d.Id).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(repetidos);
    }

    [Fact]
    public void Secoes_TemIdsUnicos()
    {
        var repetidos = CommandCatalog.Sections.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(repetidos);
    }

    [Fact]
    public void TodaSecao_TemPeloMenosUmComando()
    {
        var vazias = CommandCatalog.Sections.Where(s => CommandCatalog.Definitions.All(d => d.SectionId != s.Id));
        Assert.Empty(vazias);
    }

    [Theory]
    [MemberData(nameof(TodosOsIds))]
    public void Comando_ApontaParaSecaoExistente(string id)
    {
        var definicao = CommandCatalog.Get(id);
        Assert.Contains(CommandCatalog.Sections, s => s.Id == definicao.SectionId);
    }

    [Theory]
    [MemberData(nameof(TodosOsIds))]
    public void Comando_TemTituloDescricaoEPassos(string id)
    {
        var definicao = CommandCatalog.Get(id);
        Assert.False(string.IsNullOrWhiteSpace(definicao.Title));
        Assert.False(string.IsNullOrWhiteSpace(definicao.Description));
        Assert.NotEmpty(definicao.Steps);
        Assert.All(definicao.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.FileName)));
    }

    [Theory]
    [MemberData(nameof(TodosOsIds))]
    public void ComandoComRisco_SempreTemConfirmacao(string id)
    {
        var definicao = CommandCatalog.Get(id);
        if (definicao.Risk != CommandRisk.Safe)
        {
            Assert.False(string.IsNullOrWhiteSpace(definicao.Confirmation));
            Assert.False(string.IsNullOrWhiteSpace(definicao.ConfirmationTitle));
        }
    }

    [Theory]
    [InlineData("ip-release")]
    [InlineData("ip-reset")]
    [InlineData("winget-upgrade-all")]
    [InlineData("winsock-reset")]
    [InlineData("tcpip-reset")]
    [InlineData("network-full-repair")]
    [InlineData("temp-clean")]
    [InlineData("recycle-bin")]
    [InlineData("hibernate")]
    public void ComandosQueAlteramOSistema_SaoPerigosos(string id)
        => Assert.Equal(CommandRisk.Dangerous, CommandCatalog.Get(id).Risk);

    [Theory]
    [InlineData("sfc")]
    [InlineData("dism")]
    [InlineData("chkdsk")]
    [InlineData("ip-release")]
    [InlineData("winsock-reset")]
    [InlineData("tcpip-reset")]
    [InlineData("restore-point-create")]
    public void ComandosDeSistema_ExigemAdmin(string id) => Assert.True(CommandCatalog.Get(id).RequiresAdmin);

    [Fact]
    public void ReparoCompletoDaRede_SegueMesmoSeUmPassoFalhar()
    {
        var reparo = CommandCatalog.Get("network-full-repair");
        Assert.Equal(3, reparo.Steps.Count);
        Assert.True(reparo.ContinueOnFailure);
    }

    [Fact]
    public void RelatorioDeBateria_SalvaEmDocumentos()
    {
        var passo = CommandCatalog.Get("battery-report").Steps.Single();
        Assert.Equal(CommandCatalog.BatteryReportPath, passo.Arguments[^1]);
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), CommandCatalog.BatteryReportPath);
    }

    [Fact]
    public async Task TempoLigado_ScriptPowerShellRodaEMostraAData()
    {
        // Comando só de leitura: consulta a hora do último boot.
        var (output, error, code) = await CommandExecutor.ExecuteAsync(CommandCatalog.Get("uptime").Steps.Single());
        Assert.True(code == 0, error);
        Assert.StartsWith("Ligado desde", output);
    }

    [Fact]
    public void ResetDeConexao_FazReleaseERenewMesmoSeOReleaseFalhar()
    {
        var reset = CommandCatalog.Get("ip-reset");
        Assert.Equal(new[] { "/release", "/renew" }, reset.Steps.Select(s => s.Arguments.Single()));
        Assert.True(reset.ContinueOnFailure);
    }

    [Fact]
    public void Busca_IncluiOComandoReal()
        => Assert.Contains("netstat -ano", CommandCatalog.Get("netstat").SearchText);
}
