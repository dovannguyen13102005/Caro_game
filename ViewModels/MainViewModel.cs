using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Caro_game.Commands;

namespace Caro_game.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _firstPlayer;
        private BoardViewModel _board;
        private bool _isAIEnabled;
        private string _selectedAIMode;

        public ObservableCollection<int> RowOptions { get; set; }
        public ObservableCollection<int> ColumnOptions { get; set; }
        public ObservableCollection<string> Players { get; set; }
        public ObservableCollection<string> AIModes { get; set; }

        private int _selectedRows;
        public int SelectedRows
        {
            get => _selectedRows;
            set { _selectedRows = value; OnPropertyChanged(); }
        }

        private int _selectedColumns;
        public int SelectedColumns
        {
            get => _selectedColumns;
            set { _selectedColumns = value; OnPropertyChanged(); }
        }

        public string FirstPlayer
        {
            get => _firstPlayer;
            set { _firstPlayer = value; OnPropertyChanged(); }
        }

        public BoardViewModel Board
        {
            get => _board;
            set { _board = value; OnPropertyChanged(); }
        }

        public bool IsAIEnabled
        {
            get => _isAIEnabled;
            set { _isAIEnabled = value; OnPropertyChanged(); }
        }

        public string SelectedAIMode
        {
            get => _selectedAIMode;
            set { _selectedAIMode = value; OnPropertyChanged(); }
        }

        public ICommand StartGameCommand { get; set; }
        public ICommand ExitCommand { get; set; }

        public MainViewModel()
        {
            RowOptions = new ObservableCollection<int> { 15, 20 };
            ColumnOptions = new ObservableCollection<int> { 18, 35 };
            Players = new ObservableCollection<string> { "X (Bạn)", "O" };
            AIModes = new ObservableCollection<string> { "Dễ", "Khó" };

            SelectedRows = 20;
            SelectedColumns = 35;
            FirstPlayer = "X (Bạn)";
            IsAIEnabled = true;
            SelectedAIMode = "Khó";

            StartGameCommand = new RelayCommand(StartGame);
            ExitCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());
        }

        private void StartGame(object parameter)
        {
            int rows = SelectedRows;
            int cols = SelectedColumns;

            Board = new BoardViewModel(rows, cols, FirstPlayer)
            {
                IsAIEnabled = this.IsAIEnabled,
                AIMode = this.SelectedAIMode
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
