using System.IO;
using System.Text;
using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

public class AppUpdaterTests
{
    [Theory]
    [InlineData("2.2.0", "2.1.1", true)]
    [InlineData("v2.2.0", "2.1.1", true)]
    [InlineData("3.0.0", "2.9.9", true)]
    [InlineData("2.1.2", "2.1.1", true)]
    [InlineData("2.1.1", "2.1.1", false)]
    [InlineData("v2.1.1", "2.1.1", false)]
    [InlineData("2.1.0", "2.1.1", false)]
    [InlineData("1.9.9", "2.1.1", false)]
    [InlineData("2.2.0+abc123", "2.1.1", true)]
    [InlineData("", "2.1.1", false)]
    [InlineData("invalida", "2.1.1", false)]
    public void IsNewerVersion_ComparaCorretamente(string latest, string current, bool esperado)
        => Assert.Equal(esperado, AppUpdater.IsNewerVersion(latest, current));

    [Theory]
    [InlineData("https://github.com/jeanr0cha/WinTerminalTools/releases/download/v2.2.0/WinTerminalTools.exe", true)]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/WinTerminalTools.exe", true)]
    [InlineData("https://release-assets.githubusercontent.com/abc/WinTerminalTools.exe", true)]
    [InlineData("http://github.com/jeanr0cha/WinTerminalTools/releases/download/v2.2.0/WinTerminalTools.exe", false)]
    [InlineData("https://github.com/outro-usuario/WinTerminalTools/releases/download/v2.2.0/WinTerminalTools.exe", false)]
    [InlineData("https://evil.example.com/WinTerminalTools.exe", false)]
    [InlineData("", false)]
    public void IsTrustedDownloadUrl_AceitaApenasGitHubOficialHttps(string url, bool esperado)
        => Assert.Equal(esperado, AppUpdater.IsTrustedDownloadUrl(url));

    [Fact]
    public void ParseLatestRelease_ExtraiVersaoEAssetExecutavel()
    {
        const string json = """
        {
          "tag_name": "v2.2.0",
          "html_url": "https://github.com/jeanr0cha/WinTerminalTools/releases/tag/v2.2.0",
          "draft": false,
          "prerelease": false,
          "assets": [
            {
              "name": "WinTerminalTools-v2.2.0-win-x64.zip",
              "size": 900000,
              "browser_download_url": "https://github.com/jeanr0cha/WinTerminalTools/releases/download/v2.2.0/WinTerminalTools-v2.2.0-win-x64.zip"
            },
            {
              "name": "WinTerminalTools.exe",
              "size": 65000000,
              "browser_download_url": "https://github.com/jeanr0cha/WinTerminalTools/releases/download/v2.2.0/WinTerminalTools.exe"
            }
          ]
        }
        """;

        var info = AppUpdater.ParseLatestRelease(json);

        Assert.NotNull(info);
        Assert.Equal("2.2.0", info.Version);
        Assert.Equal("v2.2.0", info.TagName);
        Assert.Equal("https://github.com/jeanr0cha/WinTerminalTools/releases/tag/v2.2.0", info.ReleaseUrl);
        Assert.Equal("https://github.com/jeanr0cha/WinTerminalTools/releases/download/v2.2.0/WinTerminalTools.exe", info.ExecutableUrl);
        Assert.Equal(65000000, info.ExecutableSize);
    }

    [Fact]
    public void ParseLatestRelease_IgnoraDraftEPrerelease()
    {
        Assert.Null(AppUpdater.ParseLatestRelease("""{"tag_name":"v2.2.0","draft":true,"prerelease":false}"""));
        Assert.Null(AppUpdater.ParseLatestRelease("""{"tag_name":"v2.2.0-beta.1","draft":false,"prerelease":true}"""));
        Assert.Null(AppUpdater.ParseLatestRelease("não é json"));
    }

    [Theory]
    [InlineData(@"C:\Tools\WinTerminalTools.exe", true)]
    [InlineData(@"D:\pasta com espaco\winterminaltools.exe", true)]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void CanSelfReplace_PermiteApenasExecutavelDoApp(string? path, bool esperado)
        => Assert.Equal(esperado, AppUpdater.CanSelfReplace(path));

    [Fact]
    public void HasValidExecutableHeader_ReconhecePEValidoERecusaTexto()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"wtt-upd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var validExe = Path.Combine(tempDir, "ok.exe");
            var bytes = new byte[2048];
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'Z';
            File.WriteAllBytes(validExe, bytes);
            Assert.True(AppUpdater.HasValidExecutableHeader(validExe));

            var htmlFile = Path.Combine(tempDir, "erro.exe");
            File.WriteAllText(htmlFile, new string('x', 2048));
            Assert.False(AppUpdater.HasValidExecutableHeader(htmlFile));

            var tinyFile = Path.Combine(tempDir, "tiny.exe");
            File.WriteAllBytes(tinyFile, new byte[] { (byte)'M', (byte)'Z' });
            Assert.False(AppUpdater.HasValidExecutableHeader(tinyFile));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CreateSwapProcessStartInfo_UsaEncodedCommandEEscapaAspasSimples()
    {
        var psi = AppUpdater.CreateSwapProcessStartInfo(
            1234,
            @"C:\Users\O'Connor\WinTerminalTools.exe.new",
            @"C:\Users\O'Connor\WinTerminalTools.exe");

        Assert.Equal("powershell.exe", psi.FileName);
        Assert.False(psi.UseShellExecute);
        Assert.True(psi.CreateNoWindow);
        Assert.Contains("-EncodedCommand", psi.ArgumentList);

        var decoded = Encoding.Unicode.GetString(Convert.FromBase64String(psi.ArgumentList[^1]));
        Assert.Contains("$pidAlvo = 1234", decoded);
        Assert.Contains(@"C:\Users\O''Connor\WinTerminalTools.exe.new", decoded);
        Assert.Contains(@"C:\Users\O''Connor\WinTerminalTools.exe", decoded);
    }
}
