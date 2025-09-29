using System;
using System.Linq;
using Caro_game.Models;

namespace Caro_game.ViewModels;

public partial class BoardViewModel
{
    public void LoadFromState(GameState state)
    {
        if (state.Rows != Rows || state.Columns != Columns)
        {
            throw new ArgumentException("Kích thước bàn không khớp với trạng thái đã lưu.");
        }

        foreach (var cell in Cells)
        {
            cell.Value = string.Empty;
            cell.IsWinningCell = false;
        }

        if (!string.IsNullOrWhiteSpace(state.FirstPlayer))
        {
            _initialPlayer = state.FirstPlayer!;
        }

        if (!string.IsNullOrWhiteSpace(state.HumanPiece))
        {
            HumanPiece = state.HumanPiece!;
        }

        if (!string.IsNullOrWhiteSpace(state.AiPiece))
        {
            AiPiece = state.AiPiece!;
        }

        if (state.Cells != null)
        {
            foreach (var cellState in state.Cells)
            {
                if (_cellLookup.TryGetValue((cellState.Row, cellState.Col), out var cell))
                {
                    cell.Value = cellState.Value ?? string.Empty;
                    cell.IsWinningCell = cellState.IsWinningCell;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(state.CurrentPlayer))
        {
            CurrentPlayer = state.CurrentPlayer!;
        }

        RebuildCandidatePositions();

        IsPaused = state.IsPaused;
    }

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
}
