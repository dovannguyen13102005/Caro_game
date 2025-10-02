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
            cell.IsLastMove = false;
        }

        _moveHistory.Clear();

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

        if (state.Moves != null)
        {
            foreach (var move in state.Moves)
            {
                var normalized = NormalizePlayerSymbol(move.Player);
                _moveHistory.Add(new MoveState
                {
                    Row = move.Row,
                    Col = move.Col,
                    Player = normalized
                });
            }
        }

        _lastMoveCell = null;
        _lastHumanMoveCell = null;
        _lastMovePlayer = null;

        if (_moveHistory.Count > 0)
        {
            var lastMoveState = _moveHistory[^1];
            _lastMovePlayer = lastMoveState.Player;

            if (_cellLookup.TryGetValue((lastMoveState.Row, lastMoveState.Col), out var lastMoveCell))
            {
                _lastMoveCell = lastMoveCell;
                lastMoveCell.IsLastMove = true;
            }

            var lastHumanMoveState = _moveHistory.LastOrDefault(m => m.Player == _humanSymbol);
            if (lastHumanMoveState != null &&
                _cellLookup.TryGetValue((lastHumanMoveState.Row, lastHumanMoveState.Col), out var lastHumanCell))
            {
                _lastHumanMoveCell = lastHumanCell;
            }
        }
        else
        {
            if (state.LastMoveRow.HasValue && state.LastMoveCol.HasValue &&
                _cellLookup.TryGetValue((state.LastMoveRow.Value, state.LastMoveCol.Value), out var lastMove))
            {
                _lastMoveCell = lastMove;
                _lastMovePlayer = string.IsNullOrWhiteSpace(state.LastMovePlayer)
                    ? null
                    : NormalizePlayerSymbol(state.LastMovePlayer);
                lastMove.IsLastMove = true;
            }

            if (state.LastHumanMoveRow.HasValue && state.LastHumanMoveCol.HasValue &&
                _cellLookup.TryGetValue((state.LastHumanMoveRow.Value, state.LastHumanMoveCol.Value), out var lastHuman))
            {
                _lastHumanMoveCell = lastHuman;
            }
        }

        if (!string.IsNullOrWhiteSpace(state.CurrentPlayer))
        {
            CurrentPlayer = state.CurrentPlayer!;
        }

        RebuildCandidatePositions();

        IsPaused = state.IsPaused;

        if (AIMode == "Chuyên nghiệp" && IsAIEnabled)
        {
            SyncProfessionalEngineWithMoves();
        }
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
