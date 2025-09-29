using System;
using System.Windows;
using System.Windows.Threading;

namespace Caro_game.ViewModels
{
    public partial class MainViewModel
    {
        private void TogglePause()
        {
            if (Board == null)
            {
                return;
            }

            if (!IsGamePaused)
            {
                IsGamePaused = true;
                Board.IsPaused = true;
                _gameTimer?.Stop();
                StatusMessage = "Đang tạm dừng";
            }
            else
            {
                if (SelectedTimeOption.Minutes > 0 && RemainingTime <= TimeSpan.Zero)
                {
                    return;
                }

                IsGamePaused = false;
                Board.IsPaused = false;
                if (_configuredDuration > TimeSpan.Zero)
                {
                    _gameTimer?.Start();
                }
                StatusMessage = "Đang chơi";
            }
        }

        private void StartTimer()
        {
            StopTimer();

            if (_configuredDuration > TimeSpan.Zero)
            {
                RemainingTime = _configuredDuration;
                _gameTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _gameTimer.Tick += OnGameTimerTick;
                _gameTimer.Start();
            }
            else
            {
                RemainingTime = TimeSpan.Zero;
            }
        }

        private void StopTimer()
        {
            if (_gameTimer != null)
            {
                _gameTimer.Stop();
                _gameTimer.Tick -= OnGameTimerTick;
                _gameTimer = null;
            }
        }

        private void OnGameTimerTick(object? sender, EventArgs e)
        {
            if (IsGamePaused)
            {
                return;
            }

            if (RemainingTime > TimeSpan.Zero)
            {
                RemainingTime -= TimeSpan.FromSeconds(1);
            }

            if (RemainingTime <= TimeSpan.Zero)
            {
                RemainingTime = TimeSpan.Zero;
                StopTimer();
                HandleTimeExpired();
            }
        }

        private void HandleTimeExpired()
        {
            Board?.PauseBoard();
            IsGameActive = false;
            IsGamePaused = false;
            StatusMessage = "Hết thời gian";
            MessageBox.Show("Hết thời gian! Ván đấu đã kết thúc.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
