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

        if (IsAIEnabled)
        {
            Player2.Name = "Máy";
            Player2.AvatarPath = "robot"; 
        }

        bool aiPlaysBlack = IsAIEnabled ? aiSymbol == "X" : true;

        ApplyRuleConfiguration(ruleOption, aiPlaysBlack);

        var ruleInstance = ruleOption.CreateRule();

        var board = new BoardViewModel(rows, cols, startingSymbol, SelectedAIMode, humanSymbol, ruleInstance, ruleOption.Name, allowExpansion)
        {
            IsAIEnabled = IsAIEnabled,
            PlayerXName = Player1.Name,
            PlayerOName = Player2.Name
        };

        Board = board;

        board.TryStartAITurn();

        _configuredDuration = SelectedTimeOption.Minutes > 0
            ? TimeSpan.FromMinutes(SelectedTimeOption.Minutes)
            : TimeSpan.Zero;

        if (_configuredDuration > TimeSpan.Zero)
        {
            RemainingTimeX = _configuredDuration;
            RemainingTimeO = _configuredDuration;
            RemainingTime = _configuredDuration;
        }
        else
        {
            RemainingTimeX = TimeSpan.Zero;
            RemainingTimeO = TimeSpan.Zero;
            RemainingTime = TimeSpan.Zero;
        }

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

        if (_configuredDuration <= TimeSpan.Zero || Board == null)
        {
            return;
        }

        if (Board.CurrentPlayer == "X")
        {
            if (RemainingTimeX > TimeSpan.Zero)
            {
                RemainingTimeX -= TimeSpan.FromSeconds(1);
            }
            if (RemainingTimeX <= TimeSpan.Zero)
            {
                RemainingTimeX = TimeSpan.Zero;
                StopTimer();
                HandleTimeExpired();
            }
        }
        else
        {
            if (RemainingTimeO > TimeSpan.Zero)
            {
                RemainingTimeO -= TimeSpan.FromSeconds(1);
            }
            if (RemainingTimeO <= TimeSpan.Zero)
            {
                RemainingTimeO = TimeSpan.Zero;
                StopTimer();
                HandleTimeExpired();
            }
        }
        
        RemainingTime = Board.CurrentPlayer == "X" ? RemainingTimeX : RemainingTimeO;
    }

    private void HandleTimeExpired()
    {
        Board?.PauseBoard();
        IsGameActive = false;
        IsGamePaused = false;
        
        if (Board == null) return;

        string loser = Board.CurrentPlayer;
        string winner = loser == "X" ? "O" : "X";
        string loserName = loser == "X" ? Player1.Name : Player2.Name;
        string winnerName = winner == "X" ? Player1.Name : Player2.Name;

        if (Board.IsAIEnabled)
        {
            bool humanLost = string.Equals(loser, Board.HumanSymbol, StringComparison.OrdinalIgnoreCase);
            
            if (humanLost)
            {
                StatusMessage = $"{loserName} thua vì hết thời gian!";
                MessageBox.Show($"{loserName} hết thời gian! Bạn đã thua.", "Kết thúc ván", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"{winnerName} thắng!";
                MessageBox.Show($"Máy hết thời gian! {winnerName} thắng!", "Kết thúc ván", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            StatusMessage = $"{winnerName} thắng! {loserName} hết thời gian.";
            MessageBox.Show($"{loserName} hết thời gian!\n{winnerName} thắng!", "Kết thúc ván", MessageBoxButton.OK, MessageBoxImage.Information);
        }
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
