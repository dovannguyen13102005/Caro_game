using System.Collections.Generic;

namespace Caro_game.Rules
{
    public class FreeStyleRule : IRule
    {
        private static readonly (int dRow, int dCol)[] Directions =
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        public string Name => "Free Style";

        public int BoardSize => 19;

        public bool AllowBoardExpansion => false;

        public string? EngineRuleKeyword => "freestyle";

        public string? ForbiddenMoveMessage => null;

        public string? GetConfigFileName(bool aiIsBlack) => "config_freestyle.toml";

        public bool IsForbiddenMove(int[,] board, int player, int lastRow, int lastCol) => false;

        public bool TryGetWinningLine(int[,] board, int player, int lastRow, int lastCol, out List<(int Row, int Col)> winningCells)
        {
            foreach (var (dRow, dCol) in Directions)
            {
                var line = CollectLine(board, player, lastRow, lastCol, dRow, dCol);
                if (line.Count >= 5)
                {
                    winningCells = line;
                    return true;
                }
            }

            winningCells = new List<(int, int)>();
            return false;
        }

        public IRule Clone() => new FreeStyleRule();

        private static List<(int Row, int Col)> CollectLine(int[,] board, int player, int lastRow, int lastCol, int dRow, int dCol)
        {
            var line = new List<(int, int)> { (lastRow, lastCol) };

            int row = lastRow + dRow;
            int col = lastCol + dCol;
            while (IsInside(board, row, col) && board[row, col] == player)
            {
                line.Add((row, col));
                row += dRow;
                col += dCol;
            }

            row = lastRow - dRow;
            col = lastCol - dCol;
            while (IsInside(board, row, col) && board[row, col] == player)
            {
                line.Add((row, col));
                row -= dRow;
                col -= dCol;
            }

            return line;
        }

        private static bool IsInside(int[,] board, int row, int col)
            => row >= 0 && row < board.GetLength(0) && col >= 0 && col < board.GetLength(1);
    }
}
