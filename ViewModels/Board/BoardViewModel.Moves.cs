using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Caro_game.Models;
using Caro_game.Rules;
using Caro_game.Services;
using Caro_game.Views;

namespace Caro_game.ViewModels;

public partial class BoardViewModel
{
    public void MakeHumanMove(Cell cell)
        => ExecuteMove(cell, isAiMove: false);

    private void ExecuteMove(Cell cell, bool isAiMove)
    {
        if (!isAiMove && (IsPaused || !string.IsNullOrEmpty(cell.Value)))
        {
            //AudioService.Instance.PlayErrorSound();
            return;
        }

        if (!isAiMove && IsAIEnabled && CurrentPlayer != _humanSymbol)
        {
            //AudioService.Instance.PlayErrorSound();
            return;
        }

        var movingPlayer = CurrentPlayer;
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

        _moveHistory.Add(new MoveState
        {
            Row = cell.Row,
            Col = cell.Col,
            Player = movingPlayer
        });

        AudioService.Instance.PlayMoveSound();

        if (_allowBoardExpansion && !(IsAIEnabled && AIMode == "Chuyên nghiệp"))
        {
            ExpandBoardIfNeeded(originalRow, originalCol);
        }

        UpdateCandidatePositions(cell.Row, cell.Col);

        var boardState = BuildBoardState();
        int playerValue = GetPlayerValue(movingPlayer);

        if (_rule.IsForbiddenMove(boardState, playerValue))
        {
            HandleForbiddenMove(movingPlayer);
            return;
        }

        if (_rule.IsWinning(boardState, playerValue))
        {
            HighlightWinningCells(boardState, cell.Row, cell.Col, playerValue);

            Application.Current.Dispatcher.Invoke(() =>
            {
                bool aiMatch = IsAIEnabled;
                bool aiWon = aiMatch && string.Equals(movingPlayer, _aiSymbol, StringComparison.OrdinalIgnoreCase);

                string message = aiMatch
                    ? (aiWon ? "Máy thắng!" : "Bạn thắng!")
                    : $"Người chơi {movingPlayer} thắng!";

                if (aiWon)
                {
                    AudioService.Instance.PlayLoseSound();
                }
                else
                {
                    AudioService.Instance.PlayWinSound();
                }

                var dialog = new WinDialog(message)
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
                AudioService.Instance.PlayLoseSound();
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
        _moveHistory.Clear();

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
                        .ToList();

                    if (neighbors.Count > 0)
                    {
                        bestCell = neighbors[Random.Shared.Next(neighbors.Count)];
                    }
                }

                if (bestCell == null)
                {
                    var emptyCells = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();
                    if (emptyCells.Count > 0)
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

        var parts = aiMove.Split(',');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int aiX) &&
            int.TryParse(parts[1], out int aiY) &&
            _cellLookup.TryGetValue((aiY, aiX), out var aiCell) &&
            string.IsNullOrEmpty(aiCell.Value))
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

    private void HighlightWinningCells(int[,] boardState, int row, int col, int playerValue)
    {
        var winningLine = GetWinningLine(boardState, row, col, playerValue);
        if (winningLine == null)
        {
            return;
        }

        foreach (var (lineRow, lineCol) in winningLine)
        {
            if (_cellLookup.TryGetValue((lineRow, lineCol), out var cellInLine))
            {
                cellInLine.IsWinningCell = true;
            }
        }
    }

    private void HandleForbiddenMove(string offendingPlayer)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            string winner = offendingPlayer == "X" ? "O" : "X";
            AudioService.Instance.PlayErrorSound();

            bool aiMatch = IsAIEnabled;
            bool humanCommittedFoul = aiMatch && string.Equals(offendingPlayer, _humanSymbol, StringComparison.OrdinalIgnoreCase);
            bool aiCommittedFoul = aiMatch && string.Equals(offendingPlayer, _aiSymbol, StringComparison.OrdinalIgnoreCase);

            string message = aiMatch
                ? (humanCommittedFoul
                    ? "Bạn thua vì đi sai luật!"
                    : aiCommittedFoul
                        ? "Máy đi sai luật, bạn thắng!"
                        : $"Nước đi của {offendingPlayer} bị cấm theo luật {RuleName}. {winner} thắng!")
                : $"Nước đi của {offendingPlayer} bị cấm theo luật {RuleName}. {winner} thắng!";

            if (humanCommittedFoul)
            {
                AudioService.Instance.PlayLoseSound();
            }
            else
            {
                AudioService.Instance.PlayWinSound();
            }

            var dialog = new WinDialog(message)
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
                Application.Current.Shutdown();
            }
        });
    }

    private int[,] BuildBoardState()
    {
        var boardState = new int[Rows, Columns];

        foreach (var cell in _cellLookup.Values)
        {
            if (cell.Value == "X")
            {
                boardState[cell.Row, cell.Col] = 1;
            }
            else if (cell.Value == "O")
            {
                boardState[cell.Row, cell.Col] = 2;
            }
        }

        return boardState;
    }

    private static int GetPlayerValue(string player)
        => player.Equals("X", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

    private static string NormalizePlayerSymbol(string? player)
        => string.Equals(player, "O", StringComparison.OrdinalIgnoreCase) ? "O" : "X";

    private List<(int Row, int Col)>? GetWinningLine(int[,] boardState, int row, int col, int playerValue)
    {
        var directions = new (int dx, int dy)[]
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        foreach (var (dx, dy) in directions)
        {
            var line = CollectLine(boardState, row, col, dx, dy, playerValue);
            if (line.Count < 5)
            {
                continue;
            }

            if (_rule is StandardRule)
            {
                if (line.Count == 5 && EndsAreClosedForStandard(boardState, line, dx, dy, playerValue))
                {
                    return line;
                }
            }
            else
            {
                return line;
            }
        }

        return null;
    }

    private List<(int Row, int Col)> CollectLine(int[,] boardState, int row, int col, int dx, int dy, int playerValue)
    {
        var line = new List<(int, int)>();

        int startRow = row;
        int startCol = col;

        while (IsWithinBoard(startRow - dx, startCol - dy) && boardState[startRow - dx, startCol - dy] == playerValue)
        {
            startRow -= dx;
            startCol -= dy;
        }

        int currentRow = startRow;
        int currentCol = startCol;

        while (IsWithinBoard(currentRow, currentCol) && boardState[currentRow, currentCol] == playerValue)
        {
            line.Add((currentRow, currentCol));
            currentRow += dx;
            currentCol += dy;
        }

        return line;
    }

    private bool EndsAreClosedForStandard(int[,] boardState, List<(int Row, int Col)> line, int dx, int dy, int playerValue)
    {
        var first = line[0];
        var last = line[^1];

        int beforeRow = first.Row - dx;
        int beforeCol = first.Col - dy;
        int afterRow = last.Row + dx;
        int afterCol = last.Col + dy;

        bool end1 = !IsWithinBoard(beforeRow, beforeCol) || boardState[beforeRow, beforeCol] != playerValue;
        bool end2 = !IsWithinBoard(afterRow, afterCol) || boardState[afterRow, afterCol] != playerValue;

        return end1 && end2;
    }

    private bool IsWithinBoard(int row, int col)
        => row >= 0 && row < Rows && col >= 0 && col < Columns;
}
