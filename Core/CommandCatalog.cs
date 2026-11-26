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

    /// <summary>O winget escreve em UTF-8 mesmo redirecionado.</summary>
    private static CommandSpec Winget(params string[] arguments) =>
        CommandSpec.Create("winget", arguments) with { Utf8Output = true };

    public static CommandSpec WingetListUpgrades => Winget("upgrade");

    public static CommandSpec WingetUpgradeAll =>
        Winget("upgrade", "--all", "--include-unknown", WingetAgreements1, WingetAgreements2);

    public static CommandSpec WingetUpgrade(string id) =>
        Winget("upgrade", "--id", id, "-e", WingetAgreements1, WingetAgreements2);

    public static CommandSpec WingetSearch(string term) => Winget("search", term);

    public static CommandSpec WingetInstall(string id) =>
        Winget("install", "--id", id, "-e", "--silent", WingetAgreements1, WingetAgreements2);

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

    /// <summary>
    /// Lista o conteúdo de uma pasta. O dir é interno do cmd, então a pasta entra como diretório de
    /// trabalho e nunca na linha de comando (um nome como "a&amp;calc" não vira outro comando).
    /// </summary>
    public static CommandSpec Dir(string folder) =>
        CommandSpec.Create("cmd.exe", "/c", "dir") with { WorkingDirectory = folder };

    // ================= CATÁLOGO DA INTERFACE =================

    /// <summary>Cards das abas, na ordem em que aparecem.</summary>
    public static IReadOnlyList<CommandSection> Sections { get; } = new CommandSection[]
    {
        new("network.query", CommandTab.Network, "📊 Consultas de Rede", "Só leitura: não altera nada."),
        new("network.repair", CommandTab.Network, "🛠️ Manutenção & Reparo de Conexão"),
        new("apps.updates", CommandTab.Apps, "🔄 Central de Atualizações (Winget)",
            "Verifique quais programas possuem versões novas antes de atualizar."),
        new("files.disk", CommandTab.Files, "💽 Discos e Partições"),
        new("system.repair", CommandTab.System, "🛠️ Diagnóstico e Reparo do Windows"),
        new("system.power", CommandTab.System, "⚡ Energia e Desligamento Programado"),
    };

    /// <summary>Todos os comandos que não dependem de algo digitado pelo usuário.</summary>
    public static IReadOnlyList<CommandDefinition> Definitions { get; } = BuildDefinitions();

    public static CommandDefinition Get(string id) => Definitions.First(d => d.Id == id);

    private static CommandDefinition[] BuildDefinitions()
    {
        var chkdsk = Chkdsk(Environment.SystemDirectory);
        var drive = chkdsk.Arguments[0];

        return new CommandDefinition[]
        {
            // ---------- Rede: consultas ----------
            new()
            {
                Id = "ipconfig", SectionId = "network.query",
                Title = "📋 Meu Endereço IP (ipconfig)", Description = "Exibe adaptadores ativos e IPs básicos",
                Steps = new[] { CommandSpec.Create("ipconfig") }, Keywords = "ip endereço"
            },
            new()
            {
                Id = "ipconfig-all", SectionId = "network.query",
                Title = "🔍 Diagnóstico Completo (/all)", Description = "DHCP, DNS, máscara de rede e leases",
                Steps = new[] { CommandSpec.Create("ipconfig", "/all") }, Keywords = "ip dns dhcp"
            },
            new()
            {
                Id = "getmac", SectionId = "network.query",
                Title = "🏷️ Endereços Físicos MAC (getmac /v)", Description = "Lista placas e endereços físicos (MAC)",
                Steps = new[] { CommandSpec.Create("getmac", "/v") }, Keywords = "mac placa"
            },
            new()
            {
                Id = "arp", SectionId = "network.query",
                Title = "🌐 Dispositivos na Rede Local (arp -a)", Description = "Tabela com IPs e MACs da rede local",
                Steps = new[] { CommandSpec.Create("arp", "-a") }, Keywords = "rede local dispositivos"
            },
            new()
            {
                Id = "netstat", SectionId = "network.query",
                Title = "🔌 Conexões e Portas (netstat -ano)", Description = "Portas TCP/UDP abertas e PIDs ativos",
                Steps = new[] { CommandSpec.Create("netstat", "-ano") }, Keywords = "porta conexão tcp udp"
            },

            // ---------- Rede: manutenção ----------
            new()
            {
                Id = "flushdns", SectionId = "network.repair",
                Title = "🧹 Limpar Cache DNS (/flushdns)", Description = "Corrige falhas ao acessar sites",
                Steps = new[] { CommandSpec.Create("ipconfig", "/flushdns") }, Keywords = "dns cache"
            },
            new()
            {
                Id = "displaydns", SectionId = "network.repair",
                Title = "📜 Exibir Cache DNS (/displaydns)", Description = "Lista os sites memorizados recentemente",
                Steps = new[] { CommandSpec.Create("ipconfig", "/displaydns") }, Keywords = "dns cache"
            },
            new()
            {
                Id = "ip-release", SectionId = "network.repair",
                Title = "⚠️ Liberar Endereço IP (/release)", Description = "Interrompe a conexão até renovar o IP",
                Steps = new[] { IpRelease }, Risk = CommandRisk.Dangerous, RequiresAdmin = true, Keywords = "ip",
                ConfirmationTitle = "Aviso de Desconexão - ipconfig /release",
                Confirmation =
                    "⚠️ ATENÇÃO: Liberação de Endereço IP\n\n" +
                    "O comando 'ipconfig /release' interromperá imediatamente sua conexão com a rede local e a internet.\n\n" +
                    "• Impacto: Você ficará sem internet até renovar o IP (/renew).\n" +
                    "• Risco: Downloads ou conexões ativas serão interrompidas.\n\n" +
                    "Deseja liberar seu endereço de IP agora?"
            },
            new()
            {
                Id = "ip-renew", SectionId = "network.repair",
                Title = "🔄 Solicitar Novo IP (/renew)", Description = "Reconecta e solicita novo IP via DHCP",
                Steps = new[] { IpRenew }, Risk = CommandRisk.Caution, RequiresAdmin = true, Keywords = "ip dhcp",
                ConfirmationTitle = "Renovar IP - ipconfig /renew",
                Confirmation =
                    "O comando 'ipconfig /renew' solicitará um novo endereço IP ao roteador (servidor DHCP).\n\n" +
                    "Deseja renovar sua conexão?"
            },
            new()
            {
                Id = "ip-reset", SectionId = "network.repair",
                Title = "⚡ Reset Completo de Conexão", Description = "Release + Renew em sequência",
                // Mesmo que o release falhe, tenta o renew para não deixar a máquina sem IP.
                Steps = new[] { IpRelease, IpRenew }, ContinueOnFailure = true,
                Risk = CommandRisk.Dangerous, RequiresAdmin = true, Keywords = "ip release renew",
                ConfirmationTitle = "Confirmação de Reset de Conexão",
                Confirmation =
                    "⚠️ ATENÇÃO: Reset Completo de Conexão\n\n" +
                    "Esta ação executará o ciclo completo de liberação e renovação:\n" +
                    "1. ipconfig /release (interrompe o IP atual)\n" +
                    "2. ipconfig /renew (solicita um novo IP via DHCP)\n\n" +
                    "• Impacto: A conexão com a internet cairá por alguns segundos.\n\n" +
                    "Deseja prosseguir com o reset de conexão?"
            },

            // ---------- Apps: atualizações ----------
            new()
            {
                Id = "winget-list-upgrades", SectionId = "apps.updates",
                Title = "🔍 1. Verificar Atualizações Disponíveis", Description = "Apenas lista quais apps têm update (não instala nada)",
                Steps = new[] { WingetListUpgrades }, Keywords = "winget upgrade app"
            },
            new()
            {
                Id = "winget-upgrade-all", SectionId = "apps.updates",
                Title = "🔄 2. Atualizar Todos os Apps do PC", Description = "Atualiza em lote silenciosamente com aviso prévio",
                Steps = new[] { WingetUpgradeAll }, Risk = CommandRisk.Dangerous, Keywords = "winget upgrade app",
                ConfirmationTitle = "Aviso de Atualização em Lote",
                Confirmation =
                    "⚠️ ATENÇÃO: Atualização Geral em Lote\n\n" +
                    "Esta ação buscará e atualizará silenciosamente TODOS os aplicativos com novas versões no seu computador.\n\n" +
                    "• Risco: Programas que estejam abertos no momento podem fechar ou reiniciar durante a atualização.\n" +
                    "• Recomendação: Feche programas importantes antes de atualizar.\n\n" +
                    "Deseja iniciar a atualização de todos os programas agora?"
            },

            // ---------- Arquivos: discos ----------
            new()
            {
                Id = "disk-space", SectionId = "files.disk",
                Title = "📊 Espaço em Disco (Get-Volume)", Description = "Tamanho e espaço livre em cada partição",
                Steps = new[] { DiskSpace() }, Keywords = "disco espaço partição volume"
            },

            // ---------- Sistema: reparo ----------
            new()
            {
                Id = "sfc", SectionId = "system.repair",
                Title = "⚠️ sfc /scannow", Description = "Verifica e repara arquivos corrompidos (5-15 min)",
                Steps = new[] { Sfc }, Risk = CommandRisk.Caution, RequiresAdmin = true, Keywords = "reparo integridade",
                ConfirmationTitle = "Aviso de Segurança - SFC Scannow",
                Confirmation =
                    "⚠️ ATENÇÃO: Verificação de Integridade de Arquivos (SFC)\n\n" +
                    "O comando 'sfc /scannow' examinará todos os arquivos protegidos do sistema e substituirá os arquivos corrompidos por uma cópia em cache.\n\n" +
                    "• Duração: Pode levar de 5 a 20 minutos dependendo do seu computador.\n" +
                    "• Impacto: Alto consumo de disco e processador durante o teste.\n\n" +
                    "Deseja iniciar a verificação agora?"
            },
            new()
            {
                Id = "dism", SectionId = "system.repair",
                Title = "⚠️ DISM RestoreHealth", Description = "Restaura a imagem do Windows via Update (10-25 min)",
                Steps = new[] { DismRestoreHealth }, Risk = CommandRisk.Caution, RequiresAdmin = true, Keywords = "reparo imagem",
                ConfirmationTitle = "Aviso de Segurança - DISM RestoreHealth",
                Confirmation =
                    "⚠️ ATENÇÃO: Reparação da Imagem do Windows (DISM)\n\n" +
                    "O comando 'DISM RestoreHealth' fará o download e reparo de componentes essenciais da imagem do Windows diretamente pelo Windows Update.\n\n" +
                    "• Duração: Pode demorar entre 10 e 30 minutos.\n" +
                    "• Impacto: Alto consumo de banda de internet e processamento.\n" +
                    "• Requisito: Conexão ativa com a internet.\n\n" +
                    "Deseja iniciar a restauração agora?"
            },
            new()
            {
                Id = "chkdsk", SectionId = "system.repair",
                Title = $"🔍 Verificar Disco (chkdsk {drive})", Description = "Verifica o sistema de arquivos (somente leitura)",
                Steps = new[] { chkdsk }, Risk = CommandRisk.Caution, RequiresAdmin = true, Keywords = "disco reparo",
                ConfirmationTitle = "Diagnóstico de Disco",
                Confirmation =
                    "⚠️ AVISO: Verificação de Disco (CHKDSK)\n\n" +
                    $"O comando 'chkdsk {drive}' verificará o sistema de arquivos da unidade do Windows em modo de leitura.\n\n" +
                    "• Duração: 2 a 5 minutos.\n" +
                    "• Impacto: Acesso intensivo ao disco.\n\n" +
                    "Deseja iniciar o diagnóstico do disco?"
            },
            new()
            {
                Id = "systeminfo", SectionId = "system.repair",
                Title = "💻 systeminfo", Description = "Especificações do PC, RAM, BIOS e versão do Windows",
                Steps = new[] { CommandSpec.Create("systeminfo") }, Keywords = "sistema hardware"
            },
            new()
            {
                Id = "tasklist", SectionId = "system.repair",
                Title = "📋 tasklist", Description = "Lista todos os processos ativos e consumo",
                Steps = new[] { CommandSpec.Create("tasklist") }, Keywords = "processos task"
            },

            // ---------- Sistema: energia ----------
            new()
            {
                Id = "shutdown-1h", SectionId = "system.power",
                Title = "⏰ Desligar em 1 Hora", Description = "shutdown /s /t 3600",
                Steps = new[] { ShutdownIn(TimeSpan.FromHours(1)) }, Risk = CommandRisk.Dangerous, Keywords = "desligar energia",
                ConfirmationTitle = "Agendar Desligamento",
                Confirmation =
                    "⚠️ ATENÇÃO: Desligamento Agendado\n\n" +
                    "O computador será desligado automaticamente em 1 HORA (3600 segundos).\n\n" +
                    "Deseja realmente agendar o desligamento?"
            },
            new()
            {
                Id = "shutdown-abort", SectionId = "system.power",
                Title = "🛑 Cancelar Desligamento", Description = "Cancela qualquer desligamento agendado (shutdown /a)",
                Steps = new[] { ShutdownAbort }, Keywords = "desligar energia cancelar"
            },
        };
    }
}
