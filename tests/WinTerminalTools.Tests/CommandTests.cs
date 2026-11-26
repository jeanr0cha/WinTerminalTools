using System.IO;
using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

public class CommandTests
{
    [Fact]
    public void Dir_PastaVaiComoDiretorioDeTrabalhoENuncaNaLinhaDeComando()
    {
        var spec = CommandCatalog.Dir(@"C:\a&calc");
        Assert.Equal("cmd.exe", spec.FileName);
        Assert.Equal(new[] { "/c", "dir" }, spec.Arguments);
        Assert.Equal(@"C:\a&calc", spec.WorkingDirectory);
    }

    [Fact]
    public void Ping_MantemHostComoUmUnicoArgumento()
    {
        var spec = CommandCatalog.Ping("8.8.8.8");
        Assert.Equal("ping", spec.FileName);
        Assert.Single(spec.Arguments);
    }

    [Fact]
    public void WingetSearch_TermoComEspacoEMetacaracteresContinuaUmArgumento()
    {
        var spec = CommandCatalog.WingetSearch("a & calc \"x\"");
        Assert.Equal(new[] { "search", "a & calc \"x\"" }, spec.Arguments);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32", "C:")]
    [InlineData(@"D:\Windows\System32", "D:")]
    [InlineData("", "C:")]
    public void Chkdsk_UsaUnidadeDoWindows(string systemDir, string drive)
        => Assert.Equal(drive, CommandCatalog.Chkdsk(systemDir).Arguments[0]);

    [Fact]
    public void ShutdownIn_UsaSegundos()
        => Assert.Equal(new[] { "/s", "/t", "3600" }, CommandCatalog.ShutdownIn(TimeSpan.FromHours(1)).Arguments);

    [Theory]
    [InlineData("", "Requer elevação para executar", true)]
    [InlineData("Access is denied.", "", true)]
    [InlineData("O arquivo requer atenção", "", false)]
    [InlineData("Arquivo não encontrado", "", false)]
    public void OutputAnalyzer_DetectaSomenteFrasesDeElevacao(string output, string error, bool esperado)
        => Assert.Equal(esperado, OutputAnalyzer.IndicatesElevationNeeded(output, error));
}

public class CommandExecutorTests
{
    // Só roda comandos inofensivos (echo / ping local / dir de pasta inexistente).
    [Fact]
    public async Task Echo_NaoInterpretaMetacaracteresDoShell()
    {
        var spec = CommandSpec.Create("cmd.exe", "/c", "echo", "ok");
        var (output, _, code) = await CommandExecutor.ExecuteAsync(spec);
        Assert.Equal(0, code);
        Assert.Equal("ok", output);
    }

    [Fact]
    public async Task Dir_ListaAPastaPedidaMesmoComMetacaractereNoNome()
    {
        // Pasta temporária com "&" no nome: se o caminho fosse para a linha de comando do cmd, quebraria.
        var pasta = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"wtt-teste a&b-{Guid.NewGuid():N}"));
        try
        {
            File.WriteAllText(Path.Combine(pasta.FullName, "marcador-wtt.txt"), "ok");
            var (output, _, code) = await CommandExecutor.ExecuteAsync(CommandCatalog.Dir(pasta.FullName));
            Assert.Equal(0, code);
            Assert.Contains("marcador-wtt.txt", output);
        }
        finally
        {
            pasta.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecutavelInexistente_RetornaErroSemLancar()
    {
        var (_, error, code) = await CommandExecutor.ExecuteAsync(CommandSpec.Create("wtt-nao-existe-xyz"));
        Assert.Equal(-1, code);
        Assert.Contains("Erro ao iniciar", error);
    }

    [Fact]
    public async Task Cancelamento_EncerraProcessoRapidamente()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (_, error, code) = await CommandExecutor.ExecuteAsync(
            CommandSpec.Create("ping", "-n", "30", "127.0.0.1"), cancellationToken: cts.Token);
        Assert.Equal(-1, code);
        Assert.Contains("cancelado", error);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10));
    }
}
