using Caro_game.Commands;
using Caro_game.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public BoardViewModel(int rows, int columns, string firstPlayer)
        {
            Rows = rows;
            Columns = columns;
            CurrentPlayer = firstPlayer.StartsWith("X", StringComparison.OrdinalIgnoreCase) ? "X" : "O";

            _initialPlayer = CurrentPlayer;
            Cells = new ObservableCollection<Cell>();
            _cellLookup = new Dictionary<(int, int), Cell>(rows * columns);
            _candidatePositions = new HashSet<(int, int)>();

            for (int i = 0; i < rows * columns; i++)
            {
                int r = i / columns;
                int c = i % columns;
                var cell = new Cell(r, c, this)
                {
                    ClickCommand = new RelayCommand(_ => MakeMove(cell))
                };
                Cells.Add(cell);
                _cellLookup[(r, c)] = cell;
            }
        }

        public void MakeMove(Cell cell)
        {
            if (IsPaused || !string.IsNullOrEmpty(cell.Value))
            {
                return;
            }

            cell.Value = CurrentPlayer;
            UpdateCandidatePositions(cell.Row, cell.Col);

            if (CheckWin(cell.Row, cell.Col, CurrentPlayer))
            {
                HighlightWinningCells(cell.Row, cell.Col, CurrentPlayer);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new Views.WinDialog($"Người chơi {CurrentPlayer} thắng!")
                    {
                        Owner = Application.Current.MainWindow
                    };

                    dialog.ShowDialog();

                    GameEnded?.Invoke(this, new GameEndedEventArgs(CurrentPlayer, dialog.IsPlayAgain, true));

                    if (dialog.IsPlayAgain)
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
                    var neighbors = GetNeighbors(lastPlayerMove.Row, lastPlayerMove.Col, 1)
                        .Where(c => string.IsNullOrEmpty(c.Value))
                        .ToList();

                    if (neighbors.Any())
                    {
                        bestCell = neighbors[Random.Shared.Next(neighbors.Count)];
                    }
                }

                if (bestCell == null)
                {
                    var emptyCells = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();
                    if (emptyCells.Any())
                    {
                        bestCell = emptyCells[Random.Shared.Next(emptyCells.Count)];
                    }
                }
            }
            else
            {
                List<Cell> candidateCells;
                lock (_candidateLock)
                {
                    candidateCells = _candidatePositions
                        .Select(pos => _cellLookup[pos])
                        .Where(c => string.IsNullOrEmpty(c.Value))
                        .ToList();
                }

                if (!candidateCells.Any())
                {
                    candidateCells = Cells
                        .Where(c => string.IsNullOrEmpty(c.Value) && HasNeighbor(c, 1))
                        .ToList();
                }

                if (!candidateCells.Any())
                {
                    if (_cellLookup.TryGetValue((Rows / 2, Columns / 2), out var center) && string.IsNullOrEmpty(center.Value))
                    {
                        candidateCells.Add(center);
                    }
                }

                if (!candidateCells.Any())
                {
                    candidateCells = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();
                }

                int bestScore = int.MinValue;

                foreach (var candidate in candidateCells)
                {
                    int score = EvaluateCellAdvanced(candidate);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCell = candidate;
                    }
                }
            }

            if (bestCell != null)
            {
                Application.Current.Dispatcher.Invoke(() => MakeMove(bestCell));
            }
        }

        private IEnumerable<Cell> GetNeighbors(int row, int col, int range)
        {
            for (int dr = -range; dr <= range; dr++)
            {
                for (int dc = -range; dc <= range; dc++)
                {
                    if (dr == 0 && dc == 0)
                    {
                        continue;
                    }

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
                if (neighbor.Value == player)
                {
                    score += 1;
                }
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
                else
                {
                    break;
                }
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
                var opposite = GetLine(row, col, -dir[0], -dir[1], player);
                line.AddRange(opposite);

                if (_cellLookup.TryGetValue((row, col), out var center))
                {
                    line.Add(center);
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

            lock (_candidateLock)
            {
                _candidatePositions.Clear();
            }

            CurrentPlayer = _initialPlayer;
            IsPaused = false;
        }

        public void PauseBoard()
        {
            IsPaused = true;
        }
    }
}
