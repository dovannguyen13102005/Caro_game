using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using Caro_game.Commands;
using Caro_game.Models;

namespace Caro_game.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private const string DefaultDarkThemeLabel = "Dark (mặc định)";

        protected static readonly Uri DarkThemeUri = new("Resources/Themes/DarkTheme.xaml", UriKind.Relative);
        protected static readonly Uri LightThemeUri = new("Resources/Themes/LightTheme.xaml", UriKind.Relative);

        private string _firstPlayer;
        private BoardViewModel? _board;
        private bool _isAIEnabled;
        private string _selectedAILevel = string.Empty;
        private TimeOption _selectedTimeOption;
        private string _selectedTheme = DefaultDarkThemeLabel;
        private bool _isSoundEnabled;
        private bool _isGameActive;
        private bool _isGamePaused;
        private TimeSpan _remainingTime;
        private string _statusMessage = string.Empty;
        private DispatcherTimer? _gameTimer;
        private TimeSpan _configuredDuration = TimeSpan.Zero;

        public ObservableCollection<string> Players { get; }
        public ObservableCollection<string> AILevels { get; }
        public ObservableCollection<TimeOption> TimeOptions { get; }
        public ObservableCollection<string> Themes { get; } =
            new ObservableCollection<string> { DefaultDarkThemeLabel, "Light" };

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

        public string SelectedAILevel
        {
            get => _selectedAILevel;
            set
            {
                if (_selectedAILevel != value)
                {
                    _selectedAILevel = value;
                    OnPropertyChanged();
                }
            }
        }

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

        public ICommand StartGameCommand { get; }
        public ICommand TogglePauseCommand { get; }
        public ICommand SaveGameCommand { get; }
        public ICommand LoadGameCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        public MainViewModel()
        {
            Players = new ObservableCollection<string> { "X (Bạn)", "O" };
            AILevels = new ObservableCollection<string> { "Dễ", "Khó", "Chuyên nghiệp" };
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

            FirstPlayer = "X (Bạn)";
            IsAIEnabled = true;
            SelectedAILevel = "Khó";
            SelectedTheme = DefaultDarkThemeLabel;
            IsSoundEnabled = true;
            _selectedTimeOption = TimeOptions[3];
            RemainingTime = TimeSpan.FromMinutes(_selectedTimeOption.Minutes);
            StatusMessage = "Chưa bắt đầu";

            StartGameCommand = new RelayCommand(StartGame);
            TogglePauseCommand = new RelayCommand(_ => TogglePause(), _ => Board != null && IsGameActive);
            SaveGameCommand = new RelayCommand(_ => SaveCurrentGame(), _ => Board != null);
            LoadGameCommand = new RelayCommand(_ => LoadSavedGame());
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
