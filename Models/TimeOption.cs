namespace Caro_game.Models
{
    public class TimeOption
    {
        public TimeOption(int minutes, string display)
        {
            Minutes = minutes;
            Display = display;
        }

        public int Minutes { get; }
        public string Display { get; }

        public override string ToString() => Display;
    }
}
