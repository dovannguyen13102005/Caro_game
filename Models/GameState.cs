using System;
using System.Collections.Generic;

namespace Caro_game.Models
{
    public class GameState
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public string? FirstPlayer { get; set; }
        public string? CurrentPlayer { get; set; }
        public bool IsAIEnabled { get; set; }
        public string? AIMode { get; set; }
        public int TimeLimitMinutes { get; set; }
        public int? RemainingSeconds { get; set; }
        public bool IsPaused { get; set; }
        public DateTime SavedAt { get; set; }
        public List<CellState> Cells { get; set; } = new();
        public GameRule Rule { get; set; } = GameRule.Freestyle;
        public List<CoordinateState> ForbiddenCells { get; set; } = new();
        public List<StonePlacementState> InitialStones { get; set; } = new();
        public bool AllowExpansion { get; set; }
        public int ExpansionThreshold { get; set; } = 2;
        public int MaxRows { get; set; }
        public int MaxColumns { get; set; }
        public int InitialRows { get; set; }
        public int InitialColumns { get; set; }
    }
}
