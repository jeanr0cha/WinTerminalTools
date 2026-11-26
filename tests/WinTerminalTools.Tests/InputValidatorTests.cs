using WinTerminalTools.Core;

namespace WinTerminalTools.Tests;

public class InputValidatorTests
{
    [Theory]
    [InlineData("google.com", "google.com")]
    [InlineData("  8.8.8.8  ", "8.8.8.8")]
    [InlineData("https://a.com/x?y=1", "a.com")]
    [InlineData("http://a.com:8080/", "a.com")]
    [InlineData("localhost:8080", "localhost")]
    [InlineData("192.168.1.1:80", "192.168.1.1")]
    [InlineData("::1", "::1")]
    [InlineData("[::1]", "::1")]
    public void NormalizeHost_AceitaHostsValidos(string entrada, string esperado)
        => Assert.Equal(esperado, InputValidator.NormalizeHost(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-t")]
    [InlineData("-n 100 a.com")]
    [InlineData("8.8.8.8 -t")]
    [InlineData("8.8.8.8 & calc")]
    [InlineData("a.com\"&calc&\"")]
    [InlineData("a.com%PATH%")]
    [InlineData("a|b")]
    [InlineData("a.com\r\ncalc")]
    public void NormalizeHost_RejeitaEntradasPerigosas(string? entrada)
        => Assert.Null(InputValidator.NormalizeHost(entrada));

    [Theory]
    [InlineData("Git.Git")]
    [InlineData("Google.Chrome")]
    [InlineData("Notepad++.Notepad++")]
    [InlineData("7zip.7zip")]
    [InlineData("Microsoft.VisualStudioCode")]
    public void IsValidWingetId_Aceita(string id) => Assert.True(InputValidator.IsValidWingetId(id));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-e")]
    [InlineData("Git.Git --force")]
    [InlineData("Git.Git&calc")]
    [InlineData("\"Git.Git\"")]
    public void IsValidWingetId_Rejeita(string? id) => Assert.False(InputValidator.IsValidWingetId(id));

    [Theory]
    [InlineData("obs-studio", "obs-studio")]
    [InlineData("  visual studio  ", "visual studio")]
    public void NormalizeSearchTerm_Aceita(string entrada, string esperado)
        => Assert.Equal(esperado, InputValidator.NormalizeSearchTerm(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("--source")]
    [InlineData("a\nb")]
    public void NormalizeSearchTerm_Rejeita(string? entrada) => Assert.Null(InputValidator.NormalizeSearchTerm(entrada));

    [Fact]
    public void NormalizeFolderPath_AceitaAbsolutoEAspas()
        => Assert.Equal(@"C:\Minha Pasta", InputValidator.NormalizeFolderPath("\"C:\\Minha Pasta\""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relativa\\pasta")]
    [InlineData("C:\\a\0b")]
    [InlineData("C:\\a\nb")]
    public void NormalizeFolderPath_Rejeita(string? entrada) => Assert.Null(InputValidator.NormalizeFolderPath(entrada));

    [Theory]
    [InlineData("1", 1)]
    [InlineData(" 443 ", 443)]
    [InlineData("65535", 65535)]
    public void NormalizePort_Aceita(string entrada, int esperado) => Assert.Equal(esperado, InputValidator.NormalizePort(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("80;calc")]
    [InlineData("4e2")]
    public void NormalizePort_Rejeita(string? entrada) => Assert.Null(InputValidator.NormalizePort(entrada));

    [Theory]
    [InlineData("Minha Casa 5G", "Minha Casa 5G")]
    [InlineData("  Café_Wi-Fi  ", "Café_Wi-Fi")]
    public void NormalizeWifiProfile_Aceita(string entrada, string esperado)
        => Assert.Equal(esperado, InputValidator.NormalizeWifiProfile(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("rede\" key=clear name=\"outra")]
    [InlineData("-rede")]
    [InlineData("a\nb")]
    public void NormalizeWifiProfile_Rejeita(string? entrada) => Assert.Null(InputValidator.NormalizeWifiProfile(entrada));

    [Theory]
    [InlineData("0", 0)]
    [InlineData("60", 60)]
    [InlineData("1440", 1440)]
    public void NormalizeMinutes_Aceita(string entrada, int esperado) => Assert.Equal(esperado, InputValidator.NormalizeMinutes(entrada));

    [Theory]
    [InlineData("1441")]
    [InlineData("-5")]
    [InlineData("1,5")]
    [InlineData("abc")]
    public void NormalizeMinutes_Rejeita(string entrada) => Assert.Null(InputValidator.NormalizeMinutes(entrada));
}
