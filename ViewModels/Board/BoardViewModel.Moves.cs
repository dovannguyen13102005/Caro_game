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
    private static readonly int[][] Directions =
    {
        new[] { 0, 1 },
        new[] { 1, 0 },
        new[] { 1, 1 },
        new[] { 1, -1 }
    };

    private static readonly string[] RenjuOpenThreePatterns =
    {
        ".XXX.",
        "..XXX.",
        ".XXX..",
        ".XX.X.",
        ".X.XX."
    };

    private static readonly string[] RenjuOpenFourPatterns =
    {
        ".XXXX.",
        "..XXXX.",
        ".XXXX..",
        ".XXX.X.",
        ".X.XXX.",
        ".XX.XX.",
        "..XXX.X.",
        ".XXX.X..",
        "..X.XXX.",
        ".X.XXX..",
        "..XX.XX.",
        ".XX.XX.."
    };

    public void MakeMove(Cell cell, bool isAiMove = false)
    {
        if (IsPaused || !string.IsNullOrEmpty(cell.Value))
        {
            return;
        }

        if (IsAIEnabled)
        {
            if (isAiMove)
            {
                if (CurrentPlayer != AiPiece)
                {
                    return;
                }
            }
            else if (CurrentPlayer != HumanPiece)
            {
                return;
            }
        }

        var movingPlayer = CurrentPlayer;
        int originalRow = cell.Row;
        int originalCol = cell.Col;

        cell.Value = movingPlayer;

        bool isWinningMove = CheckWin(originalRow, originalCol, movingPlayer);

        bool shouldValidateRenju = GameRule == GameRule.Renju &&
                                   movingPlayer == "X" &&
                                   !(IsAIEnabled && AIMode == "Chuyên nghiệp");

        if (shouldValidateRenju)
        {
            string? violation = ValidateRenjuMove(originalRow, originalCol, isWinningMove);
            if (violation != null)
            {
                cell.Value = string.Empty;
                Application.Current.Dispatcher?.Invoke(() =>
                {
                    MessageBox.Show(violation, "Renju", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
                return;
            }
        }

        if (!(IsAIEnabled && AIMode == "Chuyên nghiệp"))
        {
            ExpandBoardIfNeeded(originalRow, originalCol);
        }

        UpdateCandidatePositions(cell.Row, cell.Col);

        if (isWinningMove)
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

        if (IsAIEnabled && CurrentPlayer == AiPiece)
        {
            MaybeScheduleAiMove(isAiMove ? null : cell);
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

        InitializeTurnOrder();
        IsPaused = false;

        if (AIMode == "Chuyên nghiệp")
        {
            TryInitializeProfessionalEngine();
        }
        else if (IsAIEnabled)
        {
            MaybeScheduleAiMove(null);
        }
    }

    private void AIMove()
    {
        if (!IsAIEnabled || IsPaused)
        {
            return;
        }

        string aiPiece = AiPiece;
        string opponent = HumanPiece;

        Cell? bestCell = null;

        if (AIMode == "Dễ")
        {
            var lastPlayerMove = Cells.LastOrDefault(c => c.Value == opponent);
            if (lastPlayerMove != null)
            {
                var neighbors = Cells.Where(c =>
                        string.IsNullOrEmpty(c.Value) &&
                        Math.Abs(c.Row - lastPlayerMove.Row) <= 1 &&
                        Math.Abs(c.Col - lastPlayerMove.Col) <= 1)
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
                int score = EvaluateCellAdvanced(candidate, aiPiece, opponent);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = candidate;
                }
            }
        }

        if (bestCell != null)
        {
            Application.Current.Dispatcher.Invoke(() => MakeMove(bestCell, true));
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
            Application.Current.Dispatcher.Invoke(() => MakeMove(aiCell, true));
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

    private int EvaluateCellAdvanced(Cell cell, string aiPiece, string opponent)
    {
        int score = 0;
        score += EvaluatePotential(cell, aiPiece);
        score += EvaluatePotential(cell, opponent) * 2;
        score += ProximityScore(cell, opponent) * 5;
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

        foreach (var dir in Directions)
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
        foreach (var dir in Directions)
        {
            int count = 1;
            count += CountDirectionSimulate(row, col, dir[0], dir[1], player);
            count += CountDirectionSimulate(row, col, -dir[0], -dir[1], player);

            switch (GameRule)
            {
                case GameRule.Freestyle:
                    if (count >= 5)
                    {
                        return true;
                    }
                    break;
                case GameRule.Standard:
                    if (count == 5)
                    {
                        return true;
                    }
                    break;
                case GameRule.Renju:
                    if (player == "X")
                    {
                        if (count == 5)
                        {
                            return true;
                        }
                    }
                    else if (count >= 5)
                    {
                        return true;
                    }
                    break;
            }
        }

        return false;
    }

    private void HighlightWinningCells(int row, int col, string player)
    {
        foreach (var dir in Directions)
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

    private string? ValidateRenjuMove(int row, int col, bool isWinningMove)
    {
        if (IsRenjuOverline(row, col))
        {
            return "Nước đi này vi phạm luật Renju: X không được tạo hơn 5 quân liên tiếp (overline).";
        }

        if (isWinningMove)
        {
            return null;
        }

        int openFourCount = CountRenjuOpenFours(row, col);
        if (openFourCount >= 2)
        {
            return "Nước đi này vi phạm luật Renju: cấm tạo đồng thời hai thế tứ (double-four).";
        }

        int openThreeCount = CountRenjuOpenThrees(row, col);
        if (openThreeCount >= 2)
        {
            return "Nước đi này vi phạm luật Renju: cấm tạo đồng thời hai thế tam mở (double-three).";
        }

        return null;
    }

    private bool IsRenjuOverline(int row, int col)
    {
        foreach (var dir in Directions)
        {
            int count = 1;
            count += CountDirectionSimulate(row, col, dir[0], dir[1], "X");
            count += CountDirectionSimulate(row, col, -dir[0], -dir[1], "X");

            if (count > 5)
            {
                return true;
            }
        }

        return false;
    }

    private int CountRenjuOpenFours(int row, int col)
    {
        int total = 0;
        foreach (var dir in Directions)
        {
            var data = BuildRenjuLine(row, col, dir[0], dir[1]);
            total += CountPatternMatches(data, RenjuOpenFourPatterns);
        }
        return total;
    }

    private int CountRenjuOpenThrees(int row, int col)
    {
        int total = 0;
        foreach (var dir in Directions)
        {
            var data = BuildRenjuLine(row, col, dir[0], dir[1]);
            total += CountPatternMatches(data, RenjuOpenThreePatterns);
        }
        return total;
    }

    private (string line, int centerIndex) BuildRenjuLine(int row, int col, int dRow, int dCol)
    {
        const int range = 6;
        var chars = new char[range * 2 + 1];

        for (int offset = -range; offset <= range; offset++)
        {
            int r = row + offset * dRow;
            int c = col + offset * dCol;
            char value;

            if (_cellLookup.TryGetValue((r, c), out var cell))
            {
                value = string.IsNullOrEmpty(cell.Value) ? '.' : cell.Value[0];
            }
            else
            {
                value = '#';
            }

            chars[offset + range] = value;
        }

        return (new string(chars), range);
    }

    private int CountPatternMatches((string line, int centerIndex) data, string[] patterns)
    {
        var (line, centerIndex) = data;
        var matches = new HashSet<int>();

        foreach (var pattern in patterns)
        {
            int searchIndex = 0;
            while (searchIndex <= line.Length - pattern.Length)
            {
                int index = line.IndexOf(pattern, searchIndex, StringComparison.Ordinal);
                if (index == -1)
                {
                    break;
                }

                if (index <= centerIndex && centerIndex < index + pattern.Length)
                {
                    bool valid = true;
                    for (int i = 0; i < pattern.Length; i++)
                    {
                        char expected = pattern[i];
                        char actual = line[index + i];

                        if (expected == '.')
                        {
                            if (actual != '.')
                            {
                                valid = false;
                                break;
                            }
                        }
                        else if (expected == 'X' || expected == 'O')
                        {
                            if (actual != expected)
                            {
                                valid = false;
                                break;
                            }
                        }
                        else if (expected == '#')
                        {
                            if (actual != '#')
                            {
                                valid = false;
                                break;
                            }
                        }
                    }

                    if (valid)
                    {
                        matches.Add(index);
                    }
                }

                searchIndex = index + 1;
            }
        }

        return matches.Count;
    }

    private void MaybeScheduleAiMove(Cell? lastHumanMove)
    {
        if (!IsAIEnabled || IsPaused || CurrentPlayer != AiPiece)
        {
            return;
        }

        if (AIMode == "Chuyên nghiệp" && _engine != null)
        {
            Application.Current.Dispatcher?.Invoke(() =>
            {
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    mainVm.SetStatus("AI đang suy nghĩ...");
                }
            });

            Task.Run(() =>
            {
                try
                {
                    string aiMove;
                    if (lastHumanMove != null)
                    {
                        aiMove = _engine!.Turn(lastHumanMove.Col, lastHumanMove.Row);
                    }
                    else
                    {
                        if (Cells.Any(c => !string.IsNullOrEmpty(c.Value)))
                        {
                            return;
                        }

                        aiMove = _engine!.Begin();
                    }

                    PlaceAiIfValid(aiMove);

                    Application.Current.Dispatcher?.Invoke(() =>
                    {
                        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
                        {
                            vm.SetStatus("Đang chơi");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher?.Invoke(() =>
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
}
