using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Caro_game.Models;

namespace Caro_game.ViewModels
{
    public partial class BoardViewModel
    {
        private const string AiSymbol = "O";
        private const string HumanSymbol = "X";
        private const int WinningScore = 1_000_000;
        private const double EasyDefenseMultiplier = 1.5;
        private const double HardDefenseMultiplier = 3.5;
        private const int EasyTopCandidates = 4;
        private const int MaxSearchCandidates = 8;
        private const int SearchDepth = 3;

        private void AIMove()
        {
            if (!IsAIEnabled || IsPaused)
                return;

            Cell? bestCell = AIMode switch
            {
                "Dễ" => ChooseEasyMove(),
                "Khó" => ChooseHardMove(),
                _ => ChooseHardMove()
            };

            if (bestCell != null)
            {
                Application.Current.Dispatcher.Invoke(() => MakeMove(bestCell));
            }
        }

        private Cell? ChooseEasyMove()
        {
            var candidates = Cells
                .Where(c => string.IsNullOrEmpty(c.Value) && HasNeighbor(c, 2))
                .ToList();

            if (!candidates.Any())
            {
                candidates = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();
            }

            if (!candidates.Any())
            {
                return null;
            }

            var scored = candidates
                .Select(cell => new { Cell = cell, Score = EvaluateCellAdvanced(cell) })
                .OrderByDescending(x => x.Score)
                .ToList();

            int takeCount = Math.Min(EasyTopCandidates, scored.Count);
            var selectionPool = scored.Take(takeCount).ToList();

            return selectionPool[_random.Next(selectionPool.Count)].Cell;
        }

        private Cell? ChooseHardMove()
        {
            var candidates = GetOrderedCandidates(forAi: true);

            if (!candidates.Any())
            {
                return null;
            }

            Cell? bestCell = null;
            int bestScore = int.MinValue;

            foreach (var cell in candidates)
            {
                cell.Value = AiSymbol;

                int score = CheckWin(cell.Row, cell.Col, AiSymbol)
                    ? WinningScore
                    : Minimax(SearchDepth - 1, maximizingPlayer: false, lastMove: cell, alpha: int.MinValue + 1, beta: int.MaxValue - 1);

                cell.Value = string.Empty;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }

            return bestCell;
        }

        private List<Cell> GetOrderedCandidates(bool forAi)
        {
            var candidates = Cells
                .Where(c => string.IsNullOrEmpty(c.Value) && HasNeighbor(c, 2))
                .ToList();

            if (!candidates.Any())
            {
                candidates = Cells.Where(c => string.IsNullOrEmpty(c.Value)).ToList();
            }

            return (forAi
                    ? candidates.OrderByDescending(EvaluateCellStrategic)
                    : candidates.OrderByDescending(c => EvaluateCellForPlayer(c, HumanSymbol, HardDefenseMultiplier)))
                .Take(MaxSearchCandidates)
                .ToList();
        }

        private int Minimax(int depth, bool maximizingPlayer, Cell? lastMove, int alpha, int beta)
        {
            if (lastMove != null && !string.IsNullOrEmpty(lastMove.Value))
            {
                if (CheckWin(lastMove.Row, lastMove.Col, lastMove.Value))
                {
                    return lastMove.Value == AiSymbol
                        ? WinningScore + depth
                        : -WinningScore - depth;
                }
            }

            if (depth == 0)
            {
                return EvaluateBoard();
            }

            var candidates = GetOrderedCandidates(forAi: maximizingPlayer);

            if (!candidates.Any())
            {
                return EvaluateBoard();
            }

            if (maximizingPlayer)
            {
                int value = int.MinValue;

                foreach (var move in candidates)
                {
                    move.Value = AiSymbol;
                    int evaluation = Minimax(depth - 1, false, move, alpha, beta);
                    move.Value = string.Empty;

                    if (evaluation > value)
                    {
                        value = evaluation;
                    }

                    if (value > alpha) alpha = value;
                    if (alpha >= beta) break;
                }

                return value;
            }
            else
            {
                int value = int.MaxValue;

                foreach (var move in candidates)
                {
                    move.Value = HumanSymbol;
                    int evaluation = Minimax(depth - 1, true, move, alpha, beta);
                    move.Value = string.Empty;

                    if (evaluation < value)
                    {
                        value = evaluation;
                    }

                    if (value < beta) beta = value;
                    if (alpha >= beta) break;
                }

                return value;
            }
        }

        private int EvaluateBoard()
        {
            int aiScore = 0;
            int humanScore = 0;

            foreach (var cell in Cells)
            {
                if (!string.IsNullOrEmpty(cell.Value))
                {
                    continue;
                }

                aiScore += EvaluatePatternScore(cell, AiSymbol);
                humanScore += EvaluatePatternScore(cell, HumanSymbol);
            }

            return aiScore - (int)Math.Round(humanScore * HardDefenseMultiplier);
        }

        private int EvaluateCellAdvanced(Cell cell)
            => EvaluateCellForPlayer(cell, AiSymbol, EasyDefenseMultiplier);

        private int EvaluateCellStrategic(Cell cell)
            => EvaluateCellForPlayer(cell, AiSymbol, HardDefenseMultiplier);

        private int EvaluateCellForPlayer(Cell cell, string player, double defenseMultiplier)
        {
            int offensiveScore = EvaluatePatternScore(cell, player);
            string opponent = player == AiSymbol ? HumanSymbol : AiSymbol;
            int defensiveScore = EvaluatePatternScore(cell, opponent);

            return offensiveScore + (int)Math.Round(defensiveScore * defenseMultiplier);
        }

        private int EvaluatePatternScore(Cell cell, string player)
        {
            int[][] directions =
            {
                new[] { 0, 1 },
                new[] { 1, 0 },
                new[] { 1, 1 },
                new[] { 1, -1 }
            };

            int totalScore = 0;

            foreach (var dir in directions)
            {
                var (count, openEnds) = AnalyzeDirection(cell, player, dir[0], dir[1]);
                totalScore += ScorePattern(count, openEnds);
            }

            return totalScore;
        }

        private (int Count, bool OpenEnd) CountDirectionDetailed(int row, int col, int dRow, int dCol, string player)
        {
            int count = 0;
            int r = row + dRow;
            int c = col + dCol;

            while (r >= 0 && r < Rows && c >= 0 && c < Columns)
            {
                if (_cellLookup.TryGetValue((r, c), out var neighbor))
                {
                    if (neighbor.Value == player)
                    {
                        count++;
                        r += dRow;
                        c += dCol;
                        continue;
                    }

                    if (string.IsNullOrEmpty(neighbor.Value))
                    {
                        return (count, true);
                    }

                    return (count, false);
                }

                break;
            }

            return (count, false);
        }

        private (int Count, int OpenEnds) AnalyzeDirection(Cell cell, string player, int dRow, int dCol)
        {
            var forward = CountDirectionDetailed(cell.Row, cell.Col, dRow, dCol, player);
            var backward = CountDirectionDetailed(cell.Row, cell.Col, -dRow, -dCol, player);

            int totalCount = 1 + forward.Count + backward.Count;
            int openEnds = 0;

            if (forward.OpenEnd) openEnds++;
            if (backward.OpenEnd) openEnds++;

            return (totalCount, openEnds);
        }

        private int ScorePattern(int count, int openEnds)
        {
            if (count >= 5)
            {
                return WinningScore;
            }

            return (count, openEnds) switch
            {
                (4, 2) => 200_000,
                (4, 1) => 50_000,
                (3, 2) => 1_000,
                (3, 1) => 200,
                (2, 2) => 100,
                (2, 1) => 30,
                (1, 2) => 5,
                (1, 1) => 2,
                _ => 0
            };
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

        private void TryInitializeMasterEngine()
        {
            DisposeEngine();

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var enginePath = Path.Combine(baseDirectory, "AI", "pbrain-rapfi-windows-avx2.exe");

            Console.WriteLine("[Engine Path] " + enginePath);

            if (string.IsNullOrWhiteSpace(enginePath) || !File.Exists(enginePath))
            {
                NotifyMasterModeUnavailable("Không tìm thấy tệp AI cần thiết cho chế độ Chuyên nghiệp.\n" +
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
                            "Chuyên nghiệp", MessageBoxButton.OK, MessageBoxImage.Warning);

                        DisposeEngine();
                        IsAIEnabled = false;
                        AIMode = "Khó";
                        return;
                    }
                }

                if (Cells != null && Cells.All(c => string.IsNullOrEmpty(c.Value)) && CurrentPlayer == "O")
                {
                    var aiMove = _engine.Begin();
                    PlaceAiIfValid(aiMove);
                }
            }
            catch (Exception ex)
            {
                NotifyMasterModeUnavailable($"Không thể khởi động AI Chuyên nghiệp.\nChi tiết: {ex}");
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
