using System.Windows;
using System;
using Microsoft.Win32;
using Caro_game.ViewModels;

namespace Caro_game.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.StateChanged += MainWindow_StateChanged;
        }
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                this.Width = 1005;
                this.Height = 707;
            }
        }

        private void SelectPlayer1Avatar_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
                Title = "Chọn avatar cho Người chơi 1"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.Player1.AvatarPath = openFileDialog.FileName;
                }
            }
        }

        private void SelectPlayer2Avatar_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
                Title = "Chọn avatar cho Người chơi 2"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.Player2.AvatarPath = openFileDialog.FileName;
                }
            }
        }

    }
}
