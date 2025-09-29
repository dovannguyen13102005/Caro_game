using System.Collections.Generic;
using System.Linq;
using Caro_game.Models;

namespace Caro_game.ViewModels
{
    public partial class BoardViewModel
    {
        private void RebuildCandidatePositions()
        {
            lock (_candidateLock)
            {
                _candidatePositions.Clear();

                foreach (var filled in Cells.Where(c => !string.IsNullOrEmpty(c.Value)))
                {
                    foreach (var neighbor in GetNeighbors(filled.Row, filled.Col, 2))
                    {
                        if (string.IsNullOrEmpty(neighbor.Value))
                        {
                            _candidatePositions.Add((neighbor.Row, neighbor.Col));
                        }
                    }
                }
            }
        }

        private void UpdateCandidatePositions(int row, int col)
        {
            lock (_candidateLock)
            {
                _candidatePositions.Remove((row, col));

                foreach (var neighbor in GetNeighbors(row, col, 2))
                {
                    if (string.IsNullOrEmpty(neighbor.Value))
                    {
                        _candidatePositions.Add((neighbor.Row, neighbor.Col));
                    }
                }
            }
        }

        private IEnumerable<Cell> GetNeighbors(int row, int col, int range)
        {
            for (int dr = -range; dr <= range; dr++)
            {
                for (int dc = -range; dc <= range; dc++)
                {
                    if (dr == 0 && dc == 0) continue;

                    int r = row + dr;
                    int c = col + dc;

                    if (_cellLookup.TryGetValue((r, c), out var neighbor))
                    {
                        yield return neighbor;
                    }
                }
            }
        }

        private void ExpandBoardIfNeeded(Cell movedCell)
        {
            if (!_allowDynamicResize)
            {
                return;
            }

            const int threshold = 2;
            bool expanded = false;

            if (movedCell.Row <= threshold)
            {
                AddRowTop();
                expanded = true;
            }

            if (Rows - 1 - movedCell.Row <= threshold)
            {
                AddRowBottom();
                expanded = true;
            }

            if (movedCell.Col <= threshold)
            {
                AddColumnLeft();
                expanded = true;
            }

            if (Columns - 1 - movedCell.Col <= threshold)
            {
                AddColumnRight();
                expanded = true;
            }

            if (expanded)
            {
                RebuildCandidatePositions();
            }
        }

        private void AddRowTop()
        {
            foreach (var cell in Cells)
            {
                cell.Row++;
            }

            var newCells = Enumerable.Range(0, Columns)
                .Select(c => new Cell(0, c, this))
                .ToList();

            Rows = Rows + 1;
            RefreshCells(newCells);
        }

        private void AddRowBottom()
        {
            var newRow = Rows;
            var newCells = Enumerable.Range(0, Columns)
                .Select(c => new Cell(newRow, c, this))
                .ToList();

            Rows = Rows + 1;
            RefreshCells(newCells);
        }

        private void AddColumnLeft()
        {
            foreach (var cell in Cells)
            {
                cell.Col++;
            }

            var newCells = Enumerable.Range(0, Rows)
                .Select(r => new Cell(r, 0, this))
                .ToList();

            Columns = Columns + 1;
            RefreshCells(newCells);
        }

        private void AddColumnRight()
        {
            var newColumn = Columns;
            var newCells = Enumerable.Range(0, Rows)
                .Select(r => new Cell(r, newColumn, this))
                .ToList();

            Columns = Columns + 1;
            RefreshCells(newCells);
        }

        private void RefreshCells(IEnumerable<Cell>? additionalCells = null)
        {
            var combined = Cells.ToList();
            if (additionalCells != null)
            {
                combined.AddRange(additionalCells);
            }

            combined.Sort((a, b) =>
            {
                int rowCompare = a.Row.CompareTo(b.Row);
                return rowCompare != 0 ? rowCompare : a.Col.CompareTo(b.Col);
            });

            Cells.Clear();
            _cellLookup.Clear();

            foreach (var cell in combined)
            {
                Cells.Add(cell);
                _cellLookup[(cell.Row, cell.Col)] = cell;
            }
        }

        private bool HasNeighbor(Cell cell, int range)
            => GetNeighbors(cell.Row, cell.Col, range).Any(n => !string.IsNullOrEmpty(n.Value));
    }
}
