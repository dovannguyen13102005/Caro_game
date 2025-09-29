using System.Collections.Generic;
using System.Linq;
using Caro_game.Models;

namespace Caro_game.ViewModels
{
    public partial class BoardViewModel
    {
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
                var opposite = GetLine(row, col, -dir[0], -dir[1], player);
                line.AddRange(opposite);

                if (_cellLookup.TryGetValue((row, col), out var center))
                {
                    line.Add(center);
                }

                if (line.Count >= 5)
                {
                    foreach (var c in line) c.IsWinningCell = true;
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
                else break;
            }
            return list;
        }
    }
}
