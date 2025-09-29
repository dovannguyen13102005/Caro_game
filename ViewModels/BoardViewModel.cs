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
        private int _rows;
        public int Rows
        {
            get => _rows;
            private set
            {
                if (_rows != value)
                {
                    _rows = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _columns;
        public int Columns
        {
            get => _columns;
            private set
            {
                if (_columns != value)
                {
                    _columns = value;
                    OnPropertyChanged();
                }
            }
        }
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
            int originalRow = cell.Row;
            int originalCol = cell.Col;

            cell.Value = movingPlayer;

            if (!(IsAIEnabled && AIMode == "Bậc thầy"))
            {
                ExpandBoardIfNeeded(originalRow, originalCol);
            }

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


        public void LoadFromState(GameState state)
        {
            if (state.Rows != Rows || state.Columns != Columns)
            {
                throw new ArgumentException("Kích thước bàn không khớp với trạng thái đã lưu.");
            }

            foreach (var cell in Cells)
            {
                cell.Value = string.Empty;
                cell.IsWinningCell = false;
            }

            if (state.Cells != null)
            {
                foreach (var cellState in state.Cells)
                {
                    if (_cellLookup.TryGetValue((cellState.Row, cellState.Col), out var cell))
                    {
                        cell.Value = cellState.Value ?? string.Empty;
                        cell.IsWinningCell = cellState.IsWinningCell;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(state.CurrentPlayer))
            {
                CurrentPlayer = state.CurrentPlayer!;
            }

            RebuildCandidatePositions();

            IsPaused = state.IsPaused;
        }

        private void RebuildCandidatePositions()
        {
            lock (_candidateLock)
            {
                _candidatePositions.Clear();

                foreach (var filled in Cells.Where(c => !string.IsNullOrEmpty(c.Value)))
                {
                    foreach (var neighbor in GetNeighbors(filled.Row, filled.Col, 2))
                    {
                        if (string.IsNullOrEmpty(neighbor.Value))
                        {
                            _candidatePositions.Add((neighbor.Row, neighbor.Col));
                        }
                    }
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

        private void ExpandBoardIfNeeded(int originalRow, int originalCol)
        {
            int previousRows = Rows;
            int previousCols = Columns;

            bool addTop = originalRow == 0;
            bool addBottom = originalRow == previousRows - 1;
            bool addLeft = originalCol == 0;
            bool addRight = originalCol == previousCols - 1;

            bool expanded = addTop || addBottom || addLeft || addRight;

            if (!expanded)
            {
                return;
            }

            if (addTop)
            {
                AddRowTop();
            }

            if (addBottom)
            {
                AddRowBottom();
            }

            if (addLeft)
            {
                AddColumnLeft();
            }

            if (addRight)
            {
                AddColumnRight();
            }

            RebuildCellsCollection();
        }

        private void AddRowTop()
        {
            ShiftAllCells(1, 0);

            int currentColumns = Columns;
            for (int col = 0; col < currentColumns; col++)
            {
                var cell = new Cell(0, col, this);
                _cellLookup[(0, col)] = cell;
            }

            Rows = Rows + 1;
        }

        private void AddRowBottom()
        {
            int newRowIndex = Rows;
            for (int col = 0; col < Columns; col++)
            {
                var cell = new Cell(newRowIndex, col, this);
                _cellLookup[(newRowIndex, col)] = cell;
            }

            Rows = Rows + 1;
        }

        private void AddColumnLeft()
        {
            ShiftAllCells(0, 1);

            int currentRows = Rows;
            for (int row = 0; row < currentRows; row++)
            {
                var cell = new Cell(row, 0, this);
                _cellLookup[(row, 0)] = cell;
            }

            Columns = Columns + 1;
        }

        private void AddColumnRight()
        {
            int newColumnIndex = Columns;
            for (int row = 0; row < Rows; row++)
            {
                var cell = new Cell(row, newColumnIndex, this);
                _cellLookup[(row, newColumnIndex)] = cell;
            }

            Columns = Columns + 1;
        }

        private void ShiftAllCells(int rowDelta, int colDelta)
        {
            if (rowDelta == 0 && colDelta == 0)
            {
                return;
            }

            var shiftedLookup = new Dictionary<(int Row, int Col), Cell>(_cellLookup.Count);

            foreach (var cell in _cellLookup.Values)
            {
                cell.Row += rowDelta;
                cell.Col += colDelta;
                shiftedLookup[(cell.Row, cell.Col)] = cell;
            }

            _cellLookup.Clear();
            foreach (var kvp in shiftedLookup)
            {
                _cellLookup[kvp.Key] = kvp.Value;
            }

            if (_candidatePositions.Count > 0)
            {
                var shiftedCandidates = new HashSet<(int Row, int Col)>(_candidatePositions.Count);
                foreach (var (row, col) in _candidatePositions)
                {
                    shiftedCandidates.Add((row + rowDelta, col + colDelta));
                }

                _candidatePositions.Clear();
                foreach (var pos in shiftedCandidates)
                {
                    _candidatePositions.Add(pos);
                }
            }
        }

        private void RebuildCellsCollection()
        {
            var ordered = _cellLookup.Values
                .OrderBy(c => c.Row)
                .ThenBy(c => c.Col)
                .ToList();

            Cells.Clear();

            foreach (var cell in ordered)
            {
                Cells.Add(cell);
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
