using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Caro_game.Models;

namespace Caro_game.ViewModels;

public partial class MainViewModel
{
    private void StartGame(object? parameter)
    {
        var ruleOption = SelectedRuleOption ?? RuleOptions.First();
        int rows = ruleOption.Rows;
        int cols = ruleOption.Columns;
        bool allowExpansion = ruleOption.AllowExpansion;
        bool isFreestyle = string.Equals(ruleOption.Name, "Freestyle", StringComparison.OrdinalIgnoreCase);

        bool playerStarts = FirstPlayer switch
        {
            "Bạn đi trước" => true,
            "Máy đi trước" => false,
            "Ngẫu nhiên" => _random.Next(2) == 0,
            _ => true
        };

        if (!IsAIEnabled)
        {
            playerStarts = true;
        }

        string startingSymbol = "X";
        string humanSymbol = playerStarts ? startingSymbol : "O";
        string aiSymbol = humanSymbol == "X" ? "O" : "X";

        if (!IsAIEnabled)
        {
            humanSymbol = startingSymbol;
            aiSymbol = "O";

            if (isFreestyle)
            {
                rows = cols = 35;
                allowExpansion = true;
            }
        }

        bool aiPlaysBlack = IsAIEnabled ? aiSymbol == "X" : true;

        ApplyRuleConfiguration(ruleOption, aiPlaysBlack);

        var ruleInstance = ruleOption.CreateRule();

        var board = new BoardViewModel(rows, cols, startingSymbol, SelectedAIMode, humanSymbol, ruleInstance, ruleOption.Name, allowExpansion)
        {
            IsAIEnabled = IsAIEnabled
        };

        Board = board;

        board.TryStartAITurn();

        _configuredDuration = SelectedTimeOption.Minutes > 0
            ? TimeSpan.FromMinutes(SelectedTimeOption.Minutes)
            : TimeSpan.Zero;

        StartTimer();

        IsGameActive = true;
        IsGamePaused = false;
        board.IsPaused = false;
        StatusMessage = "Đang chơi";
    }

    private void ApplyRuleConfiguration(RuleOption ruleOption, bool aiPlaysBlack)
    {
        try
        {
            var configFileName = ruleOption.ResolveConfigFile(aiPlaysBlack);
            if (string.IsNullOrWhiteSpace(configFileName))
            {
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\"));
            var aiDirectory = Path.Combine(projectRoot, "AI");
            var sourcePath = Path.Combine(aiDirectory, configFileName);
            var targetPath = Path.Combine(aiDirectory, "config.toml");

            if (!File.Exists(sourcePath))
            {
                MessageBox.Show($"Không tìm thấy cấu hình cho luật {ruleOption.Name}.\n{sourcePath}", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            File.Copy(sourcePath, targetPath, overwrite: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể áp dụng cấu hình luật {ruleOption.Name}.\nChi tiết: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            var board = Board;
            if (board != null && board.IsAIEnabled)
            {
                bool aiWon = string.Equals(e.Winner, board.AISymbol, StringComparison.OrdinalIgnoreCase);
                StatusMessage = aiWon ? "Máy thắng!" : "Bạn thắng!";
            }
            else
            {
                StatusMessage = string.IsNullOrWhiteSpace(e.Winner)
                    ? "Đã có người thắng!"
                    : $"Người chơi {e.Winner} thắng!";
            }
        }
        else
        {
            StatusMessage = "Hòa cờ!";
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
