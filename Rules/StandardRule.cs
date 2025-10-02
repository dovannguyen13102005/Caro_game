using System.Collections.Generic;

namespace Caro_game.Rules
{
    public class StandardRule : IRule
    {
        private static readonly (int dRow, int dCol)[] Directions =
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        public string Name => "Standard";

        public int BoardSize => 15;

        public bool AllowBoardExpansion => false;

        public string? EngineRuleKeyword => "standard";

        public string? ForbiddenMoveMessage => null;

        public string? GetConfigFileName(bool aiIsBlack) => "config_standard.toml";

        public bool IsForbiddenMove(int[,] board, int player, int lastRow, int lastCol) => false;

        public bool TryGetWinningLine(int[,] board, int player, int lastRow, int lastCol, out List<(int Row, int Col)> winningCells)
        {
            foreach (var (dRow, dCol) in Directions)
            {
                var line = CollectExactFive(board, player, lastRow, lastCol, dRow, dCol);
                if (line.Count == 5)
                {
                    winningCells = line;
                    return true;
                }
            }

            winningCells = new List<(int, int)>();
            return false;
        }

        public IRule Clone() => new StandardRule();

        private static List<(int Row, int Col)> CollectExactFive(int[,] board, int player, int lastRow, int lastCol, int dRow, int dCol)
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
            bool end1 = !IsInside(board, row, col) || board[row, col] != player;

            row = lastRow - dRow;
            col = lastCol - dCol;
            while (IsInside(board, row, col) && board[row, col] == player)
            {
                line.Add((row, col));
                row -= dRow;
                col -= dCol;
            }
            bool end2 = !IsInside(board, row, col) || board[row, col] != player;

            return (line.Count == 5 && end1 && end2) ? line : new List<(int, int)>();
        }

        private static bool IsInside(int[,] board, int row, int col)
            => row >= 0 && row < board.GetLength(0) && col >= 0 && col < board.GetLength(1);
    }
}
