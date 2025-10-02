namespace Caro_game.Rules
{
    public class FreestyleRule : IRule
    {
        private const int WinningCount = 5;

        public bool IsWinning(int[,] board, int player)
            => CheckRows(board, player)
               || CheckColumns(board, player)
               || CheckDiagonals(board, player)
               || CheckAntiDiagonals(board, player);

        public bool IsForbiddenMove(int[,] board, int player) => false;

        public IRule Clone() => new FreestyleRule();

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
            for (int d = -board.GetLength(0) + 1; d < board.GetLength(1); d++)
            {
                int count = 0;
                for (int row = 0; row < board.GetLength(0); row++)
                {
                    int col = row + d;
                    if (col >= 0 && col < board.GetLength(1))
                    {
                        count = board[row, col] == player ? count + 1 : 0;
                        if (count >= WinningCount)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool CheckAntiDiagonals(int[,] board, int player)
        {
            for (int d = 0; d < board.GetLength(0) + board.GetLength(1) - 1; d++)
            {
                int count = 0;
                for (int row = 0; row < board.GetLength(0); row++)
                {
                    int col = d - row;
                    if (col >= 0 && col < board.GetLength(1))
                    {
                        count = board[row, col] == player ? count + 1 : 0;
                        if (count >= WinningCount)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
