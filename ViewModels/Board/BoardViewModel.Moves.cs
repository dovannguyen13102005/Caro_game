using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Caro_game.Models;
using Caro_game.Views;

namespace Caro_game.ViewModels;

public partial class BoardViewModel
{
    public void MakeHumanMove(Cell cell)
        => ExecuteMove(cell, isAiMove: false);

    private void ExecuteMove(Cell cell, bool isAiMove)
    {
        if (IsPaused || !string.IsNullOrEmpty(cell.Value))
        {
            return;
        }

        if (!isAiMove && IsAIEnabled && CurrentPlayer != _humanSymbol)
        {
            return;
        }

        var movingPlayer = CurrentPlayer;
        int originalRow = cell.Row;
        int originalCol = cell.Col;

        cell.Value = movingPlayer;

        if (!(IsAIEnabled && AIMode == "Chuyên nghiệp"))
        {
            ExpandBoardIfNeeded(originalRow, originalCol);
        }

        UpdateCandidatePositions(cell.Row, cell.Col);

        if (CheckWin(cell.Row, cell.Col, movingPlayer))
        {
            HighlightWinningCells(cell.Row, cell.Col, movingPlayer);

            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new WinDialog($"Người chơi {movingPlayer} thắng!")
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

        CurrentPlayer = movingPlayer == "X" ? "O" : "X";

        TriggerAiTurnIfNeeded(cell, movingPlayer);
    }

    public void TryStartAITurn()
        => TriggerAiTurnIfNeeded(null, null);

    private void TriggerAiTurnIfNeeded(Cell? lastMoveCell, string? lastMovePlayer)
    {
        if (!IsAIEnabled || IsPaused || CurrentPlayer != _aiSymbol)
        {
            return;
        }

        if (AIMode == "Chuyên nghiệp" && _engine != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainVm = Application.Current.MainWindow?.DataContext as MainViewModel;
                mainVm?.SetStatus("AI đang suy nghĩ...");
            });

            Task.Run(() =>
            {
                try
                {
                    string aiMove = lastMovePlayer == _humanSymbol && lastMoveCell != null
                        ? _engine!.Turn(lastMoveCell.Col, lastMoveCell.Row)
                        : _engine!.Begin();

                    PlaceAiIfValid(aiMove);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainVm = Application.Current.MainWindow?.DataContext as MainViewModel;
                        mainVm?.SetStatus("Đang chơi");
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
            Task.Run(AIMove);
        }
    }

    public void PauseBoard() => IsPaused = true;

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

        if (AIMode == "Chuyên nghiệp")
        {
            TryInitializeProfessionalEngine();
        }

        TriggerAiTurnIfNeeded(null, null);
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
            var lastPlayerMove = Cells.LastOrDefault(c => c.Value == _humanSymbol);
            if (lastPlayerMove != null)
            {
                var neighbors = Cells.Where(c =>
                        string.IsNullOrEmpty(c.Value) &&
                        Math.Abs(c.Row - lastPlayerMove.Row) <= 1 &&
                        Math.Abs(c.Col - lastPlayerMove.Col) <= 1)
                    .ToList();

                if (neighbors.Any())
                {
                    bestCell = neighbors[new Random().Next(neighbors.Count)];
                }
            }

            if (bestCell == null)
            {
                var emptyCells = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();
                if (emptyCells.Any())
                {
                    bestCell = emptyCells[new Random().Next(emptyCells.Count)];
                }
            }
        }
        else
        {
            var candidates = Cells
                .Where(c => string.IsNullOrEmpty(c.Value) && HasNeighbor(c, 2))
                .ToList();

            if (!candidates.Any())
            {
                candidates = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();
            }

            int bestScore = int.MinValue;

            foreach (var candidate in candidates)
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
            Application.Current.Dispatcher.Invoke(() => ExecuteMove(bestCell, true));
        }
    }

    private void PlaceAiIfValid(string aiMove)
    {
        if (string.IsNullOrWhiteSpace(aiMove))
        {
            return;
        }

        var parts = aiMove.Split(',');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int aiX) &&
            int.TryParse(parts[1], out int aiY) &&
            _cellLookup.TryGetValue((aiY, aiX), out var aiCell))
        {
            Application.Current.Dispatcher.Invoke(() => ExecuteMove(aiCell, true));
        }
    }

    private bool HasNeighbor(Cell cell, int range)
        => GetNeighbors(cell.Row, cell.Col, range).Any(n => !string.IsNullOrEmpty(n.Value));

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

    private int EvaluateCellAdvanced(Cell cell)
    {
        int score = 0;
        score += EvaluatePotential(cell, _aiSymbol);
        score += EvaluatePotential(cell, _humanSymbol) * 2;
        score += ProximityScore(cell, _humanSymbol) * 5;
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
                foreach (var cellInLine in line)
                {
                    cellInLine.IsWinningCell = true;
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
}
