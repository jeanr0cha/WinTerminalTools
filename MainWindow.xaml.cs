using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using WinTerminalTools.Core;

namespace WinTerminalTools;

public partial class MainWindow : Window
{
    private const int MaxConsoleChars = 400_000;
    private const int KeepConsoleChars = 300_000;

    private CancellationTokenSource? _currentCts;
    private bool _isRunning;
    private readonly bool _isAdmin;
    private readonly CheckBox[] _appCheckBoxes;
    private readonly ConcurrentQueue<string> _pendingOutput = new();
    private readonly DispatcherTimer _flushTimer;

    public MainWindow()
    {
        InitializeComponent();

        _appCheckBoxes = new[]
        {
            AppChrome, AppBrave, AppFirefox, AppOperaGX,
            AppSpotify, AppDiscord, AppVLC, AppSteam, AppWhatsApp,
            AppVSCode, AppGit, App7Zip, AppNotepadPlusPlus, AppDocker
        };

        _flushTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(60)
        };
        _flushTimer.Tick += (_, _) => FlushOutput();

        _isAdmin = EhAdministrador();

        CarregarIconeSeguro();
        VerificarPermissaoAdministrador();
        ExibirMensagemBoasVindas();
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _currentCts?.Cancel(); } catch { }
        base.OnClosed(e);
    }

    private void CarregarIconeSeguro()
    {
        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/Icone.ico");
            this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
        }
        catch
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icone.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(iconPath));
                }
            }
            catch { }
        }
    }

    private static bool EhAdministrador()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void VerificarPermissaoAdministrador()
    {
        if (_isAdmin)
        {
            TxtAdminStatus.Text = "Administrador";
            TxtAdminStatus.Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183));
            AdminBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(20, 51, 36));
            AdminBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            AdminStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            BtnReiniciarAdmin.Visibility = Visibility.Collapsed;
        }
        else
        {
            TxtAdminStatus.Text = "Usuário Padrão";
            TxtAdminStatus.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
            AdminBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(55, 40, 15));
            AdminBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            AdminStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            BtnReiniciarAdmin.Visibility = Visibility.Visible;
        }
    }

    private void ExibirMensagemBoasVindas()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                WinTerminalTools v1.0 (.NET 10)                   ║");
        sb.AppendLine("║       Central de Linha de Comando, Rede e Winget                 ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine("• Escolha um comando nas categorias ao lado para executar em tempo real.");
        sb.AppendLine("• Use a aba 'Instalação Rápida & Winget' para verificar updates ou instalar apps.");
        sb.AppendLine("• Comandos com impacto no sistema exigem confirmação com aviso de risco.");
        sb.AppendLine("• Todo comando pode ser interrompido a qualquer instante clicando em [Cancelar Execução].");
        sb.AppendLine();
        sb.AppendLine("----------------------------------------------------------------------");
        TxtConsoleOutput.Text = sb.ToString();
    }

    // ================= IMPEDIR ROLAGEM VERTICAL INVISÍVEL NOS CAMPOS DE TEXTO =================
    private void TextBox_BloquearScrollMouse(object sender, MouseWheelEventArgs e)
    {
        // Impede que a rodinha do mouse role o texto verticalmente para fora do campo visível
        e.Handled = true;
    }

    // ================= CONTROLE DE ABAS =================
    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelRede == null || PanelApps == null || PanelPastas == null || PanelSistema == null)
            return;

        PanelRede.Visibility = Visibility.Collapsed;
        PanelApps.Visibility = Visibility.Collapsed;
        PanelPastas.Visibility = Visibility.Collapsed;
        PanelSistema.Visibility = Visibility.Collapsed;

        if (MainTabControl.SelectedItem == TabRede)
            PanelRede.Visibility = Visibility.Visible;
        else if (MainTabControl.SelectedItem == TabApps)
            PanelApps.Visibility = Visibility.Visible;
        else if (MainTabControl.SelectedItem == TabPastas)
            PanelPastas.Visibility = Visibility.Visible;
        else if (MainTabControl.SelectedItem == TabSistema)
            PanelSistema.Visibility = Visibility.Visible;
    }

    // ================= FILTRO / BUSCA GLOBAL =================
    private IEnumerable<ScrollViewer> Paineis => new[] { PanelRede, PanelApps, PanelPastas, PanelSistema };

    private void TxtBuscaGlobal_TextChanged(object sender, TextChangedEventArgs e)
    {
        string texto = TxtBuscaGlobal.Text;
        bool temTexto = !string.IsNullOrEmpty(texto);
        PlaceholderBusca.Visibility = temTexto ? Visibility.Collapsed : Visibility.Visible;
        BtnLimparBusca.Visibility = temTexto ? Visibility.Visible : Visibility.Collapsed;

        string filtro = texto.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(filtro))
        {
            AplicarFiltro(null);
            return;
        }

        // Alterna automaticamente para a aba relevante
        if (filtro.Contains("winget") || filtro.Contains("app") || filtro.Contains("chrome") || filtro.Contains("steam") || filtro.Contains("spotify") || filtro.Contains("upgrade"))
        {
            MainTabControl.SelectedItem = TabApps;
        }
        else if (filtro.Contains("pasta") || filtro.Contains("disco") || filtro.Contains("dir") || filtro.Contains("explorer") || filtro.Contains("mkdir"))
        {
            MainTabControl.SelectedItem = TabPastas;
        }
        else if (filtro.Contains("sfc") || filtro.Contains("dism") || filtro.Contains("reparo") || filtro.Contains("system") || filtro.Contains("task") || filtro.Contains("chkdsk") || filtro.Contains("desligar"))
        {
            MainTabControl.SelectedItem = TabSistema;
        }
        else if (filtro.Contains("ip") || filtro.Contains("dns") || filtro.Contains("ping") || filtro.Contains("tracert") || filtro.Contains("mac") || filtro.Contains("arp"))
        {
            MainTabControl.SelectedItem = TabRede;
        }

        AplicarFiltro(filtro);
    }

    private void BtnLimparBusca_Click(object sender, RoutedEventArgs e)
    {
        TxtBuscaGlobal.Clear();
        TxtBuscaGlobal.Focus();
    }

    /// <summary>
    /// Escurece os botões que não combinam com o filtro (por Tag ou pelo texto exibido).
    /// Usa a árvore lógica, que existe mesmo para abas ainda não renderizadas.
    /// </summary>
    private void AplicarFiltro(string? filtro)
    {
        foreach (var painel in Paineis)
        {
            foreach (var btn in EnumerarBotoes(painel))
            {
                bool match = filtro == null ||
                             (btn.Tag is string tag && tag.Contains(filtro, StringComparison.OrdinalIgnoreCase)) ||
                             TextoDe(btn).Contains(filtro, StringComparison.OrdinalIgnoreCase);
                btn.Opacity = match ? 1.0 : 0.25;
            }
        }
    }

    private static IEnumerable<Button> EnumerarBotoes(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is Button btn)
                yield return btn;

            foreach (var inner in EnumerarBotoes(child))
                yield return inner;
        }
    }

    private static string TextoDe(DependencyObject root)
    {
        var sb = new StringBuilder();
        void Visitar(DependencyObject node)
        {
            if (node is TextBlock tb)
                sb.Append(tb.Text).Append(' ');
            else if (node is ContentControl { Content: string s })
                sb.Append(s).Append(' ');

            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
                Visitar(child);
        }
        Visitar(root);
        return sb.ToString();
    }

    // ================= COMANDOS DE REDE =================
    private async void BtnComando_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string comando)
        {
            await ExecutarAsync(CommandSpec.FromStaticCommand(comando));
        }
    }

    private void ChipHost_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string host)
        {
            TxtHostRede.Text = host;
        }
    }

    private async void BtnPing_Click(object sender, RoutedEventArgs e)
    {
        var host = ObterHostValido("Ping");
        if (host != null)
            await ExecutarAsync(CommandCatalog.Ping(host));
    }

    private async void BtnTracert_Click(object sender, RoutedEventArgs e)
    {
        var host = ObterHostValido("Tracert");
        if (host != null)
            await ExecutarAsync(CommandCatalog.Tracert(host));
    }

    private string? ObterHostValido(string operacao)
    {
        var host = InputValidator.NormalizeHost(TxtHostRede.Text);
        if (host == null)
        {
            MessageBox.Show(
                $"Digite um host ou IP válido para o teste de {operacao} (ex: google.com, 8.8.8.8).",
                "Host Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        return host;
    }

    private async void BtnRelease_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "⚠️ ATENÇÃO: Liberação de Endereço IP\n\n" +
            "O comando 'ipconfig /release' interromperá imediatamente sua conexão com a rede local e a internet.\n\n" +
            "• Impacto: Você ficará sem internet até renovar o IP (/renew).\n" +
            "• Risco: Downloads ou conexões ativas serão interrompidas.\n\n" +
            "Deseja liberar seu endereço de IP agora?",
            "Aviso de Desconexão - ipconfig /release",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await ExecutarAsync(CommandCatalog.IpRelease);
        }
    }

    private async void BtnRenew_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "O comando 'ipconfig /renew' solicitará um novo endereço IP ao roteador (servidor DHCP).\n\nDeseja renovar sua conexão?",
            "Renovar IP - ipconfig /renew",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await ExecutarAsync(CommandCatalog.IpRenew);
        }
    }

    private async void BtnRenovarCompleto_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "⚠️ ATENÇÃO: Reset Completo de Conexão\n\n" +
            "Esta ação executará o ciclo completo de liberação e renovação:\n" +
            "1. ipconfig /release (interrompe o IP atual)\n" +
            "2. ipconfig /renew (solicita um novo IP via DHCP)\n\n" +
            "• Impacto: A conexão com a internet cairá por alguns segundos.\n\n" +
            "Deseja prosseguir com o reset de conexão?",
            "Confirmação de Reset de Conexão",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // Mesmo que o release falhe, tenta o renew para não deixar a máquina sem IP.
            await ExecutarSequenciaAsync(new[] { CommandCatalog.IpRelease, CommandCatalog.IpRenew }, pararNaFalha: false);
        }
    }

    // ================= WINGET: CENTRAL DE ATUALIZAÇÕES =================
    private async void BtnVerificarAtualizacoes_Click(object sender, RoutedEventArgs e)
    {
        // Apenas lista quais programas possuem atualizações disponíveis sem instalar nada!
        await ExecutarAsync(CommandCatalog.WingetListUpgrades);
    }

    private async void BtnUpgradeAll_Click(object sender, RoutedEventArgs e)
    {
        var confirmacao = MessageBox.Show(
            "⚠️ ATENÇÃO: Atualização Geral em Lote\n\n" +
            "Esta ação buscará e atualizará silenciosamente TODOS os aplicativos com novas versões no seu computador.\n\n" +
            "• Risco: Programas que estejam abertos no momento podem fechar ou reiniciar durante a atualização.\n" +
            "• Recomendação: Feche programas importantes antes de atualizar.\n\n" +
            "Deseja iniciar a atualização de todos os programas agora?",
            "Aviso de Atualização em Lote",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmacao == MessageBoxResult.Yes)
        {
            await ExecutarAsync(CommandCatalog.WingetUpgradeAll);
        }
    }

    private async void BtnUpgradeEspecifico_Click(object sender, RoutedEventArgs e)
    {
        string appId = TxtAppIdUpgrade.Text.Trim();
        if (!InputValidator.IsValidWingetId(appId))
        {
            MessageBox.Show(
                "Digite um ID de aplicativo válido (ex: Git.Git, Google.Chrome). Use apenas letras, números, '.', '_', '+' e '-'.",
                "ID Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await ExecutarAsync(CommandCatalog.WingetUpgrade(appId));
    }

    private async void BtnBuscarWinget_Click(object sender, RoutedEventArgs e)
    {
        var busca = InputValidator.NormalizeSearchTerm(TxtPesquisaWinget.Text);
        if (busca == null)
        {
            MessageBox.Show(
                "Digite o nome ou parte do nome do programa que deseja procurar no Winget (sem começar com '-').",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await ExecutarAsync(CommandCatalog.WingetSearch(busca));
    }

    // ================= WINGET: INSTALAÇÃO EM LOTE =================
    private void BtnMarcarTodos_Click(object sender, RoutedEventArgs e)
    {
        SetAllCheckBoxes(true);
    }

    private void BtnDesmarcarTodos_Click(object sender, RoutedEventArgs e)
    {
        SetAllCheckBoxes(false);
    }

    private void SetAllCheckBoxes(bool isChecked)
    {
        foreach (var chk in _appCheckBoxes)
        {
            chk.IsChecked = isChecked;
        }
    }

    private async void BtnInstalarApps_Click(object sender, RoutedEventArgs e)
    {
        var selecionados = _appCheckBoxes
            .Where(c => c.IsChecked == true && c.Tag is string id && InputValidator.IsValidWingetId(id))
            .Select(c => (string)c.Tag)
            .ToList();

        if (selecionados.Count == 0)
        {
            MessageBox.Show("Nenhum aplicativo foi selecionado. Marque os aplicativos desejados nas caixas de seleção.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmacao = MessageBox.Show(
            $"Deseja instalar {selecionados.Count} aplicativo(s) selecionado(s) via Winget?\n\n" +
            "Os downloads e instalações serão executados em sequência com acompanhamento no console.",
            "Confirmar Instalação em Lote",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmacao != MessageBoxResult.Yes)
            return;

        // Uma falha em um app não impede a instalação dos demais; o botão Cancelar interrompe o lote todo.
        await ExecutarSequenciaAsync(selecionados.Select(CommandCatalog.WingetInstall).ToList(), pararNaFalha: false);
    }

    // ================= PASTAS E ARQUIVOS =================
    private void BtnCriarPasta_Click(object sender, RoutedEventArgs e)
    {
        var caminho = InputValidator.NormalizeFolderPath(TxtCaminhoPasta.Text);
        if (caminho == null)
        {
            MessageBox.Show(
                "Digite um caminho absoluto e válido para a pasta (ex: C:\\MinhaNovaPasta).",
                "Caminho Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (Directory.Exists(caminho))
            {
                Escrever($"\nℹ A pasta já existe: {caminho}\n");
            }
            else
            {
                Directory.CreateDirectory(caminho);
                Escrever($"\n✔ Pasta criada: {caminho}\n");
            }
        }
        catch (Exception ex)
        {
            Escrever($"\n❌ Não foi possível criar a pasta: {ex.Message}\n");
        }

        FlushOutput();
    }

    private async void BtnEspacoDisco_Click(object sender, RoutedEventArgs e)
    {
        // Get-Volume é moderno e nativo do Windows 10 e 11 (substitui o antigo wmic descontinuado)
        await ExecutarAsync(CommandCatalog.DiskSpace());
    }

    private void BtnAbrirExplorer_Click(object sender, RoutedEventArgs e)
    {
        // Abre a pasta digitada (se existir); senão, a pasta do usuário.
        var digitado = InputValidator.NormalizeFolderPath(TxtCaminhoPasta.Text);
        string caminho = digitado != null && Directory.Exists(digitado)
            ? digitado
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        try
        {
            var psi = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = true };
            psi.ArgumentList.Add(caminho);
            Process.Start(psi);
            Escrever($"\n✔ Windows Explorer aberto em: {caminho}\n");
        }
        catch (Exception ex)
        {
            Escrever($"\n❌ Erro ao abrir Explorer: {ex.Message}\n");
        }

        FlushOutput();
    }

    // ================= SISTEMA E REPARO (COM ALERTAS DE RISCO) =================
    private async void BtnSfc_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "⚠️ ATENÇÃO: Verificação de Integridade de Arquivos (SFC)\n\n" +
            "O comando 'sfc /scannow' examinará todos os arquivos protegidos do sistema e substituirá os arquivos corrompidos por uma cópia em cache.\n\n" +
            "• Duração: Pode levar de 5 a 20 minutos dependendo do seu computador.\n" +
            "• Impacto: Alto consumo de disco e processador durante o teste.\n" +
            "• Requisito: Exige permissões de Administrador.\n\n" +
            "Deseja iniciar a verificação agora?",
            "Aviso de Segurança - SFC Scannow",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            await ExecutarAsync(CommandCatalog.Sfc);
        }
    }

    private async void BtnDism_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "⚠️ ATENÇÃO: Reparação da Imagem do Windows (DISM)\n\n" +
            "O comando 'DISM RestoreHealth' fará o download e reparo de componentes essenciais da imagem do Windows diretamente pelo Windows Update.\n\n" +
            "• Duração: Pode demorar entre 10 e 30 minutos.\n" +
            "• Impacto: Alto consumo de banda de internet e processamento.\n" +
            "• Requisito: Exige Administrador e conexão ativa com a internet.\n\n" +
            "Deseja iniciar a restauração agora?",
            "Aviso de Segurança - DISM RestoreHealth",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            await ExecutarAsync(CommandCatalog.DismRestoreHealth);
        }
    }

    private async void BtnChkdsk_Click(object sender, RoutedEventArgs e)
    {
        var chkdsk = CommandCatalog.Chkdsk(Environment.SystemDirectory);
        var unidade = chkdsk.Arguments[0];

        var confirm = MessageBox.Show(
            "⚠️ AVISO: Verificação de Disco (CHKDSK)\n\n" +
            $"O comando 'chkdsk {unidade}' verificará o sistema de arquivos da unidade do Windows em modo de leitura.\n\n" +
            "• Duração: 2 a 5 minutos.\n" +
            "• Impacto: Acesso intensivo ao disco.\n\n" +
            "Deseja iniciar o diagnóstico do disco?",
            "Diagnóstico de Disco",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (confirm == MessageBoxResult.Yes)
        {
            await ExecutarAsync(chkdsk);
        }
    }

    private async void BtnDesligar1Hora_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "⚠️ ATENÇÃO: Desligamento Agendado\n\n" +
            "O computador será desligado automaticamente em 1 HORA (3600 segundos).\n\n" +
            "Deseja realmente agendar o desligamento?",
            "Agendar Desligamento",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            await ExecutarAsync(CommandCatalog.ShutdownIn(TimeSpan.FromHours(1)));
        }
    }

    private async void BtnCancelarDesligamento_Click(object sender, RoutedEventArgs e)
    {
        await ExecutarAsync(CommandCatalog.ShutdownAbort);
    }

    // ================= CONSOLE (saída em lote, thread-safe) =================
    private void Escrever(string texto) => _pendingOutput.Enqueue(texto);

    private void FlushOutput()
    {
        if (_pendingOutput.IsEmpty)
            return;

        var sb = new StringBuilder();
        while (_pendingOutput.TryDequeue(out var parte))
            sb.Append(parte);

        TxtConsoleOutput.AppendText(sb.ToString());

        if (TxtConsoleOutput.Text.Length > MaxConsoleChars)
        {
            TxtConsoleOutput.Text = "[... saída antiga removida ...]\n" + TxtConsoleOutput.Text[^KeepConsoleChars..];
        }

        TxtConsoleOutput.ScrollToEnd();
    }

    private void SetOcupado(bool ocupado)
    {
        _isRunning = ocupado;
        // Enquanto algo executa, os painéis de comandos ficam desabilitados; só o botão Cancelar atua.
        foreach (var painel in Paineis)
            painel.IsEnabled = !ocupado;

        BtnPararComando.IsEnabled = ocupado;
    }

    // ================= MOTOR PRINCIPAL DE EXECUÇÃO ASSÍNCRONA =================
    private Task ExecutarAsync(CommandSpec comando) => ExecutarSequenciaAsync(new[] { comando }, pararNaFalha: true);

    private async Task ExecutarSequenciaAsync(IReadOnlyList<CommandSpec> comandos, bool pararNaFalha)
    {
        if (_isRunning)
            return;

        _currentCts = new CancellationTokenSource();
        var token = _currentCts.Token;

        SetOcupado(true);
        ConsoleStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0, 163, 255)); // Ciano
        TxtConsoleStatus.Text = "Executando comando no terminal...";
        _flushTimer.Start();

        try
        {
            for (int i = 0; i < comandos.Count; i++)
            {
                var spec = comandos[i];
                TxtComandoAtual.Text = comandos.Count > 1
                    ? $"Executando ({i + 1}/{comandos.Count}): {spec.Display}"
                    : $"Executando: {spec.Display}";

                Escrever($"\n\n$ {spec.Display}\n");
                Escrever("────────────────────────────────────────────────────────────────────────\n");

                var (resultado, erro, codigoSaida) = await CommandExecutor.ExecuteAsync(
                    spec,
                    onOutputLine: linha => Escrever(linha + "\n"),
                    onErrorLine: linha => Escrever("[ERRO] " + linha + "\n"),
                    cancellationToken: token);

                if (token.IsCancellationRequested)
                {
                    Escrever("\n⏹ Comando interrompido pelo usuário.\n");
                    break;
                }

                if (codigoSaida != 0)
                {
                    Escrever($"\n⚠️ [Código de saída: {codigoSaida}]\n");
                    if (!_isAdmin && OutputAnalyzer.IndicatesElevationNeeded(resultado, erro))
                    {
                        Escrever("💡 DICA: Execute o WinTerminalTools como Administrador para que este comando tenha permissão total.\n");
                    }

                    if (pararNaFalha)
                        break;
                }
                else
                {
                    Escrever("✔ Comando finalizado com sucesso.\n");
                }
            }
        }
        catch (Exception ex)
        {
            Escrever($"\n❌ Falha na execução: {ex.Message}\n");
        }
        finally
        {
            _flushTimer.Stop();
            FlushOutput();

            _currentCts.Dispose();
            _currentCts = null;

            SetOcupado(false);
            TxtComandoAtual.Text = "Nenhum comando em execução.";
            TxtConsoleStatus.Text = "Console do Windows (Pronto)";
            ConsoleStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Verde
        }
    }

    private void BtnPararComando_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _currentCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Execução já terminou.
        }
    }

    private void BtnLimparConsole_Click(object sender, RoutedEventArgs e)
    {
        while (_pendingOutput.TryDequeue(out _)) { }
        TxtConsoleOutput.Text = "Console limpo. Pronto para novos comandos.\n";
    }

    private void BtnCopiarConsole_Click(object sender, RoutedEventArgs e)
    {
        FlushOutput();
        if (!string.IsNullOrEmpty(TxtConsoleOutput.Text))
        {
            try
            {
                Clipboard.SetText(TxtConsoleOutput.Text);
                MessageBox.Show("Saída do console copiada para a Área de Transferência com sucesso!", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                MessageBox.Show("A Área de Transferência está em uso por outro programa. Tente novamente.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BtnAbrirCmd_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir o CMD: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnReiniciarAdmin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exeName = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exeName))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exeName,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                Application.Current.Shutdown();
            }
        }
        catch
        {
            MessageBox.Show("A solicitação de elevação como Administrador foi recusada ou cancelada.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir o link: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        e.Handled = true;
    }
}
