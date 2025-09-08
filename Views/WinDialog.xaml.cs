using System.Windows;

namespace Caro_game.Views
{
    public partial class WinDialog : Window
    {
        public bool IsPlayAgain { get; private set; }

        public WinDialog(string message)
        {
            InitializeComponent();
            MessageText.Text = message;

            BtnPlayAgain.Click += (s, e) =>
            {
                IsPlayAgain = true;
                DialogResult = true;
                Close();
            };

            BtnExit.Click += (s, e) =>
            {
                IsPlayAgain = false;
                DialogResult = false;
                Close();
            };
        }
    }
}
