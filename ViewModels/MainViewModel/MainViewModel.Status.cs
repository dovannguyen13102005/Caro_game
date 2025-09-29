using System.Windows;
using Caro_game.Models;

namespace Caro_game.ViewModels
{
    public partial class MainViewModel
    {
        private void OnBoardGameEnded(object? sender, GameEndedEventArgs e)
        {
            StopTimer();

            if (e.HasWinner)
            {
                StatusMessage = $"Người chơi {e.Winner} thắng!";
            }

            if (e.PlayAgain)
            {
                IsGameActive = true;
                IsGamePaused = false;
                Board!.IsPaused = false;
                StartTimer();
                StatusMessage = "Đang chơi";
            }
            else
            {
                IsGameActive = false;
                IsGamePaused = false;

                Board?.DisposeEngine();

                if (_configuredDuration > TimeSpan.Zero)
                {
                    RemainingTime = _configuredDuration;
                }
            }
        }

        public void SetStatus(string message)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                StatusMessage = message;
            }
            else
            {
                Application.Current?.Dispatcher?.Invoke(() => StatusMessage = message);
            }
        }
    }
}
