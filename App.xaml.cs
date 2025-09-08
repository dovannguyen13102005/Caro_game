using System.Windows;
using Caro_game.ViewModels;
using Caro_game.Views;

namespace Caro_game
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var w = new MainWindow();
            w.DataContext = new MainViewModel();
            w.Show();
        }
    }
}
