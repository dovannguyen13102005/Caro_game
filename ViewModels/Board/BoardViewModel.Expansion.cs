using System.Collections.Generic;
using System.Linq;
using Caro_game.Models;

namespace Caro_game.ViewModels;

public partial class BoardViewModel
{
    private void ExpandBoardIfNeeded(int originalRow, int originalCol)
    {
        if (!_allowBoardExpansion || IsAIEnabled)
        {
            return;
        }

        int previousRows = Rows;
        int previousCols = Columns;

        bool addTop = originalRow == 0;
        bool addBottom = originalRow == previousRows - 1;
        bool addLeft = originalCol == 0;
        bool addRight = originalCol == previousCols - 1;

        if (!(addTop || addBottom || addLeft || addRight))
        {
            return;
        }

        if (addTop)
        {
            AddRowTop();
        }

        if (addBottom)
        {
            AddRowBottom();
        }

        if (addLeft)
        {
            AddColumnLeft();
        }

        if (addRight)
        {
            AddColumnRight();
        }

        RebuildCellsCollection();
    }

    private void AddRowTop()
    {
        ShiftAllCells(1, 0);

        int currentColumns = Columns;
        for (int col = 0; col < currentColumns; col++)
        {
            var cell = new Cell(0, col, this);
            _cellLookup[(0, col)] = cell;
        }

        Rows += 1;
    }

    private void AddRowBottom()
    {
        int newRowIndex = Rows;
        for (int col = 0; col < Columns; col++)
        {
            var cell = new Cell(newRowIndex, col, this);
            _cellLookup[(newRowIndex, col)] = cell;
        }

        Rows += 1;
    }

    private void AddColumnLeft()
    {
        ShiftAllCells(0, 1);

        int currentRows = Rows;
        for (int row = 0; row < currentRows; row++)
        {
            var cell = new Cell(row, 0, this);
            _cellLookup[(row, 0)] = cell;
        }

        Columns += 1;
    }

    private void AddColumnRight()
    {
        int newColumnIndex = Columns;
        for (int row = 0; row < Rows; row++)
        {
            var cell = new Cell(row, newColumnIndex, this);
            _cellLookup[(row, newColumnIndex)] = cell;
        }

        Columns += 1;
    }

    private void ShiftAllCells(int rowDelta, int colDelta)
    {
        if (rowDelta == 0 && colDelta == 0)
        {
            return;
        }

        var shiftedLookup = new Dictionary<(int Row, int Col), Cell>(_cellLookup.Count);

        foreach (var cell in _cellLookup.Values)
        {
            cell.Row += rowDelta;
            cell.Col += colDelta;
            shiftedLookup[(cell.Row, cell.Col)] = cell;
        }

        _cellLookup.Clear();
        foreach (var kvp in shiftedLookup)
        {
            _cellLookup[kvp.Key] = kvp.Value;
        }

        if (_candidatePositions.Count > 0)
        {
            var shiftedCandidates = new HashSet<(int Row, int Col)>(_candidatePositions.Count);
            foreach (var (row, col) in _candidatePositions)
            {
                shiftedCandidates.Add((row + rowDelta, col + colDelta));
            }

            _candidatePositions.Clear();
            foreach (var position in shiftedCandidates)
            {
                _candidatePositions.Add(position);
            }
        }
    }

    private void RebuildCellsCollection()
    {
        var ordered = _cellLookup.Values
            .OrderBy(c => c.Row)
            .ThenBy(c => c.Col)
            .ToList();

        Cells.Clear();

        foreach (var cell in ordered)
        {
            Cells.Add(cell);
        }
    }
}
