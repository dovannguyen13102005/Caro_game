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
        private static readonly (int dRow, int dCol)[] LineDirections =
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        private readonly GameSetup _setup;
        private readonly Dictionary<(int Row, int Col), Cell> _cellLookup;
        private readonly HashSet<(int Row, int Col)> _candidatePositions;
        private readonly object _candidateLock = new();
        private MoveEvaluation? _lastEvaluation;
        private EngineClient? _engine;

        private int _rows;
        private int _columns;
        private string _currentPlayer;
        private bool _isAIEnabled;
        private string _aiMode = "Dễ";
        private bool _isPaused;

        public BoardViewModel(int rows, int columns, string firstPlayer, string aiMode = "Dễ")
            : this(new GameSetup(rows, columns, GameRule.Freestyle, firstPlayer), aiMode)
        {
        }

        public BoardViewModel(GameSetup setup, string aiMode = "Dễ")
        {
            _setup = setup.Clone();
            _rows = _setup.Rows;
            _columns = _setup.Columns;
            _currentPlayer = _setup.FirstPlayer;
            _aiMode = aiMode;

            Cells = new ObservableCollection<Cell>();
            _cellLookup = new Dictionary<(int, int), Cell>(_rows * _columns);
            _candidatePositions = new HashSet<(int, int)>();

            InitializeCells();
            ApplyInitialPlacements();
            RebuildCandidatePositions();

            if (_aiMode == "Bậc thầy")
            {
                TryInitializeMasterEngine();
            }
        }

        public ObservableCollection<Cell> Cells { get; }
        public GameSetup Setup => _setup;

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

        public string InitialPlayer => _setup.FirstPlayer;

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

        public event EventHandler<GameEndedEventArgs>? GameEnded;

        public void MakeMove(Cell cell)
            => MakeMoveInternal(cell, false);

        private void MakeMoveFromAi(Cell cell)
            => MakeMoveInternal(cell, true);

        private void MakeMoveInternal(Cell cell, bool isAiMove)
        {
            if (IsPaused || !string.IsNullOrEmpty(cell.Value))
            {
                return;
            }

            var evaluation = ValidateMove(cell.Row, cell.Col, CurrentPlayer);
            _lastEvaluation = evaluation;

            if (!evaluation.CanPlace)
            {
                if (isAiMove)
                {
                    HandleAiViolation(evaluation);
                    evaluation.MarkHandled();
                }
                else
                {
                    NotifyInvalidMove(evaluation.FailureReason ?? "Nước đi không hợp lệ");
                }
                return;
            }

            cell.Value = CurrentPlayer;
            UpdateCandidatePositions(cell.Row, cell.Col);
            evaluation.MarkApplied();
            _lastEvaluation = evaluation;

            var finalPosition = PerformExpansionIfNeeded(cell.Row, cell.Col);
            var finalCell = _cellLookup[finalPosition];

            var summary = AnalyzePatterns(finalCell.Row, finalCell.Col, CurrentPlayer, false);

            if (evaluation.IsWin)
            {
                HighlightWinningCells(finalCell.Row, finalCell.Col, CurrentPlayer, summary);
                HandleWin(CurrentPlayer);
                return;
            }

            if (IsBoardFull())
            {
                HandleDraw();
                return;
            }

            CurrentPlayer = CurrentPlayer == "X" ? "O" : "X";

            if (IsAIEnabled && CurrentPlayer == "O")
            {
                TriggerAiTurn(finalCell.Row, finalCell.Col);
            }
        }

        public void LoadFromState(GameState state)
        {
            if (state.Rows != Rows || state.Columns != Columns)
            {
                ResizeBoard(state.Rows, state.Columns, 0, 0);
            }

            foreach (var cell in Cells)
            {
                cell.Value = string.Empty;
                cell.IsWinningCell = false;
            }

            foreach (var cellState in state.Cells)
            {
                if (_cellLookup.TryGetValue((cellState.Row, cellState.Col), out var cell))
                {
                    cell.Value = cellState.Value ?? string.Empty;
                    cell.IsWinningCell = cellState.IsWinningCell;
                }
            }

            _setup.FirstPlayer = _setup.NormalizePlayer(state.FirstPlayer ?? _setup.FirstPlayer);
            CurrentPlayer = string.IsNullOrWhiteSpace(state.CurrentPlayer)
                ? _setup.FirstPlayer
                : _setup.NormalizePlayer(state.CurrentPlayer!);

            IsPaused = state.IsPaused;
            RebuildCandidatePositions();
        }

        public void ResetBoard()
        {
            DisposeEngine();

            Rows = _setup.InitialRows;
            Columns = _setup.InitialColumns;
            _setup.Rows = Rows;
            _setup.Columns = Columns;

            InitializeCells();
            ApplyInitialPlacements();
            RebuildCandidatePositions();

            CurrentPlayer = _setup.FirstPlayer;
            IsPaused = false;

            if (AIMode == "Bậc thầy")
            {
                TryInitializeMasterEngine();
            }
        }

        public void PauseBoard() => IsPaused = true;

        public void DisposeEngine()
        {
            _engine?.Dispose();
            _engine = null;
        }

        private void InitializeCells()
        {
            Cells.Clear();
            _cellLookup.Clear();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    var cell = new Cell(r, c, this);
                    Cells.Add(cell);
                    _cellLookup[(r, c)] = cell;
                }
            }
        }

        private void ApplyInitialPlacements()
        {
            foreach (var placement in _setup.InitialPlacements)
            {
                if (_cellLookup.TryGetValue((placement.Row, placement.Col), out var cell))
                {
                    cell.Value = _setup.NormalizePlayer(placement.Player);
                }
            }
        }

        private MoveEvaluation ValidateMove(int row, int col, string player)
        {
            if (_setup.ForbiddenCells.Contains((row, col)))
            {
                return MoveEvaluation.Forbidden("Ô cấm", AnalyzePatterns(row, col, player, true));
            }

            var summary = AnalyzePatterns(row, col, player, true);
            var evaluation = ApplyRules(summary, player);
            return evaluation;
        }

        private MoveEvaluation ApplyRules(MovePatternSummary summary, string player)
        {
            var evaluation = new MoveEvaluation(summary);
            bool isFirstPlayer = _setup.IsFirstPlayer(player);

            switch (_setup.Rule)
            {
                case GameRule.Freestyle:
                case GameRule.Swap:
                case GameRule.Swap2:
                    if (summary.MaxLineLength >= 5)
                    {
                        evaluation.MarkWin();
                    }
                    break;
                case GameRule.Standard:
                    if (summary.HasExactFive)
                    {
                        evaluation.MarkWin();
                    }
                    break;
                case GameRule.Renju:
                    if (isFirstPlayer)
                    {
                        if (summary.HasOverline)
                        {
                            evaluation.Forbid("Cấm overline");
                            return evaluation;
                        }

                        if (summary.HasExactFive)
                        {
                            evaluation.MarkWin();
                            return evaluation;
                        }

                        if (summary.OpenFours >= 2)
                        {
                            evaluation.Forbid("Cấm double-four");
                            return evaluation;
                        }

                        if (summary.OpenThrees >= 2)
                        {
                            evaluation.Forbid("Cấm double-three");
                            return evaluation;
                        }
                    }

                    if (summary.HasExactFive || (!isFirstPlayer && summary.MaxLineLength >= 5))
                    {
                        evaluation.MarkWin();
                    }
                    break;
            }

            return evaluation;
        }

        private MovePatternSummary AnalyzePatterns(int row, int col, string player, bool hypothetical)
        {
            var summary = new MovePatternSummary();

            for (int i = 0; i < LineDirections.Length; i++)
            {
                var dir = LineDirections[i];
                var info = AnalyzeDirection(row, col, dir.dRow, dir.dCol, player, hypothetical);
                summary.SetDirection(i, info);
            }

            return summary;
        }

        private DirectionInfo AnalyzeDirection(int row, int col, int dRow, int dCol, string player, bool hypothetical)
        {
            int left = CountDirection(row, col, -dRow, -dCol, player, hypothetical);
            int right = CountDirection(row, col, dRow, dCol, player, hypothetical);
            int total = left + right + 1;

            bool leftOpen = IsOpenEnd(row, col, -dRow, -dCol, left, hypothetical);
            bool rightOpen = IsOpenEnd(row, col, dRow, dCol, right, hypothetical);

            return new DirectionInfo(total, leftOpen, rightOpen);
        }

        private int CountDirection(int row, int col, int dRow, int dCol, string player, bool hypothetical)
        {
            int count = 0;
            int r = row + dRow;
            int c = col + dCol;

            while (IsInside(r, c) && GetCellValue(r, c, row, col, player, hypothetical) == player)
            {
                count++;
                r += dRow;
                c += dCol;
            }

            return count;
        }

        private bool IsOpenEnd(int row, int col, int dRow, int dCol, int count, bool hypothetical)
        {
            int r = row + (count + 1) * dRow;
            int c = col + (count + 1) * dCol;

            return IsCellEmpty(r, c, hypothetical ? (row, col) : null);
        }

        private string GetCellValue(int row, int col, int hypoRow, int hypoCol, string player, bool hypothetical)
        {
            if (hypothetical && row == hypoRow && col == hypoCol)
            {
                return player;
            }

            if (_cellLookup.TryGetValue((row, col), out var cell))
            {
                return cell.Value;
            }

            return string.Empty;
        }

        private bool IsCellEmpty(int row, int col, (int Row, int Col)? hypothetical)
        {
            if (!IsInside(row, col))
            {
                return false;
            }

            if (_setup.ForbiddenCells.Contains((row, col)))
            {
                return false;
            }

            if (hypothetical.HasValue && hypothetical.Value.Row == row && hypothetical.Value.Col == col)
            {
                return false;
            }

            return _cellLookup.TryGetValue((row, col), out var cell) && string.IsNullOrEmpty(cell.Value);
        }

        private bool IsInside(int row, int col)
            => row >= 0 && col >= 0 && row < Rows && col < Columns;

        private void HighlightWinningCells(int row, int col, string player, MovePatternSummary summary)
        {
            foreach (var entry in summary.Directions)
            {
                if (!ShouldHighlight(entry, player))
                {
                    continue;
                }

                var line = GetLine(row, col, entry.DirectionIndex, player);

                foreach (var cell in line)
                {
                    cell.IsWinningCell = true;
                }
            }
        }

        private bool ShouldHighlight(DirectionEntry entry, string player)
        {
            return _setup.Rule switch
            {
                GameRule.Standard => entry.Total == 5,
                GameRule.Renju => _setup.IsFirstPlayer(player) ? entry.Total == 5 : entry.Total >= 5,
                _ => entry.Total >= 5
            };
        }

        private List<Cell> GetLine(int row, int col, int directionIndex, string player)
        {
            var dir = LineDirections[directionIndex];
            var list = new List<Cell>();

            int r = row;
            int c = col;
            while (IsInside(r, c) && _cellLookup.TryGetValue((r, c), out var cell) && cell.Value == player)
            {
                list.Insert(0, cell);
                r -= dir.dRow;
                c -= dir.dCol;
            }

            r = row + dir.dRow;
            c = col + dir.dCol;
            while (IsInside(r, c) && _cellLookup.TryGetValue((r, c), out var cell) && cell.Value == player)
            {
                list.Add(cell);
                r += dir.dRow;
                c += dir.dCol;
            }

            return list;
        }

        private bool IsBoardFull()
        {
            return Cells.All(c =>
                !string.IsNullOrEmpty(c.Value) ||
                _setup.ForbiddenCells.Contains((c.Row, c.Col)));
        }

        private void UpdateCandidatePositions(int row, int col)
        {
            lock (_candidateLock)
            {
                _candidatePositions.Remove((row, col));

                foreach (var neighbor in GetNeighbors(row, col, 2))
                {
                    if (string.IsNullOrEmpty(neighbor.Value) && !_setup.ForbiddenCells.Contains((neighbor.Row, neighbor.Col)))
                    {
                        _candidatePositions.Add((neighbor.Row, neighbor.Col));
                    }
                }
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

        private void RebuildCandidatePositions()
        {
            lock (_candidateLock)
            {
                _candidatePositions.Clear();

                foreach (var filled in Cells.Where(c => !string.IsNullOrEmpty(c.Value)))
                {
                    foreach (var neighbor in GetNeighbors(filled.Row, filled.Col, 2))
                    {
                        if (string.IsNullOrEmpty(neighbor.Value) && !_setup.ForbiddenCells.Contains((neighbor.Row, neighbor.Col)))
                        {
                            _candidatePositions.Add((neighbor.Row, neighbor.Col));
                        }
                    }
                }
            }
        }

        private (int Row, int Col) PerformExpansionIfNeeded(int row, int col)
        {
            if (!_setup.AllowExpansion || AIMode == "Bậc thầy")
            {
                return (row, col);
            }

            bool addTop = row <= _setup.ExpansionThreshold;
            bool addBottom = (Rows - 1 - row) <= _setup.ExpansionThreshold;
            bool addLeft = col <= _setup.ExpansionThreshold;
            bool addRight = (Columns - 1 - col) <= _setup.ExpansionThreshold;

            int newRows = Rows;
            int newCols = Columns;
            int rowShift = 0;
            int colShift = 0;

            if (addTop && newRows < _setup.MaxRows)
            {
                newRows++;
                rowShift = 1;
            }
            else
            {
                addTop = false;
            }

            if (addBottom && newRows < _setup.MaxRows)
            {
                newRows++;
            }
            else
            {
                addBottom = false;
            }

            if (addLeft && newCols < _setup.MaxColumns)
            {
                newCols++;
                colShift = 1;
            }
            else
            {
                addLeft = false;
            }

            if (addRight && newCols < _setup.MaxColumns)
            {
                newCols++;
            }
            else
            {
                addRight = false;
            }

            if (newRows != Rows || newCols != Columns)
            {
                ResizeBoard(newRows, newCols, rowShift, colShift);
                row += rowShift;
                col += colShift;
            }

            return (row, col);
        }

        private void ResizeBoard(int newRows, int newCols, int rowShift, int colShift)
        {
            var snapshot = Cells.ToDictionary(c => (c.Row, c.Col), c => (c.Value, c.IsWinningCell));

            Rows = newRows;
            Columns = newCols;
            _setup.Rows = newRows;
            _setup.Columns = newCols;

            Cells.Clear();
            _cellLookup.Clear();

            for (int r = 0; r < newRows; r++)
            {
                for (int c = 0; c < newCols; c++)
                {
                    var cell = new Cell(r, c, this);
                    if (snapshot.TryGetValue((r - rowShift, c - colShift), out var state))
                    {
                        cell.Value = state.Item1;
                        cell.IsWinningCell = state.Item2;
                    }
                    Cells.Add(cell);
                    _cellLookup[(r, c)] = cell;
                }
            }

            RebuildCandidatePositions();
        }

        private void TriggerAiTurn(int lastRow, int lastCol)
        {
            if (AIMode == "Bậc thầy" && _engine != null)
            {
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Application.Current.MainWindow?.DataContext is MainViewModel mainVM)
                        {
                            mainVM.SetStatus("AI đang suy nghĩ...");
                        }
                    });
                }

                Task.Run(() =>
                {
                    try
                    {
                        string aiMove = _engine!.Turn(lastCol, lastRow);
                        PlaceAiIfValid(aiMove);

                        if (Application.Current?.Dispatcher != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Application.Current.MainWindow?.DataContext is MainViewModel mainVM)
                                {
                                    mainVM.SetStatus("Đang chơi");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        if (Application.Current?.Dispatcher != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"AI engine error: {ex.Message}", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                        DisposeEngine();
                    }
                });
            }
            else
            {
                Task.Run(AIMove);
            }
        }

        private void AIMove()
        {
            if (!IsAIEnabled || IsPaused)
            {
                return;
            }

            Cell? bestCell = null;

            if (AIMode == "Dễ")
            {
                var lastPlayerMove = Cells.LastOrDefault(c => c.Value == "X");
                if (lastPlayerMove != null)
                {
                    var neighbors = Cells.Where(c =>
                        string.IsNullOrEmpty(c.Value) &&
                        Math.Abs(c.Row - lastPlayerMove.Row) <= 1 &&
                        Math.Abs(c.Col - lastPlayerMove.Col) <= 1 &&
                        !_setup.ForbiddenCells.Contains((c.Row, c.Col))).ToList();

                    if (neighbors.Any())
                    {
                        bestCell = neighbors[new Random().Next(neighbors.Count)];
                    }
                }

                if (bestCell == null)
                {
                    var emptyCells = Cells
                        .Where(c => string.IsNullOrEmpty(c.Value) && !_setup.ForbiddenCells.Contains((c.Row, c.Col)))
                        .ToList();

                    if (emptyCells.Any())
                    {
                        bestCell = emptyCells[new Random().Next(emptyCells.Count)];
                    }
                }
            }
            else
            {
                var candidates = Cells
                    .Where(c => string.IsNullOrEmpty(c.Value) && !_setup.ForbiddenCells.Contains((c.Row, c.Col)) && HasNeighbor(c, 2))
                    .ToList();

                if (!candidates.Any())
                {
                    candidates = Cells
                        .Where(c => string.IsNullOrEmpty(c.Value) && !_setup.ForbiddenCells.Contains((c.Row, c.Col)))
                        .ToList();
                }

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
                Application.Current?.Dispatcher?.Invoke(() => MakeMoveFromAi(bestCell));
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

            foreach (var dir in LineDirections)
            {
                int count = 1;
                count += CountDirectionSimulate(cell.Row, cell.Col, dir.dRow, dir.dCol, player);
                count += CountDirectionSimulate(cell.Row, cell.Col, -dir.dRow, -dir.dCol, player);

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

            while (IsInside(r, c))
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

        private void PlaceAiIfValid(string aiMove)
        {
            if (string.IsNullOrWhiteSpace(aiMove)) return;

            var parts = aiMove.Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int aiX) &&
                int.TryParse(parts[1], out int aiY) &&
                _cellLookup.TryGetValue((aiY, aiX), out var aiCell))
            {
                Application.Current?.Dispatcher?.Invoke(() => MakeMoveFromAi(aiCell));

                if (_lastEvaluation != null && !_lastEvaluation.CanPlace && !_lastEvaluation.WasHandled)
                {
                    HandleAiViolation(_lastEvaluation);
                    _lastEvaluation.MarkHandled();
                }
            }
        }

        private void TryInitializeMasterEngine()
        {
            DisposeEngine();

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var enginePath = Path.Combine(baseDirectory, "AI", "pbrain-rapfi-windows-avx2.exe");

            if (!File.Exists(enginePath))
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

                if (Cells.All(c => string.IsNullOrEmpty(c.Value)) && CurrentPlayer == "O")
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

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                MessageBox.Show(message, "Caro", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void NotifyInvalidMove(string reason)
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow?.DataContext is MainViewModel mainVM)
                    {
                        mainVM.SetStatus(reason);
                    }

                    MessageBox.Show(reason, "Nước không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
        }

        private void HandleAiViolation(MoveEvaluation evaluation)
        {
            string reason = evaluation.FailureReason ?? "AI đã phạm luật";

            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"AI phạm luật: {reason}. Bạn thắng!", "AI", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }

            string winner = CurrentPlayer == "O" ? "X" : "O";
            HandleWin(winner);
        }

        private void HandleWin(string winner)
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new Views.WinDialog($"Người chơi {winner} thắng!")
                    {
                        Owner = Application.Current.MainWindow
                    };

                    dialog.ShowDialog();

                    GameEnded?.Invoke(this, new GameEndedEventArgs(winner, dialog.IsPlayAgain, true));

                    if (dialog.IsPlayAgain)
                    {
                        ResetBoard();
                    }
                    else
                    {
                        DisposeEngine();
                        Application.Current?.Shutdown();
                    }
                });
            }
            else
            {
                GameEnded?.Invoke(this, new GameEndedEventArgs(winner, false, true));
            }
        }

        private void HandleDraw()
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Hòa cờ! Bàn đã đầy mà không có người thắng.",
                        "Kết thúc ván", MessageBoxButton.OK, MessageBoxImage.Information);

                    GameEnded?.Invoke(this, new GameEndedEventArgs(null!, false, false));
                });
            }
            else
            {
                GameEnded?.Invoke(this, new GameEndedEventArgs(null!, false, false));
            }
        }

        private sealed class MoveEvaluation
        {
            public MoveEvaluation(MovePatternSummary summary)
            {
                Summary = summary;
            }

            public MovePatternSummary Summary { get; }
            public bool CanPlace { get; private set; } = true;
            public bool IsWin { get; private set; }
            public string? FailureReason { get; private set; }
            public bool WasApplied { get; private set; }
            public bool WasHandled { get; private set; }

            public static MoveEvaluation Forbidden(string reason, MovePatternSummary summary)
            {
                var evaluation = new MoveEvaluation(summary);
                evaluation.Forbid(reason);
                return evaluation;
            }

            public void Forbid(string reason)
            {
                CanPlace = false;
                FailureReason = reason;
            }

            public void MarkWin()
            {
                IsWin = true;
            }

            public void MarkApplied()
            {
                WasApplied = true;
            }

            public void MarkHandled()
            {
                WasHandled = true;
            }
        }

        private sealed class MovePatternSummary
        {
            private readonly DirectionEntry[] _directions =
                Enumerable.Range(0, LineDirections.Length)
                    .Select(i => new DirectionEntry(i, 0, false, false))
                    .ToArray();

            public IEnumerable<DirectionEntry> Directions => _directions;

            public bool HasExactFive => _directions.Any(d => d.Total == 5);
            public bool HasOverline => _directions.Any(d => d.Total >= 6);
            public int MaxLineLength => _directions.Max(d => d.Total);
            public int OpenThrees => _directions.Count(d => d.Total == 3 && d.LeftOpen && d.RightOpen);
            public int OpenFours => _directions.Count(d => d.Total == 4 && d.LeftOpen && d.RightOpen);

            public void SetDirection(int index, DirectionInfo info)
            {
                _directions[index] = new DirectionEntry(index, info.Total, info.LeftOpen, info.RightOpen);
            }
        }

        private readonly record struct DirectionEntry(int DirectionIndex, int Total, bool LeftOpen, bool RightOpen);

        private readonly record struct DirectionInfo(int Total, bool LeftOpen, bool RightOpen);
    }
}
