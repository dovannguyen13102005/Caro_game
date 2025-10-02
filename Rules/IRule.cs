namespace Caro_game.Rules
{
    public interface IRule
    {
        bool IsWinning(int[,] board, int player);

        bool IsForbiddenMove(int[,] board, int player);

        IRule Clone();
    }
}
