using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Caro_game.Models;
using Microsoft.Win32;

namespace Caro_game.ViewModels
{
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
                    IsAIEnabled = Board.IsAIEnabled,
                    AIMode = Board.AIMode,
                    TimeLimitMinutes = SelectedTimeOption.Minutes,
                    RemainingSeconds = SelectedTimeOption.Minutes > 0 ? (int?)Math.Ceiling(RemainingTime.TotalSeconds) : null,
                    IsPaused = IsGamePaused,
                    SavedAt = DateTime.Now,
                    Cells = Board.Cells.Select(c => new CellState
                    {
                        Row = c.Row,
                        Col = c.Col,
                        Value = c.Value,
                        IsWinningCell = c.IsWinningCell
                    }).ToList()
                };

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

            FirstPlayer = state.FirstPlayer == "O" ? "O" : "X (Bạn)";

            IsAIEnabled = state.IsAIEnabled;
            var targetMode = string.IsNullOrWhiteSpace(state.AIMode) ? "Dễ" : state.AIMode!;
            if (string.Equals(targetMode, "Bậc thầy", StringComparison.OrdinalIgnoreCase))
            {
                targetMode = "Chuyên nghiệp";
            }

            SelectedAIMode = targetMode;

            bool professionalModeRestored = state.IsAIEnabled && targetMode == "Chuyên nghiệp";
            var boardAIMode = professionalModeRestored ? "Khó" : targetMode;
            bool allowDynamicResize = !state.IsAIEnabled || targetMode != "Chuyên nghiệp";

            var board = new BoardViewModel(state.Rows, state.Columns, state.FirstPlayer ?? "X", boardAIMode, allowDynamicResize)
            {
                IsAIEnabled = professionalModeRestored ? false : state.IsAIEnabled
            };

            board.LoadFromState(state);

            Board = board;

            if (professionalModeRestored)
            {
                SelectedAIMode = "Khó";
                IsAIEnabled = false;
                MessageBox.Show("Không thể tiếp tục chế độ AI Chuyên nghiệp cho ván đã lưu. Chế độ đã được chuyển về Khó.",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var option = EnsureTimeOption(state.TimeLimitMinutes);
            SelectedTimeOption = option;

            _configuredDuration = state.TimeLimitMinutes > 0
                ? TimeSpan.FromMinutes(state.TimeLimitMinutes)
                : TimeSpan.Zero;

            if (state.TimeLimitMinutes > 0)
            {
                RemainingTime = state.RemainingSeconds.HasValue
                    ? TimeSpan.FromSeconds(Math.Max(0, state.RemainingSeconds.Value))
                    : _configuredDuration;
            }
            else
            {
                RemainingTime = TimeSpan.Zero;
            }

            bool hasWinner = state.Cells?.Any(c => c.IsWinningCell) == true;
            IsGameActive = !hasWinner;
            IsGamePaused = state.IsPaused && !hasWinner;

            if (Board != null)
            {
                Board.IsPaused = IsGamePaused || hasWinner;
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
}
