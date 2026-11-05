using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace WinTerminalTools.Core;

/// <summary>
/// Validação e normalização de tudo que o usuário digita antes de virar argumento de processo.
/// </summary>
public static partial class InputValidator
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._+\-]{0,127}$")]
    private static partial Regex WingetIdRegex();

    /// <summary>
    /// Aceita IPv4/IPv6 ou nome de host; remove esquema, caminho e porta (ex.: https://a.com:8080/x → a.com).
    /// Retorna null quando o valor não é um host válido (inclui flags como "-t" e metacaracteres).
    /// </summary>
    public static string? NormalizeHost(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var value = input.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            !string.IsNullOrEmpty(uri.Host) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            value = uri.Host;
        }
        else
        {
            int slash = value.IndexOf('/');
            if (slash >= 0)
                value = value[..slash];

            // host:porta (somente quando há um único ':', para não quebrar IPv6)
            int firstColon = value.IndexOf(':');
            if (firstColon >= 0 && firstColon == value.LastIndexOf(':'))
                value = value[..firstColon];
        }

        value = value.Trim('[', ']');

        if (value.Length == 0 || value.StartsWith('-'))
            return null;

        if (IPAddress.TryParse(value, out _))
            return value;

        return Uri.CheckHostName(value) == UriHostNameType.Dns ? value : null;
    }

    /// <summary>ID do winget no formato Publisher.Package (letras, dígitos, ., _, +, -).</summary>
    public static bool IsValidWingetId(string? id) => !string.IsNullOrEmpty(id) && WingetIdRegex().IsMatch(id);

    /// <summary>Termo de busca livre: sem vazio, sem caracteres de controle e sem começar com "-" (evita virar flag).</summary>
    public static string? NormalizeSearchTerm(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var value = input.Trim();
        if (value.StartsWith('-') || value.Any(char.IsControl) || value.Length > 128)
            return null;

        return value;
    }

    /// <summary>
    /// Valida um caminho de pasta absoluto e retorna o caminho completo normalizado, ou null se inválido.
    /// </summary>
    public static string? NormalizeFolderPath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var value = input.Trim().Trim('"');
        if (value.Any(char.IsControl) || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return null;

        try
        {
            if (!Path.IsPathRooted(value))
                return null;

            return Path.GetFullPath(value);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
