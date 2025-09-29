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
                Application.Current.Dispatcher.Invoke(() => MakeMove(bestCell));
            }
        }

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
