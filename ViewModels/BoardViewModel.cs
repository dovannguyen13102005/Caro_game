using Caro_game.Commands;
using Caro_game.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Caro_game.ViewModels
{
    public class BoardViewModel : BaseViewModel
    {
        public int Rows { get; }
        public int Columns { get; }
        public ObservableCollection<Cell> Cells { get; }

        private readonly Dictionary<(int Row, int Col), Cell> _cellLookup;
        private readonly HashSet<(int Row, int Col)> _candidatePositions;
        private readonly object _candidateLock = new();
        private readonly string _initialPlayer;
        private string _currentPlayer;
        private EngineClient? _engine;

        public string CurrentPlayer
        {
            get => _currentPlayer;
            set
            {
                if (_currentPlayer != value)
                {
                    _currentPlayer = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isAIEnabled;
        public bool IsAIEnabled
        {
            get => _isAIEnabled;
            set
            {
                if (_isAIEnabled != value)
                {
                    _isAIEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _aiMode = "Dễ";
        public string AIMode
        {
            get => _aiMode;
            set
            {
                if (_aiMode != value)
                {
                    _aiMode = value;
                    OnPropertyChanged();

                    if (_aiMode == "Bậc thầy")
                    {
                        TryInitializeMasterEngine();
                    }
                    else
                    {
                        DisposeEngine();
                    }
                }
            }
        }


        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    OnPropertyChanged();
                }
            }
        }

        public string InitialPlayer => _initialPlayer;

        public event EventHandler<GameEndedEventArgs>? GameEnded;

        public BoardViewModel(int rows, int columns, string firstPlayer, string aiMode = "Dễ")
        {
            Rows = rows;
            Columns = columns;
            AIMode = aiMode;
            CurrentPlayer = firstPlayer.StartsWith("X", StringComparison.OrdinalIgnoreCase) ? "X" : "O";

            _initialPlayer = CurrentPlayer;
            Cells = new ObservableCollection<Cell>();
            _cellLookup = new Dictionary<(int, int), Cell>(rows * columns);
            _candidatePositions = new HashSet<(int, int)>();

            for (int i = 0; i < rows * columns; i++)
            {
                int r = i / columns;
                int c = i % columns;
                var cell = new Cell(r, c, this);
                Cells.Add(cell);
                _cellLookup[(r, c)] = cell;
            }

            if (AIMode == "Bậc thầy")
            {
                TryInitializeMasterEngine();
            }
        }

        public void MakeMove(Cell cell)
        {
            if (IsPaused || !string.IsNullOrEmpty(cell.Value))
                return;

            var movingPlayer = CurrentPlayer;
            cell.Value = movingPlayer;
            UpdateCandidatePositions(cell.Row, cell.Col);

            // Kiểm tra thắng
            if (CheckWin(cell.Row, cell.Col, movingPlayer))
            {
                HighlightWinningCells(cell.Row, cell.Col, movingPlayer);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new Views.WinDialog($"Người chơi {movingPlayer} thắng!")
                    {
                        Owner = Application.Current.MainWindow
                    };

                    dialog.ShowDialog();

                    GameEnded?.Invoke(this, new GameEndedEventArgs(movingPlayer, dialog.IsPlayAgain, true));

                    if (dialog.IsPlayAgain)
                    {
                        ResetBoard();
                    }
                    else
                    {
                        DisposeEngine();
                        Application.Current.Shutdown();
                    }
                });

                return;
            }

            // 🟢 Check hòa: nếu không còn ô trống
            if (Cells.All(c => !string.IsNullOrEmpty(c.Value)))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Hòa cờ! Bàn đã đầy mà không có người thắng.",
                        "Kết thúc ván", MessageBoxButton.OK, MessageBoxImage.Information);

                    GameEnded?.Invoke(this, new GameEndedEventArgs(null, false, false));
                });
                return;
            }

            // Đổi lượt
            CurrentPlayer = movingPlayer == "X" ? "O" : "X";

            // Nếu là lượt AI
            if (IsAIEnabled && CurrentPlayer == "O")
            {
                if (AIMode == "Bậc thầy" && _engine != null)
                {
                    // Hiện thông báo "AI đang suy nghĩ..."
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainVM = Application.Current.MainWindow?.DataContext as MainViewModel;
                        mainVM?.SetStatus("AI đang suy nghĩ...");
                    });

                    Task.Run(() =>
                    {
                        try
                        {
                            string aiMove = _engine.Turn(cell.Col, cell.Row);
                            PlaceAiIfValid(aiMove);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var mainVM = Application.Current.MainWindow?.DataContext as MainViewModel;
                                mainVM?.SetStatus("Đang chơi");
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"AI engine error: {ex.Message}", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                            DisposeEngine();
                        }
                    });
                }
                else
                {
                    // AI Dễ/Khó chạy nền
                    Task.Run(AIMove);
                }
            }
        }


        private void AIMove()
        {
            if (!IsAIEnabled || IsPaused)
                return;

            Cell? bestCell = null;

            if (AIMode == "Dễ")
            {
                var lastPlayerMove = Cells.LastOrDefault(c => c.Value == "X");
                if (lastPlayerMove != null)
                {
                    var neighbors = Cells.Where(c =>
                        string.IsNullOrEmpty(c.Value) &&
                        Math.Abs(c.Row - lastPlayerMove.Row) <= 1 &&
                        Math.Abs(c.Col - lastPlayerMove.Col) <= 1).ToList();

                    if (neighbors.Any())
                    {
                        bestCell = neighbors[new Random().Next(neighbors.Count)];
                    }
                }

                if (bestCell == null)
                {
                    var emptyCells = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();
                    if (emptyCells.Any())
                        bestCell = emptyCells[new Random().Next(emptyCells.Count)];
                }
            }
            else // Khó
            {
                var candidates = Cells
                    .Where(c => string.IsNullOrEmpty(c.Value) && HasNeighbor(c, 2))
                    .ToList();

                if (!candidates.Any())
                    candidates = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();

                int bestScore = int.MinValue;

                foreach (var cell in candidates)
                {
                    int score = EvaluateCellAdvanced(cell);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCell = cell;
                    }
                }
            }

            if (bestCell != null)
            {
                Application.Current.Dispatcher.Invoke(() => MakeMove(bestCell));
            }
        }

        private void PlaceAiIfValid(string aiMove)
        {
            if (string.IsNullOrWhiteSpace(aiMove)) return;

            var parts = aiMove.Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int aiX) &&
                int.TryParse(parts[1], out int aiY) &&
                _cellLookup.TryGetValue((aiY, aiX), out var aiCell))
            {
                Application.Current.Dispatcher.Invoke(() => MakeMove(aiCell));
            }
        }

        private IEnumerable<Cell> GetNeighbors(int row, int col, int range)
        {
            for (int dr = -range; dr <= range; dr++)
            {
                for (int dc = -range; dc <= range; dc++)
                {
                    if (dr == 0 && dc == 0) continue;

                    int r = row + dr;
                    int c = col + dc;

                    if (_cellLookup.TryGetValue((r, c), out var neighbor))
                    {
                        yield return neighbor;
                    }
                }
            }
        }

        private void UpdateCandidatePositions(int row, int col)
        {
            lock (_candidateLock)
            {
                _candidatePositions.Remove((row, col));

                foreach (var neighbor in GetNeighbors(row, col, 2))
                {
                    if (string.IsNullOrEmpty(neighbor.Value))
                    {
                        _candidatePositions.Add((neighbor.Row, neighbor.Col));
                    }
                }
            }
        }

        private bool HasNeighbor(Cell cell, int range)
            => GetNeighbors(cell.Row, cell.Col, range).Any(n => !string.IsNullOrEmpty(n.Value));

        private int EvaluateCellAdvanced(Cell cell)
        {
            int score = 0;
            score += EvaluatePotential(cell, "O");
            score += EvaluatePotential(cell, "X") * 2;
            score += ProximityScore(cell, "X") * 5;
            return score;
        }

        private int ProximityScore(Cell cell, string player)
        {
            int score = 0;
            foreach (var neighbor in GetNeighbors(cell.Row, cell.Col, 1))
            {
                if (neighbor.Value == player) score += 1;
            }
            return score;
        }

        private int EvaluatePotential(Cell cell, string player)
        {
            int totalScore = 0;
            int[][] directions = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 } };

            foreach (var dir in directions)
            {
                int count = 1;
                count += CountDirectionSimulate(cell.Row, cell.Col, dir[0], dir[1], player);
                count += CountDirectionSimulate(cell.Row, cell.Col, -dir[0], -dir[1], player);

                totalScore += count switch
                {
                    >= 5 => 10000,
                    4 => 1000,
                    3 => 100,
                    2 => 10,
                    _ => 0
                };
            }

            return totalScore;
        }

        private int CountDirectionSimulate(int row, int col, int dRow, int dCol, string player)
        {
            int count = 0;
            int r = row + dRow;
            int c = col + dCol;

            while (r >= 0 && r < Rows && c >= 0 && c < Columns)
            {
                if (_cellLookup.TryGetValue((r, c), out var neighbor) && neighbor.Value == player)
                {
                    count++;
                    r += dRow;
                    c += dCol;
                }
                else break;
            }
            return count;
        }

        private bool CheckWin(int row, int col, string player)
        {
            int[][] directions = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 } };

            foreach (var dir in directions)
            {
                int count = 1;
                count += CountDirectionSimulate(row, col, dir[0], dir[1], player);
                count += CountDirectionSimulate(row, col, -dir[0], -dir[1], player);

                if (count >= 5) return true;
            }
            return false;
        }

        private void HighlightWinningCells(int row, int col, string player)
        {
            int[][] directions = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 } };

            foreach (var dir in directions)
            {
                var line = GetLine(row, col, dir[0], dir[1], player);
                var opposite = GetLine(row, col, -dir[0], -dir[1], player);
                line.AddRange(opposite);

                if (_cellLookup.TryGetValue((row, col), out var center))
                {
                    line.Add(center);
                }

                if (line.Count >= 5)
                {
                    foreach (var c in line) c.IsWinningCell = true;
                    break;
                }
            }
        }

        private List<Cell> GetLine(int row, int col, int dRow, int dCol, string player)
        {
            var list = new List<Cell>();
            int r = row + dRow;
            int c = col + dCol;

            while (r >= 0 && r < Rows && c >= 0 && c < Columns)
            {
                if (_cellLookup.TryGetValue((r, c), out var cell) && cell.Value == player)
                {
                    list.Add(cell);
                    r += dRow;
                    c += dCol;
                }
                else break;
            }
            return list;
        }

        public void ResetBoard()
        {
            foreach (var cell in Cells)
            {
                cell.Value = string.Empty;
                cell.IsWinningCell = false;
            }

            lock (_candidateLock) _candidatePositions.Clear();

            CurrentPlayer = _initialPlayer;
            IsPaused = false;

            if (AIMode == "Bậc thầy")
            {
                TryInitializeMasterEngine();
            }
        }

        public void PauseBoard() => IsPaused = true;

        private void TryInitializeMasterEngine()
        {
            DisposeEngine();

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var enginePath = Path.Combine(baseDirectory, "AI", "pbrain-rapfi-windows-avx2.exe");

            Console.WriteLine("[Engine Path] " + enginePath);

            if (string.IsNullOrWhiteSpace(enginePath) || !File.Exists(enginePath))
            {
                NotifyMasterModeUnavailable("Không tìm thấy tệp AI cần thiết cho chế độ Bậc thầy.\n" +
                                            $"Đường dẫn: {enginePath}");
                return;
            }

            try
            {
                _engine = new EngineClient(enginePath);

                if (Rows == Columns)
                {
                    _engine.StartSquare(Rows);
                }
                else
                {
                    bool ok = _engine.StartRect(Columns, Rows);
                    if (!ok)
                    {
                        MessageBox.Show("AI không hỗ trợ kích thước bàn chữ nhật. Hãy chọn bàn vuông.",
                            "Bậc thầy", MessageBoxButton.OK, MessageBoxImage.Warning);

                        DisposeEngine();
                        IsAIEnabled = false;
                        AIMode = "Khó";
                        return;
                    }
                }

                // Nếu AI đi trước (O)
                if (Cells != null && Cells.All(c => string.IsNullOrEmpty(c.Value)) && CurrentPlayer == "O")
                {
                    var aiMove = _engine.Begin();
                    PlaceAiIfValid(aiMove);
                }

            }
            catch (Exception ex)
            {
                NotifyMasterModeUnavailable($"Không thể khởi động AI Bậc thầy.\nChi tiết: {ex}");
            }
        }



        private void NotifyMasterModeUnavailable(string message)
        {
            IsAIEnabled = false;
            AIMode = "Khó";

            Application.Current.Dispatcher?.Invoke(() =>
            {
                MessageBox.Show(message, "Caro", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        public void DisposeEngine()
        {
            _engine?.Dispose();
            _engine = null;
        }
    }
}
