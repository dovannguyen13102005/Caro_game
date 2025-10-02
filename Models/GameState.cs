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
        public string? HumanSymbol { get; set; }
        public string? Rule { get; set; }
        public bool IsAIEnabled { get; set; }
        public string? AIMode { get; set; }
        public string? FirstPlayerSelection { get; set; }
        public int TimeLimitMinutes { get; set; }
        public string? TimeOptionDisplay { get; set; }
        public int? RemainingSeconds { get; set; }
        public bool IsPaused { get; set; }
        public DateTime SavedAt { get; set; }
        public List<CellState> Cells { get; set; } = new();
        public int? LastMoveRow { get; set; }
        public int? LastMoveCol { get; set; }
        public string? LastMovePlayer { get; set; }
        public int? LastHumanMoveRow { get; set; }
        public int? LastHumanMoveCol { get; set; }
    }
}
