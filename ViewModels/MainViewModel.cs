using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Caro_game.Commands;

namespace Caro_game.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _firstPlayer;
        private BoardViewModel _board;
        private bool _isAIEnabled;
        private string _selectedAIMode;

        // Thuộc tính cho cấu hình bảng
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

        // --- Cài đặt giao diện ---
        public ObservableCollection<string> Themes { get; set; } =
            new ObservableCollection<string> { "Dark (mặc định)", "Light" };

        public ObservableCollection<string> PrimaryColors { get; set; } =
            new ObservableCollection<string> { "Xanh dương", "Tím", "Lục" };

        private string _selectedTheme;
        public string SelectedTheme
        {
            get => _selectedTheme;
            set { _selectedTheme = value; OnPropertyChanged(); }
        }

        private string _selectedPrimaryColor;
        public string SelectedPrimaryColor
        {
            get => _selectedPrimaryColor;
            set { _selectedPrimaryColor = value; OnPropertyChanged(); }
        }

        private bool _isSoundEnabled;
        public bool IsSoundEnabled
        {
            get => _isSoundEnabled;
            set { _isSoundEnabled = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand StartGameCommand { get; set; }
        public ICommand ExitCommand { get; set; }
        public ICommand SaveSettingsCommand { get; set; }

        public MainViewModel()
        {
            // Options cho bàn cờ
            RowOptions = new ObservableCollection<int> { 15, 20 };
            ColumnOptions = new ObservableCollection<int> { 18, 35 };
            Players = new ObservableCollection<string> { "X (Bạn)", "O" };
            AIModes = new ObservableCollection<string> { "Dễ", "Khó" };

            // Default
            SelectedRows = 20;
            SelectedColumns = 35;
            FirstPlayer = "X (Bạn)";
            IsAIEnabled = true;
            SelectedAIMode = "Khó";

            SelectedTheme = "Dark (mặc định)";
            SelectedPrimaryColor = "Xanh dương";
            IsSoundEnabled = true;

            // Commands
            StartGameCommand = new RelayCommand(StartGame);
            ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
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

        private void SaveSettings()
        {
            // Theme
            if (SelectedTheme == "Light")
            {
                Application.Current.MainWindow.Background = new SolidColorBrush(Colors.WhiteSmoke);
                Application.Current.Resources["DefaultForeground"] = new SolidColorBrush(Colors.Black);
            }
            else // Dark
            {
                Application.Current.MainWindow.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
                Application.Current.Resources["DefaultForeground"] = new SolidColorBrush(Colors.White);
            }

            // Primary color
            Color primaryColor = Colors.DeepSkyBlue;
            if (SelectedPrimaryColor == "Tím") primaryColor = Colors.MediumPurple;
            else if (SelectedPrimaryColor == "Lục") primaryColor = Colors.MediumSeaGreen;

            Application.Current.Resources["Primary"] = new SolidColorBrush(primaryColor);

            // Thông báo
            MessageBox.Show("Cài đặt đã được áp dụng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
