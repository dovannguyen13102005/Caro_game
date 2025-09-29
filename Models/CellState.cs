namespace Caro_game.Models
{
    public class CellState
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public string? Value { get; set; }
        public bool IsWinningCell { get; set; }
    }
}
