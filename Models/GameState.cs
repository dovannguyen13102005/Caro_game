using System;

namespace Caro_game.Models
{
    public class GameState
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public string CurrentPlayer { get; set; } = string.Empty;
        public bool IsAIEnabled { get; set; }
        public string AIMode { get; set; } = string.Empty;
        public bool IsPaused { get; set; }
        public string[] Cells { get; set; } = Array.Empty<string>();
    }
}
