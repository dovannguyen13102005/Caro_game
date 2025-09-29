using System;

namespace Caro_game.Models
{
    public class GameEndedEventArgs : EventArgs
    {
        public GameEndedEventArgs(string winner, bool playAgain, bool hasWinner)
        {
            Winner = winner;
            PlayAgain = playAgain;
            HasWinner = hasWinner;
        }

        public string Winner { get; }
        public bool PlayAgain { get; }
        public bool HasWinner { get; }
    }
}
