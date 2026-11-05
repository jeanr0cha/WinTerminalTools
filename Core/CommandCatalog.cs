using System.Globalization;
using System.IO;

namespace WinTerminalTools.Core;

/// <summary>
/// Monta os comandos usados pela UI. Concentra o conhecimento de "qual executável + quais argumentos".
/// </summary>
public static class CommandCatalog
{
    private const string WingetAgreements1 = "--accept-package-agreements";
    private const string WingetAgreements2 = "--accept-source-agreements";

    public static CommandSpec Ping(string host) => CommandSpec.Create("ping", host);
    public static CommandSpec Tracert(string host) => CommandSpec.Create("tracert", host);

    public static CommandSpec IpRelease => CommandSpec.Create("ipconfig", "/release");
    public static CommandSpec IpRenew => CommandSpec.Create("ipconfig", "/renew");

    public static CommandSpec WingetListUpgrades => CommandSpec.Create("winget", "upgrade");

    public static CommandSpec WingetUpgradeAll =>
        CommandSpec.Create("winget", "upgrade", "--all", "--include-unknown", WingetAgreements1, WingetAgreements2);

    public static CommandSpec WingetUpgrade(string id) =>
        CommandSpec.Create("winget", "upgrade", "--id", id, "-e", WingetAgreements1, WingetAgreements2);

    public static CommandSpec WingetSearch(string term) => CommandSpec.Create("winget", "search", term);

    public static CommandSpec WingetInstall(string id) =>
        CommandSpec.Create("winget", "install", "--id", id, "-e", "--silent", WingetAgreements1, WingetAgreements2);

    public static CommandSpec DiskSpace() =>
        CommandSpec.Create("powershell", "-NoProfile", "-Command",
            "Get-Volume | Format-Table -AutoSize DriveLetter, FriendlyName, FileSystemType, HealthStatus, SizeRemaining, Size");

    public static CommandSpec Sfc => CommandSpec.Create("sfc", "/scannow");
    public static CommandSpec DismRestoreHealth => CommandSpec.Create("DISM", "/Online", "/Cleanup-Image", "/RestoreHealth");

    /// <summary>chkdsk em modo leitura na unidade do Windows (ex.: "C:").</summary>
    public static CommandSpec Chkdsk(string systemDirectory)
    {
        var root = Path.GetPathRoot(systemDirectory);
        var drive = string.IsNullOrEmpty(root) ? "C:" : root.TrimEnd('\\', '/');
        return CommandSpec.Create("chkdsk", drive);
    }

    public static CommandSpec ShutdownIn(TimeSpan delay) =>
        CommandSpec.Create("shutdown", "/s", "/t", ((int)delay.TotalSeconds).ToString(CultureInfo.InvariantCulture));

    public static CommandSpec ShutdownAbort => CommandSpec.Create("shutdown", "/a");

    public static CommandSpec OpenExplorer(string folder) => CommandSpec.Create("explorer.exe", folder);
}
