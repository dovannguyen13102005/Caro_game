using Caro_game.Commands;
using Caro_game.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Caro_game.ViewModels
{
    public class BoardViewModel : BaseViewModel
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public ObservableCollection<Cell> Cells { get; set; }

        private string _currentPlayer;
        public string CurrentPlayer
        {
            get => _currentPlayer;
            set { _currentPlayer = value; OnPropertyChanged(); }
        }

        private bool _isAIEnabled;
        public bool IsAIEnabled
        {
            get => _isAIEnabled;
            set { _isAIEnabled = value; OnPropertyChanged(); }
        }

        private string _aiMode;
        public string AIMode
        {
            get => _aiMode;
            set { _aiMode = value; OnPropertyChanged(); }
        }

        public BoardViewModel(int rows, int columns, string firstPlayer)
        {
            Rows = rows;
            Columns = columns;
            CurrentPlayer = firstPlayer.StartsWith("X") ? "X" : "O";

            Cells = new ObservableCollection<Cell>();

            for (int i = 0; i < rows * columns; i++)
            {
                int r = i / columns;
                int c = i % columns;
                var cell = new Cell(r, c, this);
                cell.ClickCommand = new RelayCommand(_ => MakeMove(cell));
                Cells.Add(cell);
            }
        }

        public void MakeMove(Cell cell)
        {
            if (!string.IsNullOrEmpty(cell.Value)) return;

            cell.Value = CurrentPlayer;

            if (CheckWin(cell.Row, cell.Col, CurrentPlayer))
            {
                HighlightWinningCells(cell.Row, cell.Col, CurrentPlayer);

                App.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new Caro_game.Views.WinDialog($"Người chơi {CurrentPlayer} thắng!");
                    dialog.Owner = Application.Current.MainWindow;
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

            CurrentPlayer = (CurrentPlayer == "X") ? "O" : "X";

            if (IsAIEnabled && CurrentPlayer == "O")
            {
                Task.Run(() => AIMove());
            }
        }

        private void AIMove()
        {
            if (!IsAIEnabled) return;

            Cell bestCell = null;

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
            else
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
                App.Current.Dispatcher.Invoke(() => MakeMove(bestCell));
            }
        }

        private bool HasNeighbor(Cell cell, int range)
        {
            for (int dr = -range; dr <= range; dr++)
            {
                for (int dc = -range; dc <= range; dc++)
                {
                    if (dr == 0 && dc == 0) continue;

                    int r = cell.Row + dr;
                    int c = cell.Col + dc;

                    var neighbor = Cells.FirstOrDefault(x => x.Row == r && x.Col == c);
                    if (neighbor != null && !string.IsNullOrEmpty(neighbor.Value))
                        return true;
                }
            }
            return false;
        }

        private int EvaluateCellAdvanced(Cell cell)
        {
            int score = 0;

            score += EvaluatePotential(cell, "O") * 1;
            score += EvaluatePotential(cell, "X") * 2;
            score += ProximityScore(cell, "X") * 5;

            return score;
        }

        private int ProximityScore(Cell cell, string player)
        {
            int score = 0;
            int[][] dirs = {
                new[] {0,1}, new[] {1,0}, new[] {1,1}, new[] {1,-1},
                new[] {-1,0}, new[] {0,-1}, new[] {-1,-1}, new[] {-1,1}
            };

            foreach (var dir in dirs)
            {
                int r = cell.Row + dir[0];
                int c = cell.Col + dir[1];
                var neighbor = Cells.FirstOrDefault(x => x.Row == r && x.Col == c);
                if (neighbor != null && neighbor.Value == player)
                    score += 1;
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

                switch (count)
                {
                    case 5: totalScore += 10000; break;
                    case 4: totalScore += 1000; break;
                    case 3: totalScore += 100; break;
                    case 2: totalScore += 10; break;
                }
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
                var neighbor = Cells.FirstOrDefault(x => x.Row == r && x.Col == c);
                if (neighbor != null && neighbor.Value == player)
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
                var oppositeLine = GetLine(row, col, -dir[0], -dir[1], player);

                foreach (var c in oppositeLine) line.Add(c);

                var centerCell = Cells.First(c => c.Row == row && c.Col == col);
                line.Add(centerCell);

                if (line.Count >= 5)
                {
                    foreach (var c in line)
                        c.IsWinningCell = true;
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
                var cell = Cells.FirstOrDefault(x => x.Row == r && x.Col == c);
                if (cell != null && cell.Value == player)
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
            CurrentPlayer = "X";
        }
    }
}
