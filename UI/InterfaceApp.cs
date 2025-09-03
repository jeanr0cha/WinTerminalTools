using Terminal.Gui;
using WinTerminalTools.Core;

namespace WinTerminalTools.UI;

public class InterfaceApp
{
    
    public void Executar()
    {
        Application.Init();
        var top = Application.Top;
        var janelaPrincipal = new Window("WinTerminalTools v1.0")
        {
            X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill()
        };
        top.Add(janelaPrincipal);

        var menuPrincipalLabel = new Label("MENU PRINCIPAL") { X = Pos.Center(), Y = 1 };
        
        var btnIpConfig = new Button("Funcionalidades de IP e rede") { X = Pos.Center(), Y = 3, };
        btnIpConfig.Clicked += SubMenuIP;

        var btnSair = new Button("Sair") { X = Pos.Center(), Y = 5, };
        btnSair.Clicked += () => Application.RequestStop();

        janelaPrincipal.Add(menuPrincipalLabel, btnIpConfig, btnSair);
                
        Application.Run();
        Application.Shutdown();
    }

    private void SubMenuIP()
    {
        var menuIp = new Dialog("Comandos de IP e Rede", 130, 15); 

        
        Action<string> executarEExibir = (cmd) => {
            
            Application.RequestStop(menuIp);
            
            ExibirResultadoComando(cmd);
        };

        var btnIp = new Button("ipconfig") { X = 1, Y = 1 };
        btnIp.Clicked += () => executarEExibir("ipconfig");
        menuIp.Add(new Label("Exibe informações de IP e Rede") { X = Pos.Right(btnIp) + 2, Y = 1 });

        var btnIpAll = new Button("ipconfig /all") { X = 1, Y = 2 };
        btnIpAll.Clicked += () => executarEExibir("ipconfig /all");
        menuIp.Add(new Label("Exibe informações completas e detalhadas de todas configurações de Rede") { X = Pos.Right(btnIpAll) + 2, Y = 2 });

        var btnIpRelease = new Button("ipconfig /release") { X = 1, Y = 3 };
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
        menuIp.Add(new Label("Libera seu endereço de IP (conexão e IP interrompida temporareamente)") { X = Pos.Right(btnIpRelease) + 2, Y = 3 });
               

        var btnIpRenew = new Button("ipconfig /renew") { X = 1, Y = 4 };
        btnIpRenew.Clicked += () =>
        {
            int btnConfirmar = MessageBox.Query(
                "Atenção!",
                "Usar: Logo após um /release para obter um novo IP, ou quando o computador não consegue obter um endereço IP automaticamente ao se conectar a uma rede..",
                "Sim", "Não"
            );

            if (btnConfirmar == 0)
            {
                executarEExibir("ipconfig /renew");
            }
            
        };
        menuIp.Add(new Label("Solicita um novo endereço de IP, usar posteriormente após o uso do (release)") { X = Pos.Right(btnIpRenew) + 2, Y = 4 });
        
        var btnDisplayDns = new Button("ipconfig /displaydns") { X = 1, Y = 5 };
        btnDisplayDns.Clicked += () => executarEExibir("ipconfig /displaydns");
        menuIp.Add(new Label("Exibe o conteúdo do cache de DNS, exibe sites e IPS memorizados pelo computador") { X = Pos.Right(btnDisplayDns) + 2, Y = 5 });
        
        var btnIpFlush = new Button("ipconfig /flushdns") { X = 1, Y = 6 };
        btnIpFlush.Clicked += () => executarEExibir("ipconfig /flushdns");
        menuIp.Add(new Label("Limpa o Cache de DNS recomendado para quando você não consegue acessar sites ou é redirecionado indevidamente") { X = Pos.Right(btnIpFlush) + 2, Y = 6 });

        var btnVoltar = new Button("Voltar") { X = 1, Y = 8 };
        btnVoltar.Clicked += () => Application.RequestStop(menuIp);

        menuIp.Add(btnIp, btnIpAll, btnIpRelease, btnIpRenew, btnDisplayDns, btnIpFlush, btnVoltar);
        Application.Run(menuIp);
    }
    
    private void ExibirResultadoComando(string comando)
    {
        var (resultado, erro, codigoSaida) = CommandExecutor.Execute(comando);

        string titulo = $"Resultado: {comando}";
        string textoParaExibir;
        
        if (codigoSaida == 0)
        {
            textoParaExibir = resultado;
        }
        else
        {
            textoParaExibir = $"ERRO (Código: {codigoSaida}):\n\n{erro}";
        }

        var okButton = new Button("OK", is_default: true);

        var dialogoResultado = new Dialog(titulo, okButton);

        okButton.Clicked += () => Application.RequestStop(dialogoResultado);
        
        var scrollView = new ScrollView()
        {
            X = 1, Y = 1,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 2,
            ContentSize = new Size(100, 100),
            ShowVerticalScrollIndicator = true,
            ShowHorizontalScrollIndicator = true
        };
        
        scrollView.Add(new Label(textoParaExibir));
        dialogoResultado.Add(scrollView);
        
        Application.Run(dialogoResultado);
    }
}