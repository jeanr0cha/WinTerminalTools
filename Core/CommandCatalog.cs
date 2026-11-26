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
    public static CommandSpec Nslookup(string host) => CommandSpec.Create("nslookup", host);

    /// <summary>
    /// Script PowerShell fixo, sem barra de progresso e com tabelas largas (o padrão corta em 80 colunas).
    /// Nunca deve receber texto digitado que não tenha passado pelo InputValidator.
    /// </summary>
    private static CommandSpec PowerShell(string script) =>
        CommandSpec.Create("powershell", "-NoProfile", "-NonInteractive", "-Command",
            $"$ProgressPreference='SilentlyContinue'; {script}");

    /// <summary>Testa se uma porta TCP responde. O host já vem validado (só letras, dígitos, '.', '-' e ':').</summary>
    public static CommandSpec TestPort(string host, int port)
    {
        if (InputValidator.NormalizeHost(host) != host)
            throw new ArgumentException("Host não validado.", nameof(host));

        return PowerShell(
            $"Test-NetConnection -ComputerName '{host}' -Port {port.ToString(CultureInfo.InvariantCulture)} -WarningAction SilentlyContinue | " +
            "Format-List ComputerName, RemoteAddress, RemotePort, TcpTestSucceeded");
    }

    public static CommandSpec WifiProfiles => CommandSpec.Create("netsh", "wlan", "show", "profiles");

    /// <summary>Mostra a senha de uma rede salva (key=clear). O nome vai como um único argumento.</summary>
    public static CommandSpec WifiPassword(string profile) =>
        CommandSpec.Create("netsh", "wlan", "show", "profile", $"name={profile}", "key=clear");

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

    public static CommandSpec WingetUninstall(string id) => Winget("uninstall", "--id", id, "-e", WingetAgreements2);

    public static CommandSpec WingetList => Winget("list", WingetAgreements2);

    /// <summary>Salva a lista de programas instalados num JSON (para reinstalar depois de formatar).</summary>
    public static CommandSpec WingetExport(string file) => Winget("export", "-o", file, WingetAgreements2);

    public static CommandSpec WingetImport(string file) =>
        Winget("import", "-i", file, "--ignore-unavailable", WingetAgreements1, WingetAgreements2);

    public static CommandSpec DiskSpace() =>
        PowerShell("Get-Volume | Format-Table -AutoSize DriveLetter, FriendlyName, FileSystemType, HealthStatus, SizeRemaining, Size | Out-String -Width 200");

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

    public static CommandSpec RestartIn(TimeSpan delay) =>
        CommandSpec.Create("shutdown", "/r", "/t", ((int)delay.TotalSeconds).ToString(CultureInfo.InvariantCulture));

    public static CommandSpec ShutdownAbort => CommandSpec.Create("shutdown", "/a");

    public static CommandSpec OpenExplorer(string folder) => CommandSpec.Create("explorer.exe", folder);

    /// <summary>
    /// Lista o conteúdo de uma pasta. O dir é interno do cmd, então a pasta entra como diretório de
    /// trabalho e nunca na linha de comando (um nome como "a&amp;calc" não vira outro comando).
    /// </summary>
    public static CommandSpec Dir(string folder) =>
        CommandSpec.Create("cmd.exe", "/c", "dir") with { WorkingDirectory = folder };

    // ================= CATÁLOGO DA INTERFACE =================

    /// <summary>Onde o relatório de bateria é salvo (pasta Documentos do usuário).</summary>
    public static string BatteryReportPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "relatorio-bateria.html");

    /// <summary>Cards das abas, na ordem em que aparecem.</summary>
    public static IReadOnlyList<CommandSection> Sections { get; } = new CommandSection[]
    {
        new("network.query", CommandTab.Network, "📊 Consultas de Rede", "Só leitura: não altera nada."),
        new("network.repair", CommandTab.Network, "🛠️ Manutenção & Reparo de Conexão"),
        new("network.wifi", CommandTab.Network, "📶 Wi-Fi"),
        new("apps.updates", CommandTab.Apps, "🔄 Programas e Atualizações (Winget)",
            "Verifique quais programas possuem versões novas antes de atualizar."),
        new("files.disk", CommandTab.Files, "💽 Discos e Partições"),
        new("files.cleanup", CommandTab.Files, "🧹 Limpeza", "Feche os programas abertos antes de limpar."),
        new("system.info", CommandTab.System, "💻 Informações do Sistema", "Só leitura: não altera nada."),
        new("system.repair", CommandTab.System, "🛠️ Diagnóstico e Reparo do Windows"),
        new("system.power", CommandTab.System, "⚡ Energia"),
    };

    /// <summary>Todos os comandos que não dependem de algo digitado pelo usuário.</summary>
    public static IReadOnlyList<CommandDefinition> Definitions { get; } = BuildDefinitions();

    public static CommandDefinition Get(string id) => Definitions.First(d => d.Id == id);

    private static CommandDefinition[] BuildDefinitions()
    {
        var chkdsk = Chkdsk(Environment.SystemDirectory);
        var drive = chkdsk.Arguments[0];
        var flushDns = CommandSpec.Create("ipconfig", "/flushdns");
        var winsockReset = CommandSpec.Create("netsh", "winsock", "reset");
        var tcpIpReset = CommandSpec.Create("netsh", "int", "ip", "reset");

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
            new()
            {
                Id = "route", SectionId = "network.query",
                Title = "🧭 Tabela de Rotas (route print)", Description = "Gateway padrão e rotas de rede",
                Steps = new[] { CommandSpec.Create("route", "print") }, Keywords = "rota gateway"
            },
            new()
            {
                Id = "proxy", SectionId = "network.query",
                Title = "🛰️ Proxy do Sistema (winhttp)", Description = "Mostra se há proxy configurado",
                Steps = new[] { CommandSpec.Create("netsh", "winhttp", "show", "proxy") }, Keywords = "proxy"
            },

            // ---------- Rede: manutenção ----------
            new()
            {
                Id = "flushdns", SectionId = "network.repair",
                Title = "🧹 Limpar Cache DNS (/flushdns)", Description = "Corrige falhas ao acessar sites",
                Steps = new[] { flushDns }, Keywords = "dns cache"
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
            new()
            {
                Id = "winsock-reset", SectionId = "network.repair",
                Title = "🧯 Reset do Winsock", Description = "Corrige internet que não navega · exige reiniciar",
                Steps = new[] { winsockReset }, Risk = CommandRisk.Dangerous, RequiresAdmin = true, Keywords = "rede internet reparo",
                ConfirmationTitle = "Reset do Winsock",
                Confirmation =
                    "⚠️ ATENÇÃO: Reset do Winsock (netsh winsock reset)\n\n" +
                    "Restaura o catálogo de rede do Windows ao padrão. Resolve muitos casos de \"conectado, mas sem internet\".\n\n" +
                    "• Impacto: VPNs, proxies e alguns antivírus podem precisar ser reinstalados ou reconfigurados.\n" +
                    "• É necessário REINICIAR o computador para concluir.\n\n" +
                    "Deseja resetar o Winsock agora?"
            },
            new()
            {
                Id = "tcpip-reset", SectionId = "network.repair",
                Title = "🧯 Reset do TCP/IP", Description = "Volta as configurações de IP ao padrão · exige reiniciar",
                Steps = new[] { tcpIpReset }, Risk = CommandRisk.Dangerous, RequiresAdmin = true, Keywords = "rede internet reparo ip",
                ConfirmationTitle = "Reset do TCP/IP",
                Confirmation =
                    "⚠️ ATENÇÃO: Reset do TCP/IP (netsh int ip reset)\n\n" +
                    "Reescreve as configurações de TCP/IP do Windows com os valores padrão.\n\n" +
                    "• Impacto: IPs fixos configurados manualmente serão apagados (volta para DHCP).\n" +
                    "• É necessário REINICIAR o computador para concluir.\n\n" +
                    "Deseja resetar o TCP/IP agora?"
            },
            new()
            {
                Id = "network-full-repair", SectionId = "network.repair",
                Title = "🚑 Reparo Completo da Rede", Description = "DNS + Winsock + TCP/IP · exige reiniciar",
                Steps = new[] { flushDns, winsockReset, tcpIpReset }, ContinueOnFailure = true,
                Risk = CommandRisk.Dangerous, RequiresAdmin = true, Keywords = "rede internet reparo dns winsock",
                ConfirmationTitle = "Reparo Completo da Rede",
                Confirmation =
                    "⚠️ ATENÇÃO: Reparo Completo da Rede\n\n" +
                    "Executa em sequência:\n" +
                    "1. ipconfig /flushdns (limpa o cache DNS)\n" +
                    "2. netsh winsock reset (restaura o catálogo de rede)\n" +
                    "3. netsh int ip reset (volta o TCP/IP ao padrão)\n\n" +
                    "• Impacto: IPs fixos são apagados; VPNs e proxies podem precisar ser reconfigurados.\n" +
                    "• É necessário REINICIAR o computador para concluir.\n\n" +
                    "Deseja executar o reparo completo?"
            },

            // ---------- Rede: Wi-Fi ----------
            new()
            {
                Id = "wifi-interfaces", SectionId = "network.wifi",
                Title = "📶 Conexão Wi-Fi Atual", Description = "Rede conectada, sinal e velocidade",
                Steps = new[] { CommandSpec.Create("netsh", "wlan", "show", "interfaces") }, Keywords = "wifi wi-fi sinal wireless"
            },
            new()
            {
                Id = "wifi-profiles", SectionId = "network.wifi",
                Title = "📃 Redes Wi-Fi Salvas", Description = "Lista as redes que este PC já conectou",
                Steps = new[] { WifiProfiles }, Keywords = "wifi wi-fi perfil wireless"
            },

            // ---------- Apps: programas e atualizações ----------
            new()
            {
                Id = "winget-list-upgrades", SectionId = "apps.updates",
                Title = "🔍 Verificar Atualizações", Description = "Lista quais apps têm update (não instala nada)",
                Steps = new[] { WingetListUpgrades }, Keywords = "winget upgrade app"
            },
            new()
            {
                Id = "winget-upgrade-all", SectionId = "apps.updates",
                Title = "🔄 Atualizar Todos os Apps", Description = "Atualiza em lote silenciosamente com aviso prévio",
                Steps = new[] { WingetUpgradeAll }, Risk = CommandRisk.Dangerous, Keywords = "winget upgrade app",
                ConfirmationTitle = "Aviso de Atualização em Lote",
                Confirmation =
                    "⚠️ ATENÇÃO: Atualização Geral em Lote\n\n" +
                    "Esta ação buscará e atualizará silenciosamente TODOS os aplicativos com novas versões no seu computador.\n\n" +
                    "• Risco: Programas que estejam abertos no momento podem fechar ou reiniciar durante a atualização.\n" +
                    "• Recomendação: Feche programas importantes antes de atualizar.\n\n" +
                    "Deseja iniciar a atualização de todos os programas agora?"
            },
            new()
            {
                Id = "winget-list", SectionId = "apps.updates",
                Title = "📦 Programas Instalados", Description = "Lista tudo que está instalado, com o ID do winget",
                Steps = new[] { WingetList }, Keywords = "winget list app instalados id"
            },

            // ---------- Arquivos: discos ----------
            new()
            {
                Id = "disk-space", SectionId = "files.disk",
                Title = "📊 Espaço em Disco (Get-Volume)", Description = "Tamanho e espaço livre em cada partição",
                Steps = new[] { DiskSpace() }, Keywords = "disco espaço partição volume"
            },
            new()
            {
                Id = "physical-disks", SectionId = "files.disk",
                Title = "🩺 Saúde dos Discos", Description = "Tipo (SSD/HD) e estado de cada disco físico",
                Steps = new[]
                {
                    PowerShell("Get-PhysicalDisk | Format-Table -AutoSize FriendlyName, MediaType, BusType, HealthStatus, OperationalStatus, " +
                               "@{n='Tamanho (GB)';e={[math]::Round($_.Size/1GB)}} | Out-String -Width 200")
                },
                Keywords = "disco ssd hd saúde smart"
            },

            // ---------- Arquivos: limpeza ----------
            new()
            {
                Id = "temp-clean", SectionId = "files.cleanup",
                Title = "🧹 Limpar Arquivos Temporários", Description = "Apaga a pasta %TEMP% do usuário (arquivos em uso ficam)",
                Steps = new[]
                {
                    PowerShell(
                        "$t=$env:TEMP; " +
                        "$antes=(Get-ChildItem $t -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum; " +
                        "Get-ChildItem $t -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; " +
                        "$depois=(Get-ChildItem $t -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum; " +
                        "'Pasta: {0}' -f $t; 'Liberado: {0:N1} MB (arquivos em uso foram mantidos)' -f (($antes-$depois)/1MB)")
                },
                Risk = CommandRisk.Dangerous, Keywords = "temp temporários limpeza espaço",
                ConfirmationTitle = "Limpar Arquivos Temporários",
                Confirmation =
                    "⚠️ ATENÇÃO: Limpeza de Arquivos Temporários\n\n" +
                    "Apaga o conteúdo da sua pasta de temporários (%TEMP%). Arquivos em uso são ignorados.\n\n" +
                    "• Risco: Programas ou instaladores abertos agora podem perder arquivos de trabalho.\n" +
                    "• Recomendação: Feche os outros programas antes de continuar.\n\n" +
                    "Deseja limpar os arquivos temporários?"
            },
            new()
            {
                Id = "recycle-bin", SectionId = "files.cleanup",
                Title = "🗑️ Esvaziar Lixeira", Description = "Apaga definitivamente os itens da lixeira",
                Steps = new[] { PowerShell("Clear-RecycleBin -Force -ErrorAction SilentlyContinue; 'Lixeira esvaziada.'") },
                Risk = CommandRisk.Dangerous, Keywords = "lixeira limpeza espaço",
                ConfirmationTitle = "Esvaziar Lixeira",
                Confirmation =
                    "⚠️ ATENÇÃO: Esvaziar Lixeira\n\n" +
                    "Todos os itens da lixeira (de todas as unidades) serão apagados DEFINITIVAMENTE.\n\n" +
                    "Deseja esvaziar a lixeira?"
            },

            // ---------- Sistema: informações ----------
            new()
            {
                Id = "systeminfo", SectionId = "system.info",
                Title = "💻 systeminfo", Description = "Especificações do PC, RAM, BIOS e versão do Windows",
                Steps = new[] { CommandSpec.Create("systeminfo") }, Keywords = "sistema hardware"
            },
            new()
            {
                Id = "tasklist", SectionId = "system.info",
                Title = "📋 tasklist", Description = "Lista todos os processos ativos e consumo",
                Steps = new[] { CommandSpec.Create("tasklist") }, Keywords = "processos task"
            },
            new()
            {
                Id = "uptime", SectionId = "system.info",
                Title = "⏱️ Tempo Ligado", Description = "Desde quando o Windows está ligado (último reinício)",
                Steps = new[]
                {
                    PowerShell("$b=(Get-CimInstance Win32_OperatingSystem).LastBootUpTime; $u=(Get-Date)-$b; " +
                               "'Ligado desde {0:dd/MM/yyyy HH:mm} ({1} dia(s), {2} h e {3} min)' -f $b,$u.Days,$u.Hours,$u.Minutes")
                },
                Keywords = "uptime ligado boot reinício"
            },
            new()
            {
                Id = "startup-apps", SectionId = "system.info",
                Title = "🚀 Programas na Inicialização", Description = "O que abre junto com o Windows",
                Steps = new[]
                {
                    PowerShell("Get-CimInstance Win32_StartupCommand | Sort-Object Name | " +
                               "Format-Table -AutoSize Name, Command, Location | Out-String -Width 300")
                },
                Keywords = "inicialização startup boot lento"
            },
            new()
            {
                Id = "drivers", SectionId = "system.info",
                Title = "🧩 Drivers Instalados", Description = "Lista os drivers e quando foram instalados",
                Steps = new[] { CommandSpec.Create("driverquery") }, Keywords = "drivers driverquery"
            },
            new()
            {
                Id = "battery-report", SectionId = "system.info",
                Title = "🔋 Relatório da Bateria", Description = "Gera um HTML com a saúde da bateria em Documentos",
                Steps = new[] { CommandSpec.Create("powercfg", "/batteryreport", "/output", BatteryReportPath) },
                Keywords = "bateria notebook powercfg"
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
                Id = "restore-point-create", SectionId = "system.repair",
                Title = "🛟 Criar Ponto de Restauração", Description = "Salva o estado atual do Windows antes de mudanças",
                Steps = new[]
                {
                    PowerShell("Checkpoint-Computer -Description 'WinTerminalTools' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; " +
                               "'Ponto de restauração criado.'")
                },
                Risk = CommandRisk.Caution, RequiresAdmin = true, Keywords = "restauração backup restore",
                ConfirmationTitle = "Criar Ponto de Restauração",
                Confirmation =
                    "Criar um ponto de restauração do sistema agora?\n\n" +
                    "• Duração: 1 a 3 minutos.\n" +
                    "• Observação: o Windows só permite um ponto a cada 24 horas por padrão, e a Proteção do Sistema " +
                    "precisa estar ativada na unidade C:.\n\n" +
                    "Deseja continuar?"
            },
            new()
            {
                Id = "restore-point-list", SectionId = "system.repair",
                Title = "📚 Pontos de Restauração", Description = "Lista os pontos de restauração existentes",
                Steps = new[]
                {
                    PowerShell("$r=Get-ComputerRestorePoint; if(-not $r){'Nenhum ponto de restauração encontrado.'} else { " +
                               "$r | Format-Table -AutoSize SequenceNumber, @{n='Data';e={$_.ConvertToDateTime($_.CreationTime)}}, Description | Out-String -Width 200 }")
                },
                RequiresAdmin = true, Keywords = "restauração backup restore"
            },

            // ---------- Sistema: energia ----------
            new()
            {
                Id = "shutdown-abort", SectionId = "system.power",
                Title = "🛑 Cancelar Desligamento", Description = "Cancela desligamento ou reinício agendado",
                Steps = new[] { ShutdownAbort }, Keywords = "desligar reiniciar energia cancelar"
            },
            new()
            {
                Id = "lock", SectionId = "system.power",
                Title = "🔒 Bloquear o PC", Description = "Vai para a tela de bloqueio (como Win + L)",
                Steps = new[] { CommandSpec.Create("rundll32.exe", "user32.dll,LockWorkStation") }, Keywords = "bloquear energia"
            },
            new()
            {
                Id = "hibernate", SectionId = "system.power",
                Title = "🌙 Hibernar Agora", Description = "Salva tudo e desliga (precisa da hibernação ativa)",
                Steps = new[] { CommandSpec.Create("shutdown", "/h") }, Risk = CommandRisk.Dangerous, Keywords = "hibernar energia",
                ConfirmationTitle = "Hibernar",
                Confirmation =
                    "⚠️ O computador vai HIBERNAR imediatamente.\n\n" +
                    "Os programas abertos são salvos e voltam ao ligar, mas conexões e downloads serão interrompidos.\n\n" +
                    "Deseja hibernar agora?"
            },
            new()
            {
                Id = "power-plans", SectionId = "system.power",
                Title = "⚙️ Planos de Energia", Description = "Lista os planos e qual está ativo (powercfg /list)",
                Steps = new[] { CommandSpec.Create("powercfg", "/list") }, Keywords = "energia plano desempenho"
            },
        };
    }
}
