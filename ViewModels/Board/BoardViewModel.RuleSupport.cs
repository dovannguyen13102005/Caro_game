using System;
using System.Collections.Generic;
using System.Linq;
using Caro_game.Models;

namespace Caro_game.ViewModels;

public partial class BoardViewModel
{
    private int[,] BuildBoardArray()
    {
        var board = new int[Rows, Columns];

        foreach (var cell in Cells)
        {
            if (string.IsNullOrEmpty(cell.Value))
            {
                continue;
            }

            board[cell.Row, cell.Col] = GetPlayerValue(cell.Value);
        }

        return board;
    }

    private static int GetPlayerValue(string symbol)
        => symbol.Equals("X", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

    private bool TryGetClassicWinningLine(int row, int col, string player, out List<(int Row, int Col)> winningCells)
    {
        int[][] directions = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 } };

        foreach (var dir in directions)
        {
            var line = GetLine(row, col, dir[0], dir[1], player);
            var opposite = GetLine(row, col, -dir[0], -dir[1], player);

            var combined = new List<Cell>();
            combined.AddRange(line);
            combined.AddRange(opposite);

            if (_cellLookup.TryGetValue((row, col), out var center))
            {
                combined.Add(center);
            }

            if (combined.Count >= 5)
            {
                winningCells = combined
                    .Select(c => (c.Row, c.Col))
                    .Distinct()
                    .ToList();
                return true;
            }
        }

        winningCells = new List<(int, int)>();
        return false;
    }

    private void HighlightWinningCells(IEnumerable<(int Row, int Col)> winningCells)
    {
        foreach (var cell in Cells)
        {
            cell.IsWinningCell = false;
        }

        foreach (var (row, col) in winningCells)
        {
            if (_cellLookup.TryGetValue((row, col), out var cell))
            {
                cell.IsWinningCell = true;
            }
        }
    }

    private void RestoreLastMoveState(Cell cell, Cell? previousLastMoveCell, string? previousLastMovePlayer, Cell? previousLastHumanMoveCell)
    {
        cell.Value = string.Empty;
        cell.IsLastMove = false;

        _lastMoveCell = previousLastMoveCell;
        _lastMovePlayer = previousLastMovePlayer;
        _lastHumanMoveCell = previousLastHumanMoveCell;

        if (previousLastMoveCell != null)
        {
            previousLastMoveCell.IsLastMove = true;
        }
    }
}
