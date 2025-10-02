namespace Caro_game.Rules
{
    public class RenjuRule : IRule
    {
        private const int WinningCount = 5;

        public bool IsWinning(int[,] board, int player)
        {
            if (player == 1 && IsForbiddenMove(board, player))
            {
                return false;
            }

            return CheckRows(board, player)
                   || CheckColumns(board, player)
                   || CheckDiagonals(board, player)
                   || CheckAntiDiagonals(board, player);
        }

        public bool IsForbiddenMove(int[,] board, int player)
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

            return CountOpenThrees(board, player) >= 2;
        }

        public IRule Clone() => new RenjuRule();

        private static bool HasOverline(int[,] board, int player)
        {
            for (int row = 0; row < board.GetLength(0); row++)
            {
                int count = 0;
                for (int col = 0; col < board.GetLength(1); col++)
                {
                    count = board[row, col] == player ? count + 1 : 0;
                    if (count >= 6)
                    {
                        return true;
                    }
                }
            }

            for (int col = 0; col < board.GetLength(1); col++)
            {
                int count = 0;
                for (int row = 0; row < board.GetLength(0); row++)
                {
                    count = board[row, col] == player ? count + 1 : 0;
                    if (count >= 6)
                    {
                        return true;
                    }
                }
            }

            for (int d = -board.GetLength(0) + 1; d < board.GetLength(1); d++)
            {
                int count = 0;
                for (int row = 0; row < board.GetLength(0); row++)
                {
                    int col = row + d;
                    if (col >= 0 && col < board.GetLength(1))
                    {
                        count = board[row, col] == player ? count + 1 : 0;
                        if (count >= 6)
                        {
                            return true;
                        }
                    }
                }
            }

            for (int d = 0; d < board.GetLength(0) + board.GetLength(1) - 1; d++)
            {
                int count = 0;
                for (int row = 0; row < board.GetLength(0); row++)
                {
                    int col = d - row;
                    if (col >= 0 && col < board.GetLength(1))
                    {
                        count = board[row, col] == player ? count + 1 : 0;
                        if (count >= 6)
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
            var directions = new (int dx, int dy)[]
            {
                (0, 1),
                (1, 0),
                (1, 1),
                (-1, 1)
            };

            foreach (var (dx, dy) in directions)
            {
                bool hasOpenFour = false;

                for (int row = 0; row < board.GetLength(0); row++)
                {
                    for (int col = 0; col < board.GetLength(1); col++)
                    {
                        bool sequence = true;
                        for (int i = 0; i < 4; i++)
                        {
                            int newRow = row + i * dx;
                            int newCol = col + i * dy;

                            if (!IsWithinBounds(newRow, newCol, board) || board[newRow, newCol] != player)
                            {
                                sequence = false;
                                break;
                            }
                        }

                        if (!sequence)
                        {
                            continue;
                        }

                        int beforeRow = row - dx;
                        int beforeCol = col - dy;
                        int afterRow = row + 4 * dx;
                        int afterCol = col + 4 * dy;

                        bool openStart = IsWithinBounds(beforeRow, beforeCol, board) && board[beforeRow, beforeCol] == 0;
                        bool openEnd = IsWithinBounds(afterRow, afterCol, board) && board[afterRow, afterCol] == 0;

                        if (openStart && openEnd)
                        {
                            hasOpenFour = true;
                            break;
                        }
                    }

                    if (hasOpenFour)
                    {
                        break;
                    }
                }

                if (hasOpenFour)
                {
                    openFours++;
                }
            }

            return openFours;
        }

        private static int CountOpenThrees(int[,] board, int player)
        {
            int openThrees = 0;
            var directions = new (int dx, int dy)[]
            {
                (0, 1),
                (1, 0),
                (1, 1),
                (-1, 1)
            };

            foreach (var (dx, dy) in directions)
            {
                bool hasOpenThree = false;

                for (int row = 0; row < board.GetLength(0); row++)
                {
                    for (int col = 0; col < board.GetLength(1); col++)
                    {
                        bool sequence = true;
                        for (int i = 0; i < 3; i++)
                        {
                            int newRow = row + i * dx;
                            int newCol = col + i * dy;

                            if (!IsWithinBounds(newRow, newCol, board) || board[newRow, newCol] != player)
                            {
                                sequence = false;
                                break;
                            }
                        }

                        if (!sequence)
                        {
                            continue;
                        }

                        int beforeRow = row - dx;
                        int beforeCol = col - dy;
                        int afterRow = row + 3 * dx;
                        int afterCol = col + 3 * dy;

                        bool openStart = IsWithinBounds(beforeRow, beforeCol, board) && board[beforeRow, beforeCol] == 0;
                        bool openEnd = IsWithinBounds(afterRow, afterCol, board) && board[afterRow, afterCol] == 0;

                        if (openStart && openEnd)
                        {
                            hasOpenThree = true;
                            break;
                        }
                    }

                    if (hasOpenThree)
                    {
                        break;
                    }
                }

                if (hasOpenThree)
                {
                    openThrees++;
                }
            }

            return openThrees;
        }

        private static bool CheckRows(int[,] board, int player)
        {
            for (int row = 0; row < board.GetLength(0); row++)
            {
                int count = 0;
                for (int col = 0; col < board.GetLength(1); col++)
                {
                    count = board[row, col] == player ? count + 1 : 0;
                    if (count >= WinningCount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CheckColumns(int[,] board, int player)
        {
            for (int col = 0; col < board.GetLength(1); col++)
            {
                int count = 0;
                for (int row = 0; row < board.GetLength(0); row++)
                {
                    count = board[row, col] == player ? count + 1 : 0;
                    if (count >= WinningCount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CheckDiagonals(int[,] board, int player)
        {
            for (int startRow = 0; startRow <= board.GetLength(0) - WinningCount; startRow++)
            {
                for (int startCol = 0; startCol <= board.GetLength(1) - WinningCount; startCol++)
                {
                    int count = 0;
                    for (int i = 0; i < WinningCount; i++)
                    {
                        if (board[startRow + i, startCol + i] == player)
                        {
                            count++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (count == WinningCount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CheckAntiDiagonals(int[,] board, int player)
        {
            for (int startRow = 0; startRow <= board.GetLength(0) - WinningCount; startRow++)
            {
                for (int startCol = WinningCount - 1; startCol < board.GetLength(1); startCol++)
                {
                    int count = 0;
                    for (int i = 0; i < WinningCount; i++)
                    {
                        if (board[startRow + i, startCol - i] == player)
                        {
                            count++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (count == WinningCount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsWithinBounds(int row, int col, int[,] board)
            => row >= 0 && row < board.GetLength(0) && col >= 0 && col < board.GetLength(1);
    }
}
