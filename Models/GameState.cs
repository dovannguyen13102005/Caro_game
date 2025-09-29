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
        public string? AILevel { get; set; }
        public int TimeLimitMinutes { get; set; }
        public int? RemainingSeconds { get; set; }
        public bool IsPaused { get; set; }
        public DateTime SavedAt { get; set; }
        public List<CellState> Cells { get; set; } = new();
    }
}
