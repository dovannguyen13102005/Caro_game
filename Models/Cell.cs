using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Caro_game.Commands;
using Caro_game.ViewModels;

namespace Caro_game.Models
{
    public class Cell : INotifyPropertyChanged
    {
        private string _value;
        private bool _isWinningCell;
        private bool _isBlocked;

        public int Row { get; set; }
        public int Col { get; set; }

        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayValue));
                }
            }
        }

        public bool IsWinningCell
        {
            get => _isWinningCell;
            set
            {
                if (_isWinningCell != value)
                {
                    _isWinningCell = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBlocked
        {
            get => _isBlocked;
            set
            {
                if (_isBlocked != value)
                {
                    _isBlocked = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayValue));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string DisplayValue => IsBlocked ? "🚫" : Value;

        public ICommand ClickCommand { get; set; }

        public Cell(int row, int col, BoardViewModel board)
        {
            Row = row;
            Col = col;
            Value = string.Empty;
            IsWinningCell = false;
            IsBlocked = false;
            ClickCommand = new RelayCommand(_ => board.MakeMove(this), _ => board.CanMakeMove(this));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
