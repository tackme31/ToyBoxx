using System.Windows;
using ToyBoxx.ViewModels;

namespace ToyBoxx;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public RootViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.ViewModel;
        InitializeComponent();
        InitializeMainWindow();
    }

    private void InitializeMainWindow()
    {
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnWindowLoaded;

        var file = @"D:\Windows\Downloads\sample.mp4";
        App.ViewModel.Commands.OpenCommand.Execute(file);
    }
}