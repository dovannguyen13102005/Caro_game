using System;
using System.Collections.Generic;
using Caro_game.Models;

namespace Caro_game.Rules;

public static class GomokuRuleEvaluator
{
    private readonly struct LineData
    {
        public LineData(char[] values, (int Row, int Col)[] positions, int centerIndex)
        {
            Values = values;
            Positions = positions;
            CenterIndex = centerIndex;
        }

        public char[] Values { get; }
        public (int Row, int Col)[] Positions { get; }
        public int CenterIndex { get; }
    }

    private static readonly (int Row, int Col)[] Directions =
    {
        (0, 1),
        (1, 0),
        (1, 1),
        (1, -1)
    };

    public static bool IsMoveValid(
        Func<int, int, string?> getCellValue,
        int rows,
        int cols,
        int row,
        int col,
        string player,
        GameRuleType rule,
        out string? violationMessage)
    {
        violationMessage = null;

        if (rule != GameRuleType.Renju)
        {
            return true;
        }

        char playerChar = NormalizePlayer(player);
        if (playerChar != 'X')
        {
            return true;
        }

        bool hasOverline = false;
        var fourThreats = new HashSet<(int Row, int Col)>();
        var openThreeCandidates = new HashSet<(int Row, int Col)>();

        foreach (var (dRow, dCol) in Directions)
        {
            var line = BuildLine(getCellValue, rows, cols, row, col, dRow, dCol);

            if (HasOverline(line, playerChar))
            {
                hasOverline = true;
            }

            foreach (var pos in CollectWinningPositions(line, playerChar))
            {
                fourThreats.Add(pos);
            }

            foreach (var pos in CollectOpenThreeCandidates(line, playerChar))
            {
                openThreeCandidates.Add(pos);
            }
        }

        if (hasOverline)
        {
            violationMessage = "Luật Renju: quân X không được tạo overline (6 quân liên tiếp).";
            return false;
        }

        if (fourThreats.Count >= 2)
        {
            violationMessage = "Luật Renju: nước đi tạo đồng thời hai thế bốn (double-four).";
            return false;
        }

        openThreeCandidates.ExceptWith(fourThreats);
        if (openThreeCandidates.Count >= 2)
        {
            violationMessage = "Luật Renju: nước đi tạo đồng thời hai thế ba mở (double-three).";
            return false;
        }

        return true;
    }

    public static bool CheckWin(
        Func<int, int, string?> getCellValue,
        int rows,
        int cols,
        int row,
        int col,
        string player,
        GameRuleType rule)
    {
        char playerChar = NormalizePlayer(player);

        foreach (var (dRow, dCol) in Directions)
        {
            var line = BuildLine(getCellValue, rows, cols, row, col, dRow, dCol);

            if (HasWinningSequence(line, playerChar, rule))
            {
                return true;
            }
        }

        return false;
    }

    private static LineData BuildLine(
        Func<int, int, string?> getCellValue,
        int rows,
        int cols,
        int row,
        int col,
        int dRow,
        int dCol)
    {
        int startRow = row;
        int startCol = col;

        while (IsInside(rows, cols, startRow - dRow, startCol - dCol))
        {
            startRow -= dRow;
            startCol -= dCol;
        }

        var values = new List<char>();
        var positions = new List<(int Row, int Col)>();
        int centerIndex = -1;

        int currentRow = startRow;
        int currentCol = startCol;

        while (IsInside(rows, cols, currentRow, currentCol))
        {
            values.Add(ToCellChar(getCellValue(currentRow, currentCol)));
            positions.Add((currentRow, currentCol));

            if (currentRow == row && currentCol == col)
            {
                centerIndex = values.Count - 1;
            }

            currentRow += dRow;
            currentCol += dCol;
        }

        if (centerIndex < 0)
        {
            throw new InvalidOperationException("Không xác định được vị trí trung tâm trong đường kiểm tra.");
        }

        return new LineData(values.ToArray(), positions.ToArray(), centerIndex);
    }

    private static bool HasWinningSequence(LineData line, char playerChar, GameRuleType rule)
    {
        var values = line.Values;
        bool allowOverline = rule == GameRuleType.Freestyle || (rule == GameRuleType.Renju && playerChar == 'O');

        int consecutive = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == playerChar)
            {
                consecutive++;
            }
            else
            {
                consecutive = 0;
            }

            if (consecutive < 5)
            {
                continue;
            }

            int start = i - consecutive + 1;
            int end = i;

            if (line.CenterIndex < start || line.CenterIndex > end)
            {
                continue;
            }

            if (allowOverline)
            {
                return true;
            }

            if (consecutive == 5)
            {
                bool leftSame = start - 1 >= 0 && values[start - 1] == playerChar;
                bool rightSame = end + 1 < values.Length && values[end + 1] == playerChar;

                if (!leftSame && !rightSame)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasOverline(LineData line, char playerChar)
    {
        int count = 1;

        for (int i = line.CenterIndex - 1; i >= 0 && line.Values[i] == playerChar; i--)
        {
            count++;
        }

        for (int i = line.CenterIndex + 1; i < line.Values.Length && line.Values[i] == playerChar; i++)
        {
            count++;
        }

        return count > 5;
    }

    private static IEnumerable<(int Row, int Col)> CollectWinningPositions(LineData line, char playerChar)
    {
        var positions = new HashSet<int>();

        int minStart = Math.Max(0, line.CenterIndex - 4);
        int maxStart = Math.Min(line.Values.Length - 5, line.CenterIndex);

        for (int start = minStart; start <= maxStart; start++)
        {
            int end = start + 4;
            if (line.CenterIndex < start || line.CenterIndex > end)
            {
                continue;
            }

            int playerCount = 0;
            int emptyIndex = -1;
            bool invalid = false;

            for (int offset = 0; offset < 5; offset++)
            {
                char value = line.Values[start + offset];
                if (value == playerChar)
                {
                    playerCount++;
                }
                else if (value == '.')
                {
                    if (emptyIndex == -1)
                    {
                        emptyIndex = start + offset;
                    }
                    else
                    {
                        invalid = true;
                        break;
                    }
                }
                else
                {
                    invalid = true;
                    break;
                }
            }

            if (invalid || playerCount != 4 || emptyIndex == -1)
            {
                continue;
            }

            positions.Add(emptyIndex);
        }

        foreach (var index in positions)
        {
            yield return line.Positions[index];
        }
    }

    private static IEnumerable<(int Row, int Col)> CollectOpenThreeCandidates(LineData line, char playerChar)
    {
        var candidates = new HashSet<int>();

        for (int index = 0; index < line.Values.Length; index++)
        {
            if (line.Values[index] != '.')
            {
                continue;
            }

            if (Math.Abs(index - line.CenterIndex) > 4)
            {
                continue;
            }

            line.Values[index] = playerChar;

            if (CreatesOpenFourInLine(line, index, playerChar))
            {
                candidates.Add(index);
            }

            line.Values[index] = '.';
        }

        foreach (var idx in candidates)
        {
            yield return line.Positions[idx];
        }
    }

    private static bool CreatesOpenFourInLine(LineData line, int index, char playerChar)
    {
        var values = line.Values;

        for (int start = index - 3; start <= index; start++)
        {
            int end = start + 3;
            if (start < 0 || end >= values.Length)
            {
                continue;
            }

            if (!(index >= start && index <= end))
            {
                continue;
            }

            if (!(line.CenterIndex >= start && line.CenterIndex <= end))
            {
                continue;
            }

            bool fourInARow = true;
            for (int offset = 0; offset < 4; offset++)
            {
                if (values[start + offset] != playerChar)
                {
                    fourInARow = false;
                    break;
                }
            }

            if (!fourInARow)
            {
                continue;
            }

            char left = start - 1 >= 0 ? values[start - 1] : 'B';
            char right = end + 1 < values.Length ? values[end + 1] : 'B';

            if (left == '.' && right == '.')
            {
                return true;
            }
        }

        return false;
    }

    private static char ToCellChar(string? value)
        => string.Equals(value, "X", StringComparison.OrdinalIgnoreCase)
            ? 'X'
            : string.Equals(value, "O", StringComparison.OrdinalIgnoreCase)
                ? 'O'
                : '.';

    private static char NormalizePlayer(string player)
        => string.Equals(player, "O", StringComparison.OrdinalIgnoreCase) ? 'O' : 'X';

    private static bool IsInside(int rows, int cols, int row, int col)
        => row >= 0 && row < rows && col >= 0 && col < cols;
}
