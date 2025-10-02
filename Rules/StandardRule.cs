namespace Caro_game.Rules
{
    public class StandardRule : IRule
    {
        private static readonly (int dx, int dy)[] Directions =
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        public bool IsWinning(int[,] board, int player)
        {
            int rows = board.GetLength(0);
            int cols = board.GetLength(1);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (board[row, col] != player)
                    {
                        continue;
                    }

                    foreach (var (dx, dy) in Directions)
                    {
                        if (IsStandardWin(board, player, row, col, dx, dy))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool IsForbiddenMove(int[,] board, int player) => false;

        public IRule Clone() => new StandardRule();

        private static bool IsStandardWin(int[,] board, int player, int row, int col, int dx, int dy)
        {
            int count = 1;

            int r = row + dx;
            int c = col + dy;
            while (IsInBounds(r, c, board) && board[r, c] == player)
            {
                count++;
                r += dx;
                c += dy;
            }
            bool end1 = !IsInBounds(r, c, board) || board[r, c] != player;

            r = row - dx;
            c = col - dy;
            while (IsInBounds(r, c, board) && board[r, c] == player)
            {
                count++;
                r -= dx;
                c -= dy;
            }
            bool end2 = !IsInBounds(r, c, board) || board[r, c] != player;

            return count == 5 && end1 && end2;
        }

        private static bool IsInBounds(int row, int col, int[,] board)
            => row >= 0 && row < board.GetLength(0) && col >= 0 && col < board.GetLength(1);
    }
}
