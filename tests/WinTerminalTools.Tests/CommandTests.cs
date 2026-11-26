using System.IO;
using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

public class CommandTests
{
    [Fact]
    public void Winget_SempreLidoComoUtf8()
    {
        Assert.All(new[]
        {
            CommandCatalog.WingetListUpgrades, CommandCatalog.WingetUpgradeAll, CommandCatalog.WingetSearch("x"),
            CommandCatalog.WingetUpgrade("Git.Git"), CommandCatalog.WingetInstall("Git.Git")
        }, s => Assert.True(s.Utf8Output));
        Assert.False(CommandCatalog.Sfc.Utf8Output);
    }

    [Fact]
    public void TestePorta_SoAceitaHostValidadoEPortaNumerica()
    {
        var spec = CommandCatalog.TestPort("google.com", 443);
        Assert.Equal("powershell", spec.FileName);
        Assert.Contains("-ComputerName 'google.com' -Port 443", spec.Arguments[^1]);
        Assert.Throws<ArgumentException>(() => CommandCatalog.TestPort("a'; Remove-Item x; '", 80));
    }

    [Fact]
    public void SenhaWifi_NomeComEspacoViraUmUnicoArgumento()
    {
        var spec = CommandCatalog.WifiPassword("Minha Casa 5G");
        Assert.Equal(new[] { "wlan", "show", "profile", "name=Minha Casa 5G", "key=clear" }, spec.Arguments);
    }

    [Fact]
    public void Reiniciar_UsaBarraREmSegundos()
        => Assert.Equal(new[] { "/r", "/t", "1800" }, CommandCatalog.RestartIn(TimeSpan.FromMinutes(30)).Arguments);

    [Fact]
    public void WingetBackupEDesinstalar_MontamArgumentosCertos()
    {
        var arquivo = @"C:\Users\x\Documents\minha lista.json";
        Assert.Equal(new[] { "export", "-o", arquivo, "--accept-source-agreements" }, CommandCatalog.WingetExport(arquivo).Arguments);
        Assert.Contains(arquivo, CommandCatalog.WingetImport(arquivo).Arguments);
        Assert.Equal(new[] { "uninstall", "--id", "Git.Git", "-e", "--accept-source-agreements" },
            CommandCatalog.WingetUninstall("Git.Git").Arguments);
        Assert.All(new[] { CommandCatalog.WingetExport(arquivo), CommandCatalog.WingetImport(arquivo), CommandCatalog.WingetList },
            s => Assert.True(s.Utf8Output));
    }

    [Theory]
    [InlineData("winget", true)]
    [InlineData("winget.exe", true)]
    [InlineData("ipconfig", false)]
    public void DicaDeProgramaAusente_SoParaQuemPrecisaInstalar(string programa, bool temDica)
        => Assert.Equal(temDica, CommandExecutor.MissingProgramHint(programa) != null);

    [Fact]
    public void Versao_VemDoAssemblySemOHashDoCommit()
    {
        var versao = AppInfo.ResolveVersion(typeof(AppInfo).Assembly);
        Assert.Matches(@"^\d+\.\d+\.\d+", versao);
        Assert.DoesNotContain("+", versao);
    }

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
    public async Task ExecutavelInexistente_AvisaATelaPeloCallbackDeErro()
    {
        var linhas = new List<string>();
        await CommandExecutor.ExecuteAsync(CommandSpec.Create("wtt-nao-existe-xyz"), onErrorLine: linhas.Add);
        Assert.Contains(linhas, l => l.Contains("não foi encontrado"));
    }

    [Fact]
    public async Task SaidaUtf8_EhDecodificadaComoUtf8()
    {
        // PowerShell escrevendo bytes UTF-8 crus de "ação" no stdout; só lê/escreve na própria saída.
        var spec = CommandSpec.Create("powershell", "-NoProfile", "-Command",
            "$o=[Console]::OpenStandardOutput(); $b=[Text.Encoding]::UTF8.GetBytes('ação'); $o.Write($b,0,$b.Length)")
            with { Utf8Output = true };
        var (output, _, code) = await CommandExecutor.ExecuteAsync(spec);
        Assert.Equal(0, code);
        Assert.Equal("ação", output);
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
