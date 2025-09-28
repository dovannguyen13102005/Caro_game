using Caro_game.Commands;
using Caro_game.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;

namespace Caro_game.ViewModels
{
    public class BoardViewModel : BaseViewModel
    {
        private static readonly Random _random = new();
        private static readonly object _randomLock = new();

        public int Rows { get; private set; }
        public int Columns { get; private set; }
        public ObservableCollection<Cell> Cells { get; private set; }

        private string _currentPlayer;
        public string CurrentPlayer
        {
            get => _currentPlayer;
            set
            {
                _currentPlayer = value;
                OnPropertyChanged();
            }
        }

        private bool _isAIEnabled;
        public bool IsAIEnabled
        {
            get => _isAIEnabled;
            set
            {
                _isAIEnabled = value;
                OnPropertyChanged();
            }
        }

        private string _aiMode;
        public string AIMode
        {
            get => _aiMode;
            set
            {
                _aiMode = value;
                OnPropertyChanged();
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

                    if (!_isPaused && IsAIEnabled && CurrentPlayer == "O" && !_isGameOver)
                    {
                        Task.Run(() => AIMove());
                    }
                }
            }
        }

        private bool _isSoundEnabled;
        public bool IsSoundEnabled
        {
            get => _isSoundEnabled;
            set
            {
                _isSoundEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isGameOver;

        public BoardViewModel(int rows, int columns, string firstPlayer)
        {
            Rows = rows;
            Columns = columns;
            CurrentPlayer = firstPlayer.StartsWith("X") ? "X" : "O";
            InitializeCells();
        }

        public BoardViewModel(GameState state)
        {
            Rows = state.Rows;
            Columns = state.Columns;
            CurrentPlayer = string.IsNullOrEmpty(state.CurrentPlayer) ? "X" : state.CurrentPlayer;
            IsAIEnabled = state.IsAIEnabled;
            AIMode = state.AIMode;
            InitializeCells(state.Cells);
            IsPaused = state.IsPaused;
        }

        private void InitializeCells(string[]? values = null)
        {
            Cells = new ObservableCollection<Cell>();
            _isGameOver = false;
            _isPaused = false;

            int total = Rows * Columns;
            for (int i = 0; i < total; i++)
            {
                int r = i / Columns;
                int c = i % Columns;
                var cell = new Cell(r, c, this)
                {
                    Value = values != null && i < values.Length ? values[i] : string.Empty,
                    IsWinningCell = false
                };
                cell.ClickCommand = new RelayCommand(_ => MakeMove(cell));
                Cells.Add(cell);
            }
        }

        public GameState ToGameState()
        {
            return new GameState
            {
                Rows = Rows,
                Columns = Columns,
                CurrentPlayer = CurrentPlayer,
                IsAIEnabled = IsAIEnabled,
                AIMode = AIMode,
                IsPaused = IsPaused,
                Cells = Cells.Select(c => c.Value ?? string.Empty).ToArray()
            };
        }

        public void MakeMove(Cell cell)
        {
            if (_isGameOver || IsPaused || !string.IsNullOrEmpty(cell.Value))
            {
                return;
            }

            cell.Value = CurrentPlayer;
            PlayMoveSound(false);

            if (CheckWin(cell.Row, cell.Col, CurrentPlayer))
            {
                _isGameOver = true;
                HighlightWinningCells(cell.Row, cell.Col, CurrentPlayer);
                PlayMoveSound(true);

                App.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new Views.WinDialog($"Người chơi {CurrentPlayer} thắng!")
                    {
                        Owner = Application.Current.MainWindow
                    };

                    bool? result = dialog.ShowDialog();

                    if (result == true && dialog.IsPlayAgain)
                    {
                        ResetBoard();
                    }
                    else
                    {
                        Application.Current.Shutdown();
                    }
                });
                return;
            }

            CurrentPlayer = CurrentPlayer == "X" ? "O" : "X";

            if (IsAIEnabled && CurrentPlayer == "O")
            {
                Task.Run(() => AIMove());
            }
        }

        private void AIMove()
        {
            if (!IsAIEnabled || _isGameOver || IsPaused)
            {
                return;
            }

            var emptyCells = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();
            if (!emptyCells.Any())
            {
                return;
            }

            Cell? bestCell = emptyCells.FirstOrDefault(c => WouldWin(c, "O"));
            if (bestCell != null)
            {
                App.Current.Dispatcher.Invoke(() => MakeMove(bestCell));
                return;
            }

            Cell? blockCell = emptyCells.FirstOrDefault(c => WouldWin(c, "X"));
            if (blockCell != null)
            {
                App.Current.Dispatcher.Invoke(() => MakeMove(blockCell));
                return;
            }

            if (AIMode == "Dễ")
            {
                bestCell = ChooseEasyMove(emptyCells);
            }
            else
            {
                bestCell = ChooseAdvancedMove(emptyCells);
            }

            if (bestCell != null)
            {
                App.Current.Dispatcher.Invoke(() => MakeMove(bestCell));
            }
        }

        private Cell? ChooseEasyMove(System.Collections.Generic.IList<Cell> emptyCells)
        {
            var lastPlayerMove = Cells.LastOrDefault(c => c.Value == "X");
            if (lastPlayerMove != null)
            {
                var neighbors = emptyCells
                    .Where(c => Math.Abs(c.Row - lastPlayerMove.Row) <= 1 && Math.Abs(c.Col - lastPlayerMove.Col) <= 1)
                    .ToList();

                if (neighbors.Any())
                {
                    lock (_randomLock)
                    {
                        return neighbors[_random.Next(neighbors.Count)];
                    }
                }
            }

            lock (_randomLock)
            {
                return emptyCells[_random.Next(emptyCells.Count)];
            }
        }

        private Cell? ChooseAdvancedMove(System.Collections.Generic.IList<Cell> emptyCells)
        {
            var candidates = emptyCells
                .Where(c => HasNeighbor(c, 2))
                .ToList();

            if (!candidates.Any())
            {
                candidates = emptyCells.ToList();
            }

            Cell? bestCell = null;
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

            if (bestCell == null)
            {
                lock (_randomLock)
                {
                    bestCell = emptyCells[_random.Next(emptyCells.Count)];
                }
            }

            return bestCell;
        }

        private bool HasNeighbor(Cell cell, int range)
        {
            for (int dr = -range; dr <= range; dr++)
            {
                for (int dc = -range; dc <= range; dc++)
                {
                    if (dr == 0 && dc == 0)
                    {
                        continue;
                    }

                    int r = cell.Row + dr;
                    int c = cell.Col + dc;
                    var neighbor = GetCell(r, c);
                    if (neighbor != null && !string.IsNullOrEmpty(neighbor.Value))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private int EvaluateCellAdvanced(Cell cell)
        {
            int score = 0;
            score += EvaluatePotential(cell, "O", true);
            score += EvaluatePotential(cell, "X", false);
            score += ProximityScore(cell, "X") * 8;

            int centerRow = Rows / 2;
            int centerCol = Columns / 2;
            score += Math.Max(0, 6 - Math.Abs(cell.Row - centerRow));
            score += Math.Max(0, 6 - Math.Abs(cell.Col - centerCol));

            return score;
        }

        private int EvaluatePotential(Cell cell, string player, bool isSelf)
        {
            int totalScore = 0;
            int[][] directions = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 } };

            foreach (var dir in directions)
            {
                int forward = CountDirection(cell.Row, cell.Col, dir[0], dir[1], player);
                int backward = CountDirection(cell.Row, cell.Col, -dir[0], -dir[1], player);

                bool openForward = IsOpenEnd(cell.Row, cell.Col, dir[0], dir[1], forward);
                bool openBackward = IsOpenEnd(cell.Row, cell.Col, -dir[0], -dir[1], backward);

                totalScore += ScoreLine(forward, backward, openForward, openBackward, isSelf);
            }

            return totalScore;
        }

        private int ScoreLine(int forward, int backward, bool openForward, bool openBackward, bool isSelf)
        {
            int total = forward + backward + 1;
            int openEnds = (openForward ? 1 : 0) + (openBackward ? 1 : 0);

            if (total >= 5)
            {
                return 100000;
            }

            if (total == 4 && openEnds == 2)
            {
                return isSelf ? 12000 : 10000;
            }

            if (total == 4 && openEnds == 1)
            {
                return isSelf ? 6000 : 4000;
            }

            if (total == 3 && openEnds == 2)
            {
                return isSelf ? 2500 : 2000;
            }

            if (total == 3 && openEnds == 1)
            {
                return isSelf ? 900 : 700;
            }

            if (total == 2 && openEnds == 2)
            {
                return isSelf ? 450 : 300;
            }

            if (total == 2 && openEnds == 1)
            {
                return isSelf ? 150 : 100;
            }

            return openEnds > 0 ? 50 : 0;
        }

        private int ProximityScore(Cell cell, string player)
        {
            int score = 0;
            int[][] dirs =
            {
                new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 },
                new[] { -1, 0 }, new[] { 0, -1 }, new[] { -1, -1 }, new[] { -1, 1 }
            };

            foreach (var dir in dirs)
            {
                int r = cell.Row + dir[0];
                int c = cell.Col + dir[1];
                var neighbor = GetCell(r, c);
                if (neighbor != null && neighbor.Value == player)
                {
                    score += 1;
                }
            }
            return score;
        }

        private bool WouldWin(Cell cell, string player)
        {
            int[][] directions = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 } };

            foreach (var dir in directions)
            {
                int count = 1;
                count += CountDirection(cell.Row, cell.Col, dir[0], dir[1], player);
                count += CountDirection(cell.Row, cell.Col, -dir[0], -dir[1], player);

                if (count >= 5)
                {
                    return true;
                }
            }
            return false;
        }

        private int CountDirection(int row, int col, int dRow, int dCol, string player)
        {
            int count = 0;
            int r = row + dRow;
            int c = col + dCol;

            while (r >= 0 && r < Rows && c >= 0 && c < Columns)
            {
                var neighbor = GetCell(r, c);
                if (neighbor != null && neighbor.Value == player)
                {
                    count++;
                    r += dRow;
                    c += dCol;
                }
                else
                {
                    break;
                }
            }

            return count;
        }

        private bool IsOpenEnd(int row, int col, int dRow, int dCol, int distance)
        {
            int r = row + dRow * (distance + 1);
            int c = col + dCol * (distance + 1);

            if (r < 0 || r >= Rows || c < 0 || c >= Columns)
            {
                return false;
            }

            var neighbor = GetCell(r, c);
            return neighbor != null && string.IsNullOrEmpty(neighbor.Value);
        }

        private Cell? GetCell(int row, int col)
        {
            return Cells.FirstOrDefault(x => x.Row == row && x.Col == col);
        }

        private bool CheckWin(int row, int col, string player)
        {
            int[][] directions = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 } };

            foreach (var dir in directions)
            {
                int count = 1;
                count += CountDirection(row, col, dir[0], dir[1], player);
                count += CountDirection(row, col, -dir[0], -dir[1], player);

                if (count >= 5)
                {
                    return true;
                }
            }
            return false;
        }

        private void HighlightWinningCells(int row, int col, string player)
        {
            int[][] directions = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 } };

            foreach (var dir in directions)
            {
                var line = GetLine(row, col, dir[0], dir[1], player);
                var oppositeLine = GetLine(row, col, -dir[0], -dir[1], player);

                foreach (var c in oppositeLine)
                {
                    line.Add(c);
                }

                var centerCell = GetCell(row, col);
                if (centerCell != null)
                {
                    line.Add(centerCell);
                }

                if (line.Count >= 5)
                {
                    foreach (var c in line)
                    {
                        c.IsWinningCell = true;
                    }
                    break;
                }
            }
        }

        private ObservableCollection<Cell> GetLine(int row, int col, int dRow, int dCol, string player)
        {
            var list = new ObservableCollection<Cell>();
            int r = row + dRow;
            int c = col + dCol;

            while (r >= 0 && r < Rows && c >= 0 && c < Columns)
            {
                var cell = GetCell(r, c);
                if (cell != null && cell.Value == player)
                {
                    list.Add(cell);
                    r += dRow;
                    c += dCol;
                }
                else
                {
                    break;
                }
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
            _isGameOver = false;
            IsPaused = false;
            CurrentPlayer = "X";
        }

        private void PlayMoveSound(bool isWin)
        {
            if (!IsSoundEnabled)
            {
                return;
            }

            if (isWin)
            {
                SystemSounds.Exclamation.Play();
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }
        }
    }
}
