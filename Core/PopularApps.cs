namespace WinTerminalTools.Core;

public sealed record PopularApp(string Id, string Name, string Category);

/// <summary>
/// Sugestões mostradas na busca do winget antes de digitar algo. O winget não informa os "mais
/// baixados", então é uma lista curada de programas comuns (IDs conferidos com "winget show").
/// </summary>
public static class PopularApps
{
    public static IReadOnlyList<PopularApp> All { get; } = new PopularApp[]
    {
        new("OBSProject.OBSStudio", "OBS Studio", "🎬 Mídia & Criação"),
        new("Audacity.Audacity", "Audacity", "🎬 Mídia & Criação"),
        new("GIMP.GIMP.3", "GIMP", "🎬 Mídia & Criação"),
        new("BlenderFoundation.Blender", "Blender", "🎬 Mídia & Criação"),
        new("HandBrake.HandBrake", "HandBrake", "🎬 Mídia & Criação"),
        new("ShareX.ShareX", "ShareX", "🎬 Mídia & Criação"),

        new("Telegram.TelegramDesktop", "Telegram", "💬 Comunicação"),
        new("Zoom.Zoom", "Zoom", "💬 Comunicação"),
        new("Microsoft.Teams", "Microsoft Teams", "💬 Comunicação"),
        new("AnyDesk.AnyDesk", "AnyDesk", "💬 Comunicação"),

        new("TheDocumentFoundation.LibreOffice", "LibreOffice", "📄 Escritório"),
        new("Adobe.Acrobat.Reader.64-bit", "Adobe Acrobat Reader", "📄 Escritório"),
        new("Notion.Notion", "Notion", "📄 Escritório"),
        new("Obsidian.Obsidian", "Obsidian", "📄 Escritório"),

        new("Microsoft.PowerToys", "PowerToys", "🧰 Utilitários"),
        new("voidtools.Everything", "Everything (busca de arquivos)", "🧰 Utilitários"),
        new("RARLab.WinRAR", "WinRAR", "🧰 Utilitários"),
        new("qBittorrent.qBittorrent", "qBittorrent", "🧰 Utilitários"),
        new("Rufus.Rufus", "Rufus (pendrive bootável)", "🧰 Utilitários"),
        new("CrystalDewWorld.CrystalDiskInfo", "CrystalDiskInfo", "🧰 Utilitários"),
        new("CPUID.CPU-Z", "CPU-Z", "🧰 Utilitários"),
        new("TechPowerUp.GPU-Z", "GPU-Z", "🧰 Utilitários"),

        new("Bitwarden.Bitwarden", "Bitwarden", "🔐 Segurança"),
        new("KeePassXCTeam.KeePassXC", "KeePassXC", "🔐 Segurança"),
        new("Malwarebytes.Malwarebytes", "Malwarebytes", "🔐 Segurança"),
        new("Proton.ProtonVPN", "Proton VPN", "🔐 Segurança"),

        new("EpicGames.EpicGamesLauncher", "Epic Games Launcher", "🎮 Jogos"),
        new("ElectronicArts.EADesktop", "EA app", "🎮 Jogos"),

        new("Microsoft.WindowsTerminal", "Windows Terminal", "💻 Desenvolvimento"),
        new("Python.Python.3.13", "Python 3.13", "💻 Desenvolvimento"),
        new("OpenJS.NodeJS.LTS", "Node.js LTS", "💻 Desenvolvimento"),
        new("Postman.Postman", "Postman", "💻 Desenvolvimento"),
        new("JetBrains.Toolbox", "JetBrains Toolbox", "💻 Desenvolvimento"),
        new("Microsoft.VisualStudio.2022.Community", "Visual Studio 2022 Community", "💻 Desenvolvimento"),
    };
}
