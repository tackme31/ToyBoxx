using System.Windows;
using ToyBoxx.Foundation;
using Unosquare.FFME;

namespace ToyBoxx;

public class AppCommands
{
    private DelegateCommand? _openCommand;

    public DelegateCommand OpenCommand => _openCommand ??= new DelegateCommand(async arg =>
    {
        try
        {
            var uriString = arg as string;
            if (string.IsNullOrWhiteSpace(uriString))
            {
                return;
            }

            var media = App.ViewModel.MediaElement.Value;
            var target = new Uri(uriString);
            await media.Open(new FileInputStream(target.LocalPath));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Application.Current.MainWindow,
                $"Media Failed: {ex.GetType()}\r\n{ex.Message}",
                $"{nameof(MediaElement)} Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                MessageBoxResult.OK);
        }
    });
}
