using Caro_game.Commands;
using Caro_game.Models;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Caro_game.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _firstPlayer;
        private BoardViewModel _board;
        private bool _isAIEnabled;
        private string _selectedAIMode;
        private string _pauseButtonText = "Tạm dừng";

        // Thuộc tính cho cấu hình bảng
        public ObservableCollection<int> RowOptions { get; set; }
        public ObservableCollection<int> ColumnOptions { get; set; }
        public ObservableCollection<string> Players { get; set; }
        public ObservableCollection<string> AIModes { get; set; }

        private int _selectedRows;
        public int SelectedRows
        {
            get => _selectedRows;
            set
            {
                _selectedRows = value;
                OnPropertyChanged();
            }
        }

        private int _selectedColumns;
        public int SelectedColumns
        {
            get => _selectedColumns;
            set
            {
                _selectedColumns = value;
                OnPropertyChanged();
            }
        }

        public string FirstPlayer
        {
            get => _firstPlayer;
            set
            {
                _firstPlayer = value;
                OnPropertyChanged();
            }
        }

        public BoardViewModel Board
        {
            get => _board;
            set
            {
                if (_board != null)
                {
                    _board.PropertyChanged -= Board_PropertyChanged;
                }

                _board = value;

                if (_board != null)
                {
                    _board.PropertyChanged += Board_PropertyChanged;
                    _board.IsSoundEnabled = IsSoundEnabled;
                    _board.IsAIEnabled = IsAIEnabled;
                    _board.AIMode = SelectedAIMode;
                    PauseButtonText = _board.IsPaused ? "Tiếp tục" : "Tạm dừng";
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveGame));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasActiveGame => Board != null;

        public bool IsAIEnabled
        {
            get => _isAIEnabled;
            set
            {
                _isAIEnabled = value;
                OnPropertyChanged();
                if (Board != null)
                {
                    Board.IsAIEnabled = value;
                }
            }
        }

        public string SelectedAIMode
        {
            get => _selectedAIMode;
            set
            {
                _selectedAIMode = value;
                OnPropertyChanged();
                if (Board != null)
                {
                    Board.AIMode = value;
                }
            }
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
            set
            {
                _selectedTheme = value;
                OnPropertyChanged();
            }
        }

        private string _selectedPrimaryColor;
        public string SelectedPrimaryColor
        {
            get => _selectedPrimaryColor;
            set
            {
                _selectedPrimaryColor = value;
                OnPropertyChanged();
            }
        }

        private bool _isSoundEnabled;
        public bool IsSoundEnabled
        {
            get => _isSoundEnabled;
            set
            {
                _isSoundEnabled = value;
                OnPropertyChanged();
                if (Board != null)
                {
                    Board.IsSoundEnabled = value;
                }
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string PauseButtonText
        {
            get => _pauseButtonText;
            set
            {
                _pauseButtonText = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand StartGameCommand { get; set; }
        public ICommand ExitCommand { get; set; }
        public ICommand SaveSettingsCommand { get; set; }
        public ICommand TogglePauseCommand { get; set; }
        public ICommand SaveGameCommand { get; set; }
        public ICommand OpenGameCommand { get; set; }

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
            TogglePauseCommand = new RelayCommand(_ => TogglePause(), _ => HasActiveGame);
            SaveGameCommand = new RelayCommand(_ => SaveGame(), _ => HasActiveGame);
            OpenGameCommand = new RelayCommand(_ => OpenGame());
        }

        private void StartGame(object parameter)
        {
            int rows = SelectedRows;
            int cols = SelectedColumns;

            Board = new BoardViewModel(rows, cols, FirstPlayer)
            {
                IsAIEnabled = this.IsAIEnabled,
                AIMode = this.SelectedAIMode,
                IsSoundEnabled = this.IsSoundEnabled
            };
            PauseButtonText = "Tạm dừng";
            StatusMessage = "Bắt đầu ván chơi mới.";
        }

        private void TogglePause()
        {
            if (Board == null)
            {
                return;
            }

            Board.IsPaused = !Board.IsPaused;
            PauseButtonText = Board.IsPaused ? "Tiếp tục" : "Tạm dừng";
            StatusMessage = Board.IsPaused ? "Ván chơi đã tạm dừng." : "Tiếp tục ván chơi.";
        }

        private void SaveGame()
        {
            if (Board == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Caro Game (*.caro)|*.caro|JSON (*.json)|*.json",
                DefaultExt = ".caro",
                FileName = "caro-game.caro"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var state = Board.ToGameState();
                    var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dialog.FileName, json);
                    StatusMessage = "Đã lưu ván chơi hiện tại.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể lưu ván chơi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenGame()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Caro Game (*.caro)|*.caro|JSON (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var state = JsonSerializer.Deserialize<GameState>(json);

                    if (state != null)
                    {
                        SelectedRows = state.Rows;
                        SelectedColumns = state.Columns;
                        IsAIEnabled = state.IsAIEnabled;
                        SelectedAIMode = string.IsNullOrEmpty(state.AIMode) ? SelectedAIMode : state.AIMode;
                        FirstPlayer = state.CurrentPlayer == "O" ? "O" : "X (Bạn)";

                        var board = new BoardViewModel(state)
                        {
                            IsSoundEnabled = this.IsSoundEnabled
                        };
                        Board = board;
                        PauseButtonText = board.IsPaused ? "Tiếp tục" : "Tạm dừng";
                        StatusMessage = "Đã mở ván chơi từ file.";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể mở ván chơi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveSettings()
        {
            if (SelectedTheme == "Light")
            {
                UpdateBrush("WindowBackgroundBrush", Color.FromRgb(248, 250, 252));
                UpdateBrush("PanelBackgroundBrush", Colors.White);
                UpdateBrush("AccentPanelBackgroundBrush", Color.FromRgb(241, 245, 249));
                UpdateBrush("BoardBackgroundBrush", Colors.White);
                UpdateBrush("CellBackgroundBrush", Color.FromRgb(226, 232, 240));
                Application.Current.Resources["DefaultForeground"] = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            }
            else
            {
                UpdateBrush("WindowBackgroundBrush", (Color)ColorConverter.ConvertFromString("#0F172A"));
                UpdateBrush("PanelBackgroundBrush", (Color)ColorConverter.ConvertFromString("#111827"));
                UpdateBrush("AccentPanelBackgroundBrush", (Color)ColorConverter.ConvertFromString("#1E293B"));
                UpdateBrush("BoardBackgroundBrush", (Color)ColorConverter.ConvertFromString("#0B1220"));
                UpdateBrush("CellBackgroundBrush", (Color)ColorConverter.ConvertFromString("#0F172A"));
                Application.Current.Resources["DefaultForeground"] = new SolidColorBrush(Colors.White);
            }

            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Background =
                    (Brush)Application.Current.Resources["WindowBackgroundBrush"];
            }

            Color primaryColor = Colors.DeepSkyBlue;
            if (SelectedPrimaryColor == "Tím") primaryColor = Colors.MediumPurple;
            else if (SelectedPrimaryColor == "Lục") primaryColor = Colors.MediumSeaGreen;

            Application.Current.Resources["Primary"] = new SolidColorBrush(primaryColor);

            MessageBox.Show("Cài đặt đã được áp dụng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static void UpdateBrush(string key, Color color)
        {
            Application.Current.Resources[key] = new SolidColorBrush(color);
        }

        private void Board_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BoardViewModel.IsPaused))
            {
                PauseButtonText = Board?.IsPaused == true ? "Tiếp tục" : "Tạm dừng";
            }
            else if (e.PropertyName == nameof(BoardViewModel.CurrentPlayer))
            {
                StatusMessage = $"Lượt hiện tại: {Board?.CurrentPlayer}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
