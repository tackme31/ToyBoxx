using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.Design;
using System.Windows;
using System.Windows.Input;
using ToyBoxx.Foundation;
using ToyBoxx.Services;
using ToyBoxx.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Input;

namespace ToyBoxx;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            _ = c.SetBasePath(AppContext.BaseDirectory);
        })
        .ConfigureServices((_1, services) =>
        {
            _ = services.AddHostedService<ApplicationHostService>();

            _ = services.AddSingleton<MainWindow>();
            _ = services.AddSingleton<RootViewModel>();
            _ = services.AddSingleton<ISnackbarService, SnackbarService>();
        })
        .Build();

    public static void ShowSnackbar(
        string title,
        string message,
        ControlAppearance appearance = ControlAppearance.Secondary,
        SymbolRegular icon = SymbolRegular.Info12,
        Action? onClick = null)
    {
        var snackbarService = _host.Services.GetRequiredService<ISnackbarService>();
        snackbarService.Show(title, message, appearance, new SymbolIcon(icon), TimeSpan.FromSeconds(3));

        if (onClick is not null)
        {
            var presenter = snackbarService.GetSnackbarPresenter();
            if (presenter is SnackbarPresenter { Content: Snackbar snackbar })
            {
                void handler(object sender, MouseButtonEventArgs e)
                {
                    onClick();

                    snackbar.PreviewMouseLeftButtonDown -= handler;

                    e.Handled = true;
                }

                snackbar.PreviewMouseLeftButtonDown += handler;
            }
        }
    }

    public static T GetRequiredService<T>()
        where T : class
    {
        return _host.Services.GetRequiredService<T>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _host.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync().Wait();
        _host.Dispose();
    }
}