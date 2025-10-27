using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Caro_game.Models
{
    public class PlayerInfo : INotifyPropertyChanged
    {
        private string _name;
        private string _avatarPath;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string AvatarPath
        {
            get => _avatarPath;
            set
            {
                if (_avatarPath != value)
                {
                    _avatarPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Symbol { get; set; } // "X" or "O"

        public PlayerInfo(string name, string symbol, string avatarPath = "")
        {
            _name = name;
            Symbol = symbol;
            _avatarPath = avatarPath;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
