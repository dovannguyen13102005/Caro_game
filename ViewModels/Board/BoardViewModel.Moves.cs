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
    private static readonly (int dRow, int dCol)[] DirectionVectors =
    {
        (0, 1),
        (1, 0),
        (1, 1),
        (1, -1)
    };

    private static readonly string[] OpenFourPatterns =
    {
        ".XXXX.",
        ".XXX.X.",
        ".X.XXX.",
        ".XX.XX."
    };

    private static readonly string[] OpenThreePatterns =
    {
        ".XXX.",
        ".XX.X.",
        ".X.XX."
    };

    private bool IsMoveLegal(int row, int col, string player, out string? violationMessage)
    {
        violationMessage = null;

        if (RuleType != GameRuleType.Renju || !string.Equals(player, "X", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var (dRow, dCol) in DirectionVectors)
        {
            int count = 1 + CountDirectionSimulate(row, col, dRow, dCol, player) +
                        CountDirectionSimulate(row, col, -dRow, -dCol, player);

            if (count > 5)
            {
                violationMessage = "Luật Renju: Không được tạo hơn 5 quân liên tiếp (overline).";
                return false;
            }
        }

        if (CheckExactFive(row, col, player))
        {
            return true;
        }

        int openFours = CountOpenFours(row, col, player);
        if (openFours >= 2)
        {
            violationMessage = "Luật Renju: Không được tạo đúp bốn (double-four).";
            return false;
        }

        int openThrees = CountOpenThrees(row, col, player);
        if (openThrees >= 2)
        {
            violationMessage = "Luật Renju: Không được tạo đúp ba (double-three).";
            return false;
        }

        return true;
    }

    private bool IsMoveLegalForRule(int row, int col, string player)
        => IsMoveLegal(row, col, player, out _);

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

        if (!IsMoveLegal(cell.Row, cell.Col, movingPlayer, out var violationMessage))
        {
            if (!isAiMove && !string.IsNullOrEmpty(violationMessage))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(violationMessage, "Luật Renju", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            return;
        }

        int originalRow = cell.Row;
        int originalCol = cell.Col;

        if (_lastMoveCell != null)
        {
            _lastMoveCell.IsLastMove = false;
        }

        _lastMoveCell = cell;
        _lastMovePlayer = movingPlayer;
        cell.IsLastMove = true;

        if (movingPlayer == _humanSymbol)
        {
            _lastHumanMoveCell = cell;
        }

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
            Task.Run(() => AIMoveAsync(lastMoveCell, lastMovePlayer));
        }
    }

    public void PauseBoard() => IsPaused = true;

    public void ResetBoard()
    {
        foreach (var cell in Cells)
        {
            cell.Value = string.Empty;
            cell.IsWinningCell = false;
            cell.IsLastMove = false;
        }

        lock (_candidateLock)
        {
            _candidatePositions.Clear();
        }

        _lastMoveCell = null;
        _lastHumanMoveCell = null;
        _lastMovePlayer = null;

        CurrentPlayer = _initialPlayer;
        IsPaused = false;

        if (AIMode == "Chuyên nghiệp")
        {
            TryInitializeProfessionalEngine();
        }

        TriggerAiTurnIfNeeded(null, null);
    }

    private async Task AIMoveAsync(Cell? lastMoveCell, string? lastMovePlayer)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            if (Application.Current.MainWindow?.DataContext is MainViewModel vm &&
                vm.IsGameActive && !vm.IsGamePaused)
            {
                vm.SetStatus("AI đang suy nghĩ...");
            }
        });

        try
        {
            await Task.Delay(AiThinkingDelay);

            if (!IsAIEnabled || IsPaused || CurrentPlayer != _aiSymbol)
            {
                return;
            }

            Cell? bestCell = null;

            if (AIMode == "Dễ")
            {
                Cell? reference = lastMovePlayer == _humanSymbol ? lastMoveCell : _lastHumanMoveCell;

                if (reference != null)
                {
                    var neighbors = Cells.Where(c =>
                            string.IsNullOrEmpty(c.Value) &&
                            Math.Abs(c.Row - reference.Row) <= 1 &&
                            Math.Abs(c.Col - reference.Col) <= 1)
                        .Where(c => IsMoveLegalForRule(c.Row, c.Col, _aiSymbol))
                        .ToList();

                    if (neighbors.Count > 0)
                    {
                        bestCell = neighbors[Random.Shared.Next(neighbors.Count)];
                    }
                }

                if (bestCell == null)
                {
                    var emptyCells = Cells
                        .Where(c => string.IsNullOrEmpty(c.Value) && IsMoveLegalForRule(c.Row, c.Col, _aiSymbol))
                        .ToList();
                    if (emptyCells.Count > 0)
                    {
                        bestCell = emptyCells[Random.Shared.Next(emptyCells.Count)];
                    }
                }
            }
            else
            {
                var candidates = Cells
                    .Where(c => string.IsNullOrEmpty(c.Value) && HasNeighbor(c, 2) && IsMoveLegalForRule(c.Row, c.Col, _aiSymbol))
                    .ToList();

                if (!candidates.Any())
                {
                    candidates = Cells
                        .Where(c => string.IsNullOrEmpty(c.Value) && IsMoveLegalForRule(c.Row, c.Col, _aiSymbol))
                        .ToList();
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
                dispatcher.Invoke(() =>
                {
                    if (!IsAIEnabled || IsPaused || CurrentPlayer != _aiSymbol)
                    {
                        return;
                    }

                    ExecuteMove(bestCell, true);
                });
            }
        }
        finally
        {
            dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow?.DataContext is MainViewModel vm &&
                    vm.IsGameActive && !vm.IsGamePaused)
                {
                    vm.SetStatus("Đang chơi");
                }
            });
        }
    }

    private void PlaceAiIfValid(string aiMove)
    {
        if (string.IsNullOrWhiteSpace(aiMove))
        {
            return;
        }

        if (ResponseIndicatesError(aiMove))
        {
            NotifyProfessionalModeUnavailable($"AI trả về lỗi: {aiMove}");
            return;
        }

        var parts = aiMove.Split(',');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int aiX) &&
            int.TryParse(parts[1], out int aiY) &&
            _cellLookup.TryGetValue((aiY, aiX), out var aiCell))
        {
            if (IsMoveLegalForRule(aiCell.Row, aiCell.Col, _aiSymbol))
            {
                Application.Current.Dispatcher.Invoke(() => ExecuteMove(aiCell, true));
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("AI đã tạo nước đi vi phạm luật đã chọn.",
                        "Caro", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
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
        => RuleType switch
        {
            GameRuleType.Freestyle => CheckFreestyleWin(row, col, player),
            GameRuleType.Standard => CheckExactFive(row, col, player),
            GameRuleType.Renju => CheckExactFive(row, col, player),
            _ => CheckFreestyleWin(row, col, player)
        };

    private void HighlightWinningCells(int row, int col, string player)
    {
        foreach (var (dRow, dCol) in DirectionVectors)
        {
            var forward = GetLine(row, col, dRow, dCol, player);
            var backward = GetLine(row, col, -dRow, -dCol, player);
            var combined = new List<Cell>(forward.Count + backward.Count + 1);
            combined.AddRange(forward);
            combined.AddRange(backward);

            if (_cellLookup.TryGetValue((row, col), out var center))
            {
                combined.Add(center);
            }

            bool isWinningLine = RuleType == GameRuleType.Freestyle
                ? combined.Count >= 5
                : combined.Count == 5;

            if (isWinningLine)
            {
                foreach (var cellInLine in combined.Distinct())
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

    private bool CheckFreestyleWin(int row, int col, string player)
    {
        foreach (var (dRow, dCol) in DirectionVectors)
        {
            int count = 1 + CountDirectionSimulate(row, col, dRow, dCol, player) +
                        CountDirectionSimulate(row, col, -dRow, -dCol, player);

            if (count >= 5)
            {
                return true;
            }
        }

        return false;
    }

    private bool CheckExactFive(int row, int col, string player)
    {
        foreach (var (dRow, dCol) in DirectionVectors)
        {
            int forward = CountDirectionSimulate(row, col, dRow, dCol, player);
            int backward = CountDirectionSimulate(row, col, -dRow, -dCol, player);
            int total = forward + backward + 1;

            if (total == 5)
            {
                bool forwardBounded = IsRunTerminated(row, col, dRow, dCol, forward, player);
                bool backwardBounded = IsRunTerminated(row, col, -dRow, -dCol, backward, player);

                if (forwardBounded && backwardBounded)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsRunTerminated(int row, int col, int dRow, int dCol, int count, string player)
    {
        int targetRow = row + (count + 1) * dRow;
        int targetCol = col + (count + 1) * dCol;

        if (!_cellLookup.TryGetValue((targetRow, targetCol), out var nextCell))
        {
            return true;
        }

        return !string.Equals(nextCell.Value, player, StringComparison.OrdinalIgnoreCase);
    }

    private int CountOpenFours(int row, int col, string player)
    {
        char playerChar = char.ToUpperInvariant(player[0]);
        int total = 0;

        foreach (var (dRow, dCol) in DirectionVectors)
        {
            string line = BuildLinePattern(row, col, dRow, dCol, playerChar);
            total += CountPatternMatches(line, playerChar, line.Length / 2, 4, OpenFourPatterns);
        }

        return total;
    }

    private int CountOpenThrees(int row, int col, string player)
    {
        char playerChar = char.ToUpperInvariant(player[0]);
        int total = 0;

        foreach (var (dRow, dCol) in DirectionVectors)
        {
            string line = BuildLinePattern(row, col, dRow, dCol, playerChar);
            total += CountPatternMatches(line, playerChar, line.Length / 2, 3, OpenThreePatterns);
        }

        return total;
    }

    private string BuildLinePattern(int row, int col, int dRow, int dCol, char playerChar, int range = 5)
    {
        var chars = new char[range * 2 + 1];

        for (int i = -range; i <= range; i++)
        {
            int r = row + i * dRow;
            int c = col + i * dCol;
            char value;

            if (i == 0)
            {
                value = playerChar;
            }
            else if (!_cellLookup.TryGetValue((r, c), out var cell))
            {
                value = '#';
            }
            else if (string.IsNullOrEmpty(cell.Value))
            {
                value = '.';
            }
            else
            {
                value = char.ToUpperInvariant(cell.Value[0]);
            }

            chars[i + range] = value;
        }

        return new string(chars);
    }

    private int CountPatternMatches(string line, char playerChar, int centerIndex, int expectedStones, string[] basePatterns)
    {
        var matches = new HashSet<(int Start, int End)>();

        foreach (var basePattern in basePatterns)
        {
            string pattern = basePattern.Replace('X', playerChar);
            int searchIndex = 0;

            while (searchIndex <= line.Length - pattern.Length)
            {
                int foundIndex = line.IndexOf(pattern, searchIndex, StringComparison.Ordinal);
                if (foundIndex == -1)
                {
                    break;
                }

                int endIndex = foundIndex + pattern.Length - 1;
                if (foundIndex <= centerIndex && endIndex >= centerIndex)
                {
                    string segment = line.Substring(foundIndex, pattern.Length);
                    int stoneCount = segment.Count(ch => ch == playerChar);

                    if (stoneCount == expectedStones)
                    {
                        matches.Add((foundIndex, endIndex));
                    }
                }

                searchIndex = foundIndex + 1;
            }
        }

        return matches.Count;
    }
}
