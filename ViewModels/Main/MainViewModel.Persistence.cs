using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Caro_game.Models;
using Microsoft.Win32;

namespace Caro_game.ViewModels;

public partial class MainViewModel
{
    private void SaveCurrentGame()
    {
        if (Board == null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Caro Save|*.json",
            FileName = $"caro_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var state = new GameState
            {
                Rows = Board.Rows,
                Columns = Board.Columns,
                FirstPlayer = Board.InitialPlayer,
                CurrentPlayer = Board.CurrentPlayer,
                HumanSymbol = Board.HumanSymbol,
                Rule = Board.RuleName,
                IsAIEnabled = Board.IsAIEnabled,
                AIMode = Board.AIMode,
                TimeLimitMinutes = SelectedTimeOption.Minutes,
                RemainingSeconds = SelectedTimeOption.Minutes > 0 ? (int?)Math.Ceiling(RemainingTime.TotalSeconds) : null,
                RemainingSecondsX = SelectedTimeOption.Minutes > 0 ? (int?)Math.Ceiling(RemainingTimeX.TotalSeconds) : null,
                RemainingSecondsO = SelectedTimeOption.Minutes > 0 ? (int?)Math.Ceiling(RemainingTimeO.TotalSeconds) : null,
                IsPaused = IsGamePaused,
                Player1Name = Player1.Name,
                Player1Avatar = Player1.AvatarPath,
                Player2Name = Player2.Name,
                Player2Avatar = Player2.AvatarPath,
                SavedAt = DateTime.Now,
                Cells = Board.Cells.Select(c => new CellState
                {
                    Row = c.Row,
                    Col = c.Col,
                    Value = c.Value,
                    IsWinningCell = c.IsWinningCell
                }).ToList(),
                Moves = Board.MoveHistory.Select(m => new MoveState
                {
                    Row = m.Row,
                    Col = m.Col,
                    Player = m.Player
                }).ToList()
            };

            var lastMove = Board.LastMovePosition;
            if (lastMove.HasValue)
            {
                state.LastMoveRow = lastMove.Value.Row;
                state.LastMoveCol = lastMove.Value.Col;
            }

            state.LastMovePlayer = Board.LastMovePlayer;

            var lastHumanMove = Board.LastHumanMovePosition;
            if (lastHumanMove.HasValue)
            {
                state.LastHumanMoveRow = lastHumanMove.Value.Row;
                state.LastHumanMoveCol = lastHumanMove.Value.Col;
            }

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(dialog.FileName, json);
            MessageBox.Show("Ván đấu đã được lưu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void LoadSavedGame()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Caro Save|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var state = JsonSerializer.Deserialize<GameState>(json);

                if (state == null)
                {
                    throw new InvalidOperationException("Không đọc được dữ liệu từ tệp đã chọn.");
                }

                ApplyGameState(state);

                MessageBox.Show("Đã tải ván đấu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở tệp đã lưu.\nChi tiết: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ApplyGameState(GameState state)
    {
        StopTimer();

        Board = null;
        IsGameActive = false;
        IsGamePaused = false;

        var humanSymbol = string.IsNullOrWhiteSpace(state.HumanSymbol)
            ? "X"
            : (state.HumanSymbol!.Equals("O", StringComparison.OrdinalIgnoreCase) ? "O" : "X");

        FirstPlayer = humanSymbol == "O" ? "Máy đi trước" : "Bạn đi trước";

        IsAIEnabled = state.IsAIEnabled;
        var targetMode = string.IsNullOrWhiteSpace(state.AIMode) ? "Dễ" : state.AIMode!;
        SelectedAIMode = targetMode;

        var ruleOption = ResolveRuleOption(state.Rule);
        SelectedRuleOption = ruleOption;
        var ruleInstance = ruleOption.CreateRule();

        var board = new BoardViewModel(state.Rows, state.Columns, state.FirstPlayer ?? "X", targetMode, humanSymbol, ruleInstance, ruleOption.Name, ruleOption.AllowExpansion, isRestoringState: true)
        {
            IsAIEnabled = state.IsAIEnabled,
            PlayerXName = Player1.Name,
            PlayerOName = Player2.Name
        };

        board.LoadFromState(state);

        Board = board;

        var option = EnsureTimeOption(state.TimeLimitMinutes);
        SelectedTimeOption = option;

        _configuredDuration = state.TimeLimitMinutes > 0
            ? TimeSpan.FromMinutes(state.TimeLimitMinutes)
            : TimeSpan.Zero;

        if (state.TimeLimitMinutes > 0)
        {
            if (state.RemainingSecondsX.HasValue && state.RemainingSecondsO.HasValue)
            {
                RemainingTimeX = TimeSpan.FromSeconds(Math.Max(0, state.RemainingSecondsX.Value));
                RemainingTimeO = TimeSpan.FromSeconds(Math.Max(0, state.RemainingSecondsO.Value));
                RemainingTime = Board?.CurrentPlayer == "X" ? RemainingTimeX : RemainingTimeO;
            }
            else
            {
                RemainingTime = state.RemainingSeconds.HasValue
                    ? TimeSpan.FromSeconds(Math.Max(0, state.RemainingSeconds.Value))
                    : _configuredDuration;
                RemainingTimeX = _configuredDuration;
                RemainingTimeO = _configuredDuration;
            }
        }
        else
        {
            RemainingTime = TimeSpan.Zero;
            RemainingTimeX = TimeSpan.Zero;
            RemainingTimeO = TimeSpan.Zero;
        }

        if (!string.IsNullOrEmpty(state.Player1Name))
        {
            Player1.Name = state.Player1Name;
        }
        if (!string.IsNullOrEmpty(state.Player1Avatar))
        {
            Player1.AvatarPath = state.Player1Avatar;
        }
        if (!string.IsNullOrEmpty(state.Player2Name))
        {
            Player2.Name = state.Player2Name;
        }
        if (!string.IsNullOrEmpty(state.Player2Avatar))
        {
            Player2.AvatarPath = state.Player2Avatar;
        }
        else if (state.IsAIEnabled)
        {
            Player2.Name = "Máy";
            Player2.AvatarPath = "robot";
        }

        bool hasWinner = state.Cells?.Any(c => c.IsWinningCell) == true;
        IsGameActive = !hasWinner;
        IsGamePaused = state.IsPaused && !hasWinner;

        if (Board != null)
        {
            Board.IsPaused = IsGamePaused || hasWinner;
            Board.ResumePendingAiTurn();
        }

        if (_configuredDuration > TimeSpan.Zero && !IsGamePaused && !hasWinner)
        {
            _gameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _gameTimer.Tick += OnGameTimerTick;
            _gameTimer.Start();
        }

        StatusMessage = hasWinner
            ? "Ván đấu đã kết thúc."
            : IsGamePaused ? "Đang tạm dừng" : "Đang chơi";

        CommandManager.InvalidateRequerySuggested();
    }

    private TimeOption EnsureTimeOption(int minutes)
    {
        var existing = TimeOptions.FirstOrDefault(t => t.Minutes == minutes);
        if (existing != null)
        {
            return existing;
        }

        var label = minutes > 0 ? $"{minutes} phút" : "Không giới hạn";
        var option = new TimeOption(minutes, label + " (tải)");
        TimeOptions.Add(option);
        return option;
    }
}
