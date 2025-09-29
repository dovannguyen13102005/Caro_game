using System;
using System.Windows;
using System.Windows.Threading;

namespace Caro_game.ViewModels;

public partial class MainViewModel
{
    private void StartGame(object? parameter)
    {
        bool isProfessionalMode = SelectedAIMode == "Chuyên nghiệp";
        int baseSize = isProfessionalMode ? 19 : 30;
        int rows = baseSize;
        int cols = baseSize;

        var board = new BoardViewModel(rows, cols, FirstPlayer, SelectedAIMode)
        {
            IsAIEnabled = IsAIEnabled
        };

        Board = board;

        _configuredDuration = SelectedTimeOption.Minutes > 0
            ? TimeSpan.FromMinutes(SelectedTimeOption.Minutes)
            : TimeSpan.Zero;

        StartTimer();

        IsGameActive = true;
        IsGamePaused = false;
        board.IsPaused = false;
        StatusMessage = "Đang chơi";
    }

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
}
