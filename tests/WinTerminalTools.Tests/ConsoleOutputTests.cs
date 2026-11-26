using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

/// <summary>Arrumação da saída no console e mensagens do winget.</summary>
public class ConsoleOutputTests
{
    private static List<string> Filtrar(params string[] linhas)
    {
        var escritas = new List<string>();
        var filtro = new OutputLineFilter(escritas.Add);
        foreach (var linha in linhas)
            filtro.Push(linha);
        return escritas;
    }

    [Fact]
    public void Filtro_TiraBrancosDoInicioEDoFim()
        => Assert.Equal(new[] { "a", "b" }, Filtrar("", "  ", "a", "b", "", ""));

    [Fact]
    public void Filtro_JuntaVariasLinhasEmBrancoNumaSo()
        => Assert.Equal(new[] { "a", "", "b" }, Filtrar("a", "", "", "   ", "b"));

    [Fact]
    public void Filtro_IgnoraAnimacaoDeProgressoDoWinget()
        => Assert.Equal(new[] { "Found Git [Git.Git]", "Successfully installed" },
            Filtrar("   -", "\\", "|", " / ", "Found Git [Git.Git]",
                    "  ██████████████▒▒▒▒▒▒▒▒▒▒▒▒▒▒  30.0 MB / 61.2 MB", "  ██████████████████████████████  100%",
                    "Successfully installed"));

    [Theory]
    [InlineData("    ████████░░░░░░░░░░░░  40% usado")] // barra do "Espaço em Disco" do próprio app
    [InlineData("C:\\Windows\\System32")]
    [InlineData("--accept-source-agreements")]
    [InlineData("TCP    0.0.0.0:135    0.0.0.0:0    LISTENING    1234")]
    public void Filtro_NaoConfundeTextoNormalComProgresso(string linha)
        => Assert.False(OutputLineFilter.IsProgressNoise(linha));

    [Theory]
    [InlineData(-1978335212, "não encontrado")]   // 0x8A150014
    [InlineData(-1978335215, "Mais de um")]       // 0x8A150011
    [InlineData(-1978335189, "Nenhuma atualização")] // 0x8A15002B
    [InlineData(-1978335135, "já está instalado")]   // 0x8A150061
    public void Winget_CodigosConhecidosViramMensagemEmPortugues(int codigo, string trecho)
        => Assert.Contains(trecho, OutputAnalyzer.WingetExitHint(codigo));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public void Winget_CodigoDesconhecidoNaoInventaMensagem(int codigo)
        => Assert.Null(OutputAnalyzer.WingetExitHint(codigo));

    [Fact]
    public void ListaPadrao_NaoUsaMaisIdsQueSairamDoWinget()
    {
        var ids = QuickInstallCatalog.Defaults.SelectMany(g => g.Apps).Select(a => a.Id).ToList();
        Assert.DoesNotContain(ids, id => QuickInstallCatalog.RenamedIds.ContainsKey(id));
        Assert.Contains("9NKSQGP7F2NH", ids);
        Assert.Contains("Brave.Brave", ids);
    }

    [Fact]
    public void ListaAntigaDoUsuario_TemIdsTrocadosAoCarregar()
    {
        var lida = QuickInstallCatalog.Parse("""
            [ { "title": "Antigos", "apps": [
                { "id": "WhatsApp.WhatsApp", "name": "WhatsApp" },
                { "id": "BraveSoftware.BraveBrowser", "name": "Brave", "checked": true },
                { "id": "Git.Git", "name": "Git" } ] } ]
            """);
        Assert.Equal(new[] { "9NKSQGP7F2NH", "Brave.Brave", "Git.Git" }, lida.Single().Apps.Select(a => a.Id));
        Assert.True(lida.Single().Apps[1].Checked);
    }

    [Theory]
    [InlineData("O Serviço de Configuração Automática sem Fio não está em execução.")]
    [InlineData("Endereço IPv4. . . . . . . . : 192.168.0.10")]
    [InlineData("ação ✔ ▸ █")]
    public void Decodificacao_AceitaUtf8EPagina850NaMesmaSaida(string texto)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var cp850 = System.Text.Encoding.GetEncoding(850);

        // netsh/winget escrevem UTF-8...
        Assert.Equal(texto, CommandExecutor.DecodeLine(System.Text.Encoding.UTF8.GetBytes(texto), cp850));

        // ...e ipconfig/systeminfo escrevem na página 850 (quando o texto cabe nela).
        var bytes850 = cp850.GetBytes(texto);
        if (cp850.GetString(bytes850) == texto)
            Assert.Equal(texto, CommandExecutor.DecodeLine(bytes850, cp850));
    }

    [Fact]
    public async Task Saida_ComQuebrasCrLfECrSozinho_ViraLinhasSeparadas()
    {
        // PowerShell escrevendo bytes crus: "a\r\nb\rc\n" deve virar a, b, c.
        var spec = CommandSpec.Create("powershell", "-NoProfile", "-Command",
            "$o=[Console]::OpenStandardOutput(); $b=[byte[]](97,13,10,98,13,99,10); $o.Write($b,0,$b.Length)");
        var linhas = new List<string>();
        var (_, _, code) = await CommandExecutor.ExecuteAsync(spec, onOutputLine: linhas.Add);
        Assert.Equal(0, code);
        Assert.Equal(new[] { "a", "b", "c" }, linhas);
    }

    [Fact]
    public async Task ScriptPowerShell_SaiEmUtf8ComSimbolos()
    {
        // Só lê o espaço das unidades (Get-Volume); confere que acentos e a barra █░ chegam intactos.
        var (output, error, code) = await CommandExecutor.ExecuteAsync(CommandCatalog.DiskSpace());
        Assert.True(code == 0, error);
        Assert.Contains("▸ ", output);
        Assert.Contains("Saúde:", output);
        Assert.Contains("% usado", output);
    }
}
