using System.Security.Principal;
using System.Text;
using Terminal.Gui;
using WinTerminalTools.Core;

namespace WinTerminalTools.UI;

public class InterfaceApp
{
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

    public void Executar()
    {
        Application.Init();
        var top = Application.Top;

        string sufixoAdmin = EhAdministrador() ? " [Administrador]" : " [Usuário Padrão]";
        var janelaPrincipal = new Window($"WinTerminalTools v1.0{sufixoAdmin}")
        {
            X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill()
        };
        top.Add(janelaPrincipal);

        var menuPrincipalLabel = new Label("MENU PRINCIPAL") { X = Pos.Center(), Y = 1 };
        
        var btnIpConfig = new Button("Funcionalidades de IP e rede") { X = Pos.Center(), Y = 3 };
        btnIpConfig.Clicked += SubMenuIP;

        var btnSair = new Button("Sair") { X = Pos.Center(), Y = 5 };
        btnSair.Clicked += () => Application.RequestStop();

        janelaPrincipal.Add(menuPrincipalLabel, btnIpConfig, btnSair);
                
        Application.Run();
        Application.Shutdown();
    }

    private void SubMenuIP()
    {
        bool isAdmin = EhAdministrador();
        string sufixoAdmin = isAdmin ? " [Administrador]" : " [Usuário Padrão]";
        var menuIp = new Dialog($"Comandos de IP e Rede{sufixoAdmin}", 132, 19); 

        // Permite fechar o submenu pressionando ESC
        menuIp.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Esc)
            {
                Application.RequestStop(menuIp);
                e.Handled = true;
            }
        };

        Action<string> executarEExibir = (cmd) =>
        {
            ExibirResultadoComando(cmd);
        };

        Action<string, string> pedirEntradaEExibir = (comandoBase, mensagem) =>
        {
            var dialogEntrada = new Dialog("Entrada Necessária", 60, 10);
            var labelMsg = new Label(mensagem) { X = 1, Y = 1 };
            var input = new TextField("") { X = 1, Y = 3, Width = Dim.Fill() - 2 };
            
            var btnOk = new Button("OK", is_default: true);
            var btnCancelar = new Button("Cancelar");

            bool confirmado = false;
            string argumentoFinal = "";

            Action confirmar = () =>
            {
                var argumento = input.Text.ToString()?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(argumento))
                {
                    if (comandoBase == "ping" || comandoBase == "tracert")
                    {
                        argumento = SanitizarHost(argumento);
                    }
                    argumentoFinal = argumento;
                    confirmado = true;
                }
                Application.RequestStop(dialogEntrada);
            };

            btnOk.Clicked += confirmar;
            btnCancelar.Clicked += () => Application.RequestStop(dialogEntrada);

            // Permite confirmar com Enter ou cancelar com ESC diretamente no campo de texto
            input.KeyPress += (e) =>
            {
                if (e.KeyEvent.Key == Key.Enter)
                {
                    confirmar();
                    e.Handled = true;
                }
                else if (e.KeyEvent.Key == Key.Esc)
                {
                    Application.RequestStop(dialogEntrada);
                    e.Handled = true;
                }
            };

            dialogEntrada.KeyPress += (e) =>
            {
                if (e.KeyEvent.Key == Key.Esc)
                {
                    Application.RequestStop(dialogEntrada);
                    e.Handled = true;
                }
            };

            dialogEntrada.AddButton(btnOk);
            dialogEntrada.AddButton(btnCancelar);
            dialogEntrada.Add(labelMsg, input);

            Application.Run(dialogEntrada);

            // O diálogo de entrada agora está 100% fechado e fora da tela antes de iniciar o comando
            if (confirmado && !string.IsNullOrWhiteSpace(argumentoFinal))
            {
                ExibirResultadoComando($"{comandoBase} {argumentoFinal}");
            }
        };

        const int colDesc = 27;
        const int btnLargura = 24;

        var btnIp = new Button("ipconfig") { X = 1, Y = 1, Width = btnLargura };
        btnIp.Clicked += () => executarEExibir("ipconfig");
        menuIp.Add(btnIp, new Label("Exibe informações de IP e adaptadores de rede") { X = colDesc, Y = 1 });

        var btnIpAll = new Button("ipconfig /all") { X = 1, Y = 2, Width = btnLargura };
        btnIpAll.Clicked += () => executarEExibir("ipconfig /all");
        menuIp.Add(btnIpAll, new Label("Exibe configurações completas e detalhadas de rede (DHCP, DNS, MAC)") { X = colDesc, Y = 2 });

        var btnIpRelease = new Button("ipconfig /release") { X = 1, Y = 3, Width = btnLargura };
        btnIpRelease.Clicked += () =>
        {
            int btnConfirmar = MessageBox.Query(
                "Atenção!",
                "Executar este comando deixará seu computador temporariamente sem conexão e sem IP.\nRecomendação: usar posteriormente o comando 'ipconfig /renew'.",
                "Sim", "Não"
            );

            if (btnConfirmar == 0)
            {
                executarEExibir("ipconfig /release");
            }
        };
        string tagAdminRelease = isAdmin ? "" : " [Requer Admin]";
        menuIp.Add(btnIpRelease, new Label($"Libera seu endereço de IP atual (conexão interrompida){tagAdminRelease}") { X = colDesc, Y = 3 });

        var btnIpRenew = new Button("ipconfig /renew") { X = 1, Y = 4, Width = btnLargura };
        btnIpRenew.Clicked += () =>
        {
            int btnConfirmar = MessageBox.Query(
                "Atenção!",
                "Usar após um /release para obter um novo IP via DHCP, ou se o computador não obtém IP na rede.",
                "Sim", "Não"
            );

            if (btnConfirmar == 0)
            {
                executarEExibir("ipconfig /renew");
            }
        };
        string tagAdminRenew = isAdmin ? "" : " [Requer Admin]";
        menuIp.Add(btnIpRenew, new Label($"Solicita um novo endereço de IP ao servidor DHCP{tagAdminRenew}") { X = colDesc, Y = 4 });
        
        var btnDisplayDns = new Button("ipconfig /displaydns") { X = 1, Y = 5, Width = btnLargura };
        btnDisplayDns.Clicked += () => executarEExibir("ipconfig /displaydns");
        string tagAdminDisplay = isAdmin ? "" : " [Requer Admin]";
        menuIp.Add(btnDisplayDns, new Label($"Exibe o cache do cliente DNS local (sites e IPs memorizados){tagAdminDisplay}") { X = colDesc, Y = 5 });
        
        var btnIpFlush = new Button("ipconfig /flushdns") { X = 1, Y = 6, Width = btnLargura };
        btnIpFlush.Clicked += () => executarEExibir("ipconfig /flushdns");
        string tagAdminFlush = isAdmin ? "" : " [Requer Admin]";
        menuIp.Add(btnIpFlush, new Label($"Limpa o cache de DNS (útil ao não conseguir acessar sites após mudanças){tagAdminFlush}") { X = colDesc, Y = 6 });

        var btnGetmac = new Button("getmac /v") { X = 1, Y = 7, Width = btnLargura };
        btnGetmac.Clicked += () => executarEExibir("getmac /v");
        menuIp.Add(btnGetmac, new Label("Lista as placas de rede e o endereço físico (MAC Address) de cada uma") { X = colDesc, Y = 7 });

        var btnArp = new Button("arp -a") { X = 1, Y = 8, Width = btnLargura };
        btnArp.Clicked += () => executarEExibir("arp -a");
        menuIp.Add(btnArp, new Label("Exibe a tabela ARP com os IPs e MACs dos dispositivos na rede local") { X = colDesc, Y = 8 });

        var btnNetstat = new Button("netstat -ano") { X = 1, Y = 9, Width = btnLargura };
        btnNetstat.Clicked += () => executarEExibir("netstat -ano");
        menuIp.Add(btnNetstat, new Label("Mostra conexões de rede ativas, portas abertas e o PID do processo") { X = colDesc, Y = 9 });

        var btnPing = new Button("ping") { X = 1, Y = 10, Width = btnLargura };
        btnPing.Clicked += () => pedirEntradaEExibir("ping", "Digite o IP ou site (ex: google.com) para testar conectividade:");
        menuIp.Add(btnPing, new Label("Testa a conectividade e latência com outro computador ou site") { X = colDesc, Y = 10 });

        var btnTracert = new Button("tracert") { X = 1, Y = 11, Width = btnLargura };
        btnTracert.Clicked += () => pedirEntradaEExibir("tracert", "Digite o IP ou site para rastrear a rota (ex: google.com):");
        menuIp.Add(btnTracert, new Label("Rastreia os saltos e roteadores até o destino (pode levar alguns segundos)") { X = colDesc, Y = 11 });

        var btnVoltar = new Button("Voltar") { X = 1, Y = 13, Width = btnLargura };
        btnVoltar.Clicked += () => Application.RequestStop(menuIp);
        menuIp.Add(btnVoltar, new Label("Retorna ao Menu Principal") { X = colDesc, Y = 13 });

        Application.Run(menuIp);
    }
    
    private void ExibirResultadoComando(string comando)
    {
        var cts = new CancellationTokenSource();
        bool emExecucao = true;

        string titulo = $"Executando: {comando} [Aguarde... ou ESC para cancelar]";

        var btnAcao = new Button("Cancelar [ESC]", is_default: true);
        var dialogoResultado = new Dialog(titulo, btnAcao);

        var sbConteudo = new StringBuilder();
        sbConteudo.AppendLine($"$ {comando}");
        sbConteudo.AppendLine("--------------------------------------------------------------------------------");
        sbConteudo.AppendLine("Iniciando comando... Aguarde o retorno dos dados em tempo real.\n");

        var textView = new TextView()
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
            Text = sbConteudo.ToString(),
            ReadOnly = true,
            WordWrap = false
        };

        Action fecharOuCancelar = () =>
        {
            if (emExecucao)
            {
                try
                {
                    cts.Cancel();
                }
                catch
                {
                    // Ignora
                }
            }
            Application.RequestStop(dialogoResultado);
        };

        btnAcao.Clicked += fecharOuCancelar;

        dialogoResultado.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Esc)
            {
                fecharOuCancelar();
                e.Handled = true;
            }
        };

        dialogoResultado.Add(textView);

        // Dispara a execução assíncrona em background para NUNCA congelar a interface
        Task.Run(async () =>
        {
            var (resultado, erro, codigoSaida) = await CommandExecutor.ExecuteAsync(
                comando,
                onOutputLine: (linha) =>
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        sbConteudo.AppendLine(linha);
                        textView.Text = sbConteudo.ToString();
                        textView.CursorPosition = new Point(0, Math.Max(0, sbConteudo.ToString().Split('\n').Length - 1));
                    });
                },
                onErrorLine: (linha) =>
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        sbConteudo.AppendLine($"[ERRO]: {linha}");
                        textView.Text = sbConteudo.ToString();
                    });
                },
                cancellationToken: cts.Token
            );

            emExecucao = false;

            Application.MainLoop.Invoke(() =>
            {
                if (cts.IsCancellationRequested)
                {
                    dialogoResultado.Title = $"Cancelado: {comando} [ESC para fechar]";
                    btnAcao.Text = "Fechar [ESC]";
                    return;
                }

                dialogoResultado.Title = $"Resultado: {comando} [Concluído - ESC para fechar]";
                btnAcao.Text = "OK [ESC]";

                if (codigoSaida != 0)
                {
                    sbConteudo.AppendLine();
                    sbConteudo.AppendLine("--------------------------------------------------------------------------------");
                    sbConteudo.AppendLine($"[CÓDIGO DE RETORNO / ERRO: {codigoSaida}]");

                    string msg = resultado + " " + erro;
                    if (msg.Contains("elevação", StringComparison.OrdinalIgnoreCase) ||
                        msg.Contains("elevation", StringComparison.OrdinalIgnoreCase) ||
                        msg.Contains("requer", StringComparison.OrdinalIgnoreCase))
                    {
                        sbConteudo.AppendLine("\n💡 DICA: Execute o terminal ou programa como Administrador (Run as Administrator) para que esta operação tenha permissão.");
                    }
                    textView.Text = sbConteudo.ToString();
                }
            });
        });

        Application.Run(dialogoResultado);
    }

    private static string SanitizarHost(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return string.Empty;
        entrada = entrada.Trim();

        // Remove caracteres perigosos de concatenação de comando no shell
        entrada = entrada.Replace("&", "").Replace("|", "").Replace(";", "").Replace("<", "").Replace(">", "").Replace("^", "");

        if (Uri.TryCreate(entrada, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            return uri.Host;
        }

        if (entrada.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            entrada = entrada.Substring(8);
        else if (entrada.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            entrada = entrada.Substring(7);

        int slashIndex = entrada.IndexOf('/');
        if (slashIndex >= 0)
        {
            entrada = entrada.Substring(0, slashIndex);
        }

        return entrada.Trim();
    }
}
