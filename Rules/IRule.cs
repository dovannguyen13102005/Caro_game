using System.Collections.Generic;

namespace Caro_game.Rules
{
    public interface IRule
    {
        string Name { get; }

        int BoardSize { get; }

        bool AllowBoardExpansion { get; }

        string? EngineRuleKeyword { get; }

        string? ForbiddenMoveMessage { get; }

        string? GetConfigFileName(bool aiIsBlack);

        bool IsForbiddenMove(int[,] board, int player, int lastRow, int lastCol);

        bool TryGetWinningLine(int[,] board, int player, int lastRow, int lastCol, out List<(int Row, int Col)> winningCells);

        IRule Clone();
    }
}
