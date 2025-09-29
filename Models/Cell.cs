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

        public int Row { get; set; }
        public int Col { get; set; }

        public string Value
        {
            get => _value;
            set => SetValue(value);
        }

        public bool IsWinningCell
        {
            get => _isWinningCell;
            set { _isWinningCell = value; OnPropertyChanged(); }
        }

        public ICommand ClickCommand { get; set; }

        public Cell(int row, int col, BoardViewModel board)
        {
            Row = row;
            Col = col;
            Value = string.Empty;
            IsWinningCell = false;
            ClickCommand = new RelayCommand(o => board.MakeMove(this));
        }

        public void SetValue(string value, bool notify = true)
        {
            if (_value == value)
            {
                return;
            }

            _value = value;

            if (notify)
            {
                OnPropertyChanged(nameof(Value));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
