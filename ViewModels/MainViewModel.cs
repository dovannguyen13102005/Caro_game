using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Caro_game.Commands;
using Caro_game.Models;
using Microsoft.Win32;

namespace Caro_game.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const string DefaultDarkThemeLabel = "Dark (mặc định)";

        private static readonly Uri DarkThemeUri = new("Resources/Themes/DarkTheme.xaml", UriKind.Relative);
        private static readonly Uri LightThemeUri = new("Resources/Themes/LightTheme.xaml", UriKind.Relative);

        private string _firstPlayer;
        private BoardViewModel? _board;
        private bool _isAIEnabled;
        private string _selectedAIMode;
        private TimeOption _selectedTimeOption;
        private string _selectedTheme;
        private string _selectedPrimaryColor;
        private bool _isSoundEnabled;
        private bool _isGameActive;
        private bool _isGamePaused;
        private TimeSpan _remainingTime;
        private string _statusMessage;
        private DispatcherTimer? _gameTimer;
        private TimeSpan _configuredDuration = TimeSpan.Zero;

        // Thuộc tính cho cấu hình bảng
        public ObservableCollection<int> RowOptions { get; }
        public ObservableCollection<int> ColumnOptions { get; }
        public ObservableCollection<string> Players { get; }
        public ObservableCollection<string> AIModes { get; }
        public ObservableCollection<TimeOption> TimeOptions { get; }

        private int _selectedRows;
        public int SelectedRows
        {
            get => _selectedRows;
            set
            {
                if (_selectedRows != value)
                {
                    _selectedRows = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _selectedColumns;
        public int SelectedColumns
        {
            get => _selectedColumns;
            set
            {
                if (_selectedColumns != value)
                {
                    _selectedColumns = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FirstPlayer
        {
            get => _firstPlayer;
            set
            {
                if (_firstPlayer != value)
                {
                    _firstPlayer = value;
                    OnPropertyChanged();
                }
            }
        }

        public BoardViewModel? Board
        {
            get => _board;
            private set
            {
                if (_board != null)
                {
                    _board.GameEnded -= OnBoardGameEnded;
                }

                _board = value;

                if (_board != null)
                {
                    _board.GameEnded += OnBoardGameEnded;
                }

                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsAIEnabled
        {
            get => _isAIEnabled;
            set
            {
                if (_isAIEnabled != value)
                {
                    _isAIEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedAIMode
        {
            get => _selectedAIMode;
            set
            {
                if (_selectedAIMode != value)
                {
                    _selectedAIMode = value;
                    OnPropertyChanged();
                }
            }
        }

        // --- Cài đặt giao diện ---
        public ObservableCollection<string> Themes { get; } =
            new ObservableCollection<string> { DefaultDarkThemeLabel, "Light" };

        public ObservableCollection<string> PrimaryColors { get; } =
            new ObservableCollection<string> { "Xanh dương", "Tím", "Lục" };

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedPrimaryColor
        {
            get => _selectedPrimaryColor;
            set
            {
                if (_selectedPrimaryColor != value)
                {
                    _selectedPrimaryColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSoundEnabled
        {
            get => _isSoundEnabled;
            set
            {
                if (_isSoundEnabled != value)
                {
                    _isSoundEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public TimeOption SelectedTimeOption
        {
            get => _selectedTimeOption;
            set
            {
                if (value != null && _selectedTimeOption.Minutes != value.Minutes)
                {
                    _selectedTimeOption = value;
                    OnPropertyChanged();
                    if (!IsGameActive)
                    {
                        RemainingTime = value.Minutes > 0
                            ? TimeSpan.FromMinutes(value.Minutes)
                            : TimeSpan.Zero;
                    }
                    OnPropertyChanged(nameof(RemainingTimeDisplay));
                }
            }
        }

        public TimeSpan RemainingTime
        {
            get => _remainingTime;
            private set
            {
                if (_remainingTime != value)
                {
                    _remainingTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RemainingTimeDisplay));
                }
            }
        }

        public string RemainingTimeDisplay =>
            SelectedTimeOption.Minutes > 0
                ? RemainingTime.ToString(@"mm\:ss")
                : "Không giới hạn";

        public bool IsGameActive
        {
            get => _isGameActive;
            private set
            {
                if (_isGameActive != value)
                {
                    _isGameActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PauseButtonText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsGamePaused
        {
            get => _isGamePaused;
            private set
            {
                if (_isGamePaused != value)
                {
                    _isGamePaused = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PauseButtonText));
                }
            }
        }

        public string PauseButtonText => IsGamePaused ? "Tiếp tục" : "Tạm dừng";

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        // Commands
        public ICommand StartGameCommand { get; }
        public ICommand TogglePauseCommand { get; }
        public ICommand SaveGameCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        public MainViewModel()
        {
            RowOptions = new ObservableCollection<int> { 15, 20, 25, 30, 40, 50, 75, 100 };
            ColumnOptions = new ObservableCollection<int> { 18, 25, 30, 35, 40, 50, 75, 100 };
            Players = new ObservableCollection<string> { "X (Bạn)", "O" };
            AIModes = new ObservableCollection<string> { "Dễ", "Khó" };
            TimeOptions = new ObservableCollection<TimeOption>
            {
                new TimeOption(0, "Không giới hạn"),
                new TimeOption(5, "5 phút"),
                new TimeOption(10, "10 phút"),
                new TimeOption(15, "15 phút"),
                new TimeOption(20, "20 phút"),
                new TimeOption(30, "30 phút"),
                new TimeOption(45, "45 phút"),
                new TimeOption(60, "60 phút")
            };

            SelectedRows = 20;
            SelectedColumns = 35;
            FirstPlayer = "X (Bạn)";
            IsAIEnabled = true;
            SelectedAIMode = "Khó";

            SelectedTheme = DefaultDarkThemeLabel;
            SelectedPrimaryColor = "Xanh dương";
            IsSoundEnabled = true;
            _selectedTimeOption = TimeOptions[3]; // 15 phút mặc định
            RemainingTime = TimeSpan.FromMinutes(_selectedTimeOption.Minutes);
            StatusMessage = "Chưa bắt đầu";

            StartGameCommand = new RelayCommand(StartGame);
            TogglePauseCommand = new RelayCommand(_ => TogglePause(), _ => Board != null && IsGameActive);
            SaveGameCommand = new RelayCommand(_ => SaveCurrentGame(), _ => Board != null);
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        }

        private void StartGame(object? parameter)
        {
            int rows = SelectedRows;
            int cols = SelectedColumns;

            var board = new BoardViewModel(rows, cols, FirstPlayer)
            {
                IsAIEnabled = IsAIEnabled,
                AIMode = SelectedAIMode
            };

            Board = board;

            _configuredDuration = SelectedTimeOption.Minutes > 0
                ? TimeSpan.FromMinutes(SelectedTimeOption.Minutes)
                : TimeSpan.Zero;

            StartTimer();

            IsGameActive = true;
            IsGamePaused = false;
            board.IsPaused = false;
            StatusMessage = "Đang chơi";
        }

        private void TogglePause()
        {
            if (Board == null)
            {
                return;
            }

            if (!IsGamePaused)
            {
                IsGamePaused = true;
                Board.IsPaused = true;
                _gameTimer?.Stop();
                StatusMessage = "Đang tạm dừng";
            }
            else
            {
                if (SelectedTimeOption.Minutes > 0 && RemainingTime <= TimeSpan.Zero)
                {
                    return;
                }

                IsGamePaused = false;
                Board.IsPaused = false;
                if (_configuredDuration > TimeSpan.Zero)
                {
                    _gameTimer?.Start();
                }
                StatusMessage = "Đang chơi";
            }
        }

        private void StartTimer()
        {
            StopTimer();

            if (_configuredDuration > TimeSpan.Zero)
            {
                RemainingTime = _configuredDuration;
                _gameTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _gameTimer.Tick += OnGameTimerTick;
                _gameTimer.Start();
            }
            else
            {
                RemainingTime = TimeSpan.Zero;
            }
        }

        private void StopTimer()
        {
            if (_gameTimer != null)
            {
                _gameTimer.Stop();
                _gameTimer.Tick -= OnGameTimerTick;
                _gameTimer = null;
            }
        }

        private void OnGameTimerTick(object? sender, EventArgs e)
        {
            if (IsGamePaused)
            {
                return;
            }

            if (RemainingTime > TimeSpan.Zero)
            {
                RemainingTime -= TimeSpan.FromSeconds(1);
            }

            if (RemainingTime <= TimeSpan.Zero)
            {
                RemainingTime = TimeSpan.Zero;
                StopTimer();
                HandleTimeExpired();
            }
        }

        private void HandleTimeExpired()
        {
            Board?.PauseBoard();
            IsGameActive = false;
            IsGamePaused = false;
            StatusMessage = "Hết thời gian";
            MessageBox.Show("Hết thời gian! Ván đấu đã kết thúc.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveCurrentGame()
        {
            if (Board == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Caro Save|*.json",
                FileName = $"caro_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                var state = new GameState
                {
                    Rows = Board.Rows,
                    Columns = Board.Columns,
                    FirstPlayer = Board.InitialPlayer,
                    CurrentPlayer = Board.CurrentPlayer,
                    IsAIEnabled = Board.IsAIEnabled,
                    AIMode = Board.AIMode,
                    TimeLimitMinutes = SelectedTimeOption.Minutes,
                    RemainingSeconds = SelectedTimeOption.Minutes > 0 ? (int?)Math.Ceiling(RemainingTime.TotalSeconds) : null,
                    IsPaused = IsGamePaused,
                    SavedAt = DateTime.Now,
                    Cells = Board.Cells.Select(c => new CellState
                    {
                        Row = c.Row,
                        Col = c.Col,
                        Value = c.Value,
                        IsWinningCell = c.IsWinningCell
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show("Ván đấu đã được lưu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveSettings()
        {
            ApplyTheme();
            ApplyPrimaryColor();
            MessageBox.Show("Cài đặt đã được áp dụng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyTheme()
        {
            var themeUri = SelectedTheme == "Light" ? LightThemeUri : DarkThemeUri;
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            ResourceDictionary? currentTheme = null;

            foreach (var dictionary in dictionaries)
            {
                if (dictionary.Source != null && dictionary.Source.OriginalString.Contains("Resources/Themes"))
                {
                    currentTheme = dictionary;
                    break;
                }
            }

            if (currentTheme != null && currentTheme.Source == themeUri)
            {
                return;
            }

            if (currentTheme != null)
            {
                dictionaries.Remove(currentTheme);
            }

            dictionaries.Add(new ResourceDictionary { Source = themeUri });
        }

        private void ApplyPrimaryColor()
        {
            Color primaryColor = Colors.DeepSkyBlue;
            if (SelectedPrimaryColor == "Tím")
            {
                primaryColor = Colors.MediumPurple;
            }
            else if (SelectedPrimaryColor == "Lục")
            {
                primaryColor = Colors.MediumSeaGreen;
            }

            Application.Current.Resources["Primary"] = new SolidColorBrush(primaryColor);
        }

        private void OnBoardGameEnded(object? sender, GameEndedEventArgs e)
        {
            StopTimer();

            if (e.HasWinner)
            {
                StatusMessage = $"Người chơi {e.Winner} thắng!";
            }

            if (e.PlayAgain)
            {
                IsGameActive = true;
                IsGamePaused = false;
                Board!.IsPaused = false;
                StartTimer();
                StatusMessage = "Đang chơi";
            }
            else
            {
                IsGameActive = false;
                IsGamePaused = false;
                if (_configuredDuration > TimeSpan.Zero)
                {
                    RemainingTime = _configuredDuration;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
