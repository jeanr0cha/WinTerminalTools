using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace WinTerminalTools.Core;

/// <summary>Informações de uma release publicada no GitHub.</summary>
public sealed record ReleaseInfo(
    string Version,
    string TagName,
    string ReleaseUrl,
    string? ExecutableUrl,
    long ExecutableSize);

/// <summary>
/// Verifica atualizações no GitHub Releases e aplica a troca do executável portátil.
/// </summary>
public static class AppUpdater
{
    public const string RepoOwner = "jeanr0cha";
    public const string RepoName = "WinTerminalTools";
    public const string LatestReleaseApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
    public const string ReleasesPageUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";
    public const string ExecutableAssetName = "WinTerminalTools.exe";

    private static readonly HttpClient SharedHttpClient = CreateDefaultHttpClient();

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"WinTerminalTools/{AppInfo.Version}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>Retorna true quando <paramref name="latestVersion"/> é estritamente maior que <paramref name="currentVersion"/>.</summary>
    public static bool IsNewerVersion(string? latestVersion, string? currentVersion)
    {
        if (!TryParseSemVer(latestVersion, out var latest) || !TryParseSemVer(currentVersion, out var current))
            return false;

        return latest > current;
    }

    private static bool TryParseSemVer(string? raw, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var clean = raw.Trim().TrimStart('v', 'V');
        var cut = clean.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0)
            clean = clean[..cut];

        if (!Version.TryParse(clean, out var parsed))
            return false;

        version = new Version(
            parsed.Major,
            parsed.Minor,
            parsed.Build < 0 ? 0 : parsed.Build,
            parsed.Revision < 0 ? 0 : parsed.Revision);
        return true;
    }

    /// <summary>Aceita apenas URLs HTTPS do repositório oficial no GitHub.</summary>
    public static bool IsTrustedDownloadUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host == "github.com")
            return uri.AbsolutePath.StartsWith($"/{RepoOwner}/{RepoName}/", StringComparison.OrdinalIgnoreCase);

        return host is "objects.githubusercontent.com" or "release-assets.githubusercontent.com";
    }

    /// <summary>Interpreta o JSON de /releases/latest da API do GitHub.</summary>
    public static ReleaseInfo? ParseLatestRelease(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True)
                return null;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            var version = tag.Trim().TrimStart('v', 'V');
            var htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;
            var releaseUrl = IsTrustedDownloadUrl(htmlUrl) ? htmlUrl! : ReleasesPageUrl;

            string? exeUrl = null;
            long exeSize = 0;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    if (!string.Equals(name, ExecutableAssetName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var downloadUrl = asset.TryGetProperty("browser_download_url", out var dlEl) ? dlEl.GetString() : null;
                    if (IsTrustedDownloadUrl(downloadUrl))
                    {
                        exeUrl = downloadUrl;
                        exeSize = asset.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var s) ? s : 0;
                    }
                    break;
                }
            }

            return new ReleaseInfo(version, tag.Trim(), releaseUrl, exeUrl, exeSize);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Só permite substituir o executável quando o processo atual é realmente o WinTerminalTools.exe
    /// (evita tentar sobrescrever dotnet.exe em modo de desenvolvimento).
    /// </summary>
    public static bool CanSelfReplace(string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
            return false;

        return string.Equals(Path.GetFileName(processPath), ExecutableAssetName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Confirma que o arquivo baixado tem tamanho mínimo e cabeçalho PE ("MZ") de executável Windows.</summary>
    public static bool HasValidExecutableHeader(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists || info.Length < 1024)
                return false;

            using var stream = File.OpenRead(filePath);
            return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Consulta a última release no GitHub. Nunca lança exceção em falhas de rede: retorna null.
    /// </summary>
    public static async Task<ReleaseInfo?> CheckForUpdateAsync(
        string currentVersion,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClient ?? SharedHttpClient;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

            using var response = await client.GetAsync(LatestReleaseApiUrl, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            var info = ParseLatestRelease(json);
            if (info == null || !IsNewerVersion(info.Version, currentVersion))
                return null;

            return info;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Baixa o novo executável para <paramref name="destinationPath"/> informando o progresso (0 a 100).</summary>
    public static async Task DownloadExecutableAsync(
        string url,
        string destinationPath,
        IProgress<double>? progress = null,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsTrustedDownloadUrl(url))
            throw new InvalidOperationException("URL de download não pertence ao repositório oficial.");

        var client = httpClient ?? SharedHttpClient;
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        try
        {
            await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var local = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long downloaded = 0;
                int read;

                while ((read = await remote.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloaded += read;

                    if (totalBytes is > 0)
                        progress?.Report(Math.Clamp(100.0 * downloaded / totalBytes.Value, 0, 100));
                }
            }

            if (!HasValidExecutableHeader(destinationPath))
                throw new InvalidDataException("O arquivo baixado não é um executável Windows válido.");
        }
        catch
        {
            try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }
            throw;
        }
    }

    /// <summary>
    /// Monta o script PowerShell que espera o app atual encerrar, substitui o .exe e reabre a nova versão.
    /// </summary>
    public static string BuildSwapScript(int currentPid, string newExePath, string targetExePath)
    {
        static string PsQuote(string s) => $"'{s.Replace("'", "''")}'";

        return
            $"$pidAlvo = {currentPid}; " +
            $"$novo = {PsQuote(newExePath)}; " +
            $"$destino = {PsQuote(targetExePath)}; " +
            "for ($i = 0; $i -lt 50; $i++) { if (-not (Get-Process -Id $pidAlvo -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 300 }; " +
            "$ok = $false; " +
            "for ($i = 0; $i -lt 20; $i++) { " +
            "  try { Move-Item -LiteralPath $novo -Destination $destino -Force -ErrorAction Stop; $ok = $true; break } " +
            "  catch { Start-Sleep -Milliseconds 300 } " +
            "}; " +
            "if ($ok) { Start-Process -FilePath $destino }";
    }

    /// <summary>Cria o ProcessStartInfo do atualizador usando -EncodedCommand (imune a espaços e aspas no caminho).</summary>
    public static ProcessStartInfo CreateSwapProcessStartInfo(int currentPid, string newExePath, string targetExePath)
    {
        var script = BuildSwapScript(currentPid, newExePath, targetExePath);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-WindowStyle");
        psi.ArgumentList.Add("Hidden");
        psi.ArgumentList.Add("-EncodedCommand");
        psi.ArgumentList.Add(encoded);
        return psi;
    }
}
