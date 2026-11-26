using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

/// <summary>Scripts da rede local. Só os que leem informações são executados (nenhum altera nada).</summary>
public class NetworkLanTests
{
    [Theory]
    [InlineData("net-adapters", "MAC:")]
    [InlineData("net-connection", "Meu IP:")]
    [InlineData("lan-devices", "Total:")]
    public async Task ScriptsDeLeitura_RodamEMostramBlocos(string id, string trecho)
    {
        var (output, error, code) = await CommandExecutor.ExecuteAsync(CommandCatalog.Get(id).Steps.Single());
        Assert.True(code == 0, $"código {code}: {error}");
        Assert.Contains("▸ ", output);
        Assert.Contains(trecho, output);
    }

    [Fact]
    public void Escanear_PedeConfirmacaoAntes()
    {
        var scan = CommandCatalog.Get("lan-scan");
        Assert.NotEqual(CommandRisk.Safe, scan.Risk);
        Assert.Contains("rede", scan.Confirmation);
    }
}
