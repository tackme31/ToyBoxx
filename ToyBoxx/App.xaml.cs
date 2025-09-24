using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using ToyBoxx.Services;
using ToyBoxx.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

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
        SymbolRegular icon = SymbolRegular.Info12)
    {
        var snackbarService = _host.Services.GetRequiredService<ISnackbarService>();
        snackbarService.Show(title, message, appearance, new SymbolIcon(icon), TimeSpan.FromSeconds(3));
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