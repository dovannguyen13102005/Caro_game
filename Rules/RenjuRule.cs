using System.Collections.Generic;

namespace Caro_game.Rules
{
    public class RenjuRule : IRule
    {
        private const int WinningCount = 5;

        private static readonly (int dRow, int dCol)[] Directions =
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        public string Name => "Renju";

        public int BoardSize => 15;

        public bool AllowBoardExpansion => false;

        public string? EngineRuleKeyword => "renju";

        public string? ForbiddenMoveMessage => "Nước đi không hợp lệ theo luật Renju.";

        public string? GetConfigFileName(bool aiIsBlack)
            => aiIsBlack ? "config_renju_black.toml" : "config_renju_white.toml";

        public bool IsForbiddenMove(int[,] board, int player, int lastRow, int lastCol)
        {
            if (player != 1)
            {
                return false;
            }

            if (HasOverline(board, player))
            {
                return true;
            }

            if (CountOpenFours(board, player) >= 2)
            {
                return true;
            }

            if (CountOpenThrees(board, player) >= 2)
            {
                return true;
            }

            return false;
        }

        public bool TryGetWinningLine(int[,] board, int player, int lastRow, int lastCol, out List<(int Row, int Col)> winningCells)
        {
            foreach (var (dRow, dCol) in Directions)
            {
                var line = CollectExactFive(board, player, lastRow, lastCol, dRow, dCol);
                if (line.Count == WinningCount)
                {
                    winningCells = line;
                    return true;
                }
            }

            winningCells = new List<(int, int)>();
            return false;
        }

        public IRule Clone() => new RenjuRule();

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

            return (line.Count == WinningCount && end1 && end2) ? line : new List<(int, int)>();
        }

        private static bool HasOverline(int[,] board, int player)
        {
            int size = board.GetLength(0);
            int cols = board.GetLength(1);

            for (int row = 0; row < size; row++)
            {
                int count = 0;
                for (int col = 0; col < cols; col++)
                {
                    count = board[row, col] == player ? count + 1 : 0;
                    if (count >= WinningCount + 1)
                    {
                        return true;
                    }
                }
            }

            for (int col = 0; col < cols; col++)
            {
                int count = 0;
                for (int row = 0; row < size; row++)
                {
                    count = board[row, col] == player ? count + 1 : 0;
                    if (count >= WinningCount + 1)
                    {
                        return true;
                    }
                }
            }

            for (int diag = -size + 1; diag < cols; diag++)
            {
                int count = 0;
                for (int row = 0; row < size; row++)
                {
                    int col = row + diag;
                    if (col >= 0 && col < cols)
                    {
                        count = board[row, col] == player ? count + 1 : 0;
                        if (count >= WinningCount + 1)
                        {
                            return true;
                        }
                    }
                }
            }

            for (int diag = 0; diag < size + cols - 1; diag++)
            {
                int count = 0;
                for (int row = 0; row < size; row++)
                {
                    int col = diag - row;
                    if (col >= 0 && col < cols)
                    {
                        count = board[row, col] == player ? count + 1 : 0;
                        if (count >= WinningCount + 1)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static int CountOpenFours(int[,] board, int player)
        {
            int openFours = 0;

            foreach (var (dRow, dCol) in Directions)
            {
                if (HasOpenSequence(board, player, 4, dRow, dCol))
                {
                    openFours++;
                }
            }

            return openFours;
        }

        private static int CountOpenThrees(int[,] board, int player)
        {
            int openThrees = 0;

            foreach (var (dRow, dCol) in Directions)
            {
                if (HasOpenSequence(board, player, 3, dRow, dCol))
                {
                    openThrees++;
                }
            }

            return openThrees;
        }

        private static bool HasOpenSequence(int[,] board, int player, int length, int dRow, int dCol)
        {
            int rows = board.GetLength(0);
            int cols = board.GetLength(1);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (!IsInside(board, row, col) || board[row, col] != player)
                    {
                        continue;
                    }

                    bool sequence = true;
                    for (int step = 1; step < length; step++)
                    {
                        int r = row + step * dRow;
                        int c = col + step * dCol;
                        if (!IsInside(board, r, c) || board[r, c] != player)
                        {
                            sequence = false;
                            break;
                        }
                    }

                    if (!sequence)
                    {
                        continue;
                    }

                    int beforeRow = row - dRow;
                    int beforeCol = col - dCol;
                    int afterRow = row + length * dRow;
                    int afterCol = col + length * dCol;

                    bool openStart = IsInside(board, beforeRow, beforeCol) && board[beforeRow, beforeCol] == 0;
                    bool openEnd = IsInside(board, afterRow, afterCol) && board[afterRow, afterCol] == 0;

                    if (openStart && openEnd)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsInside(int[,] board, int row, int col)
            => row >= 0 && row < board.GetLength(0) && col >= 0 && col < board.GetLength(1);
    }
}
