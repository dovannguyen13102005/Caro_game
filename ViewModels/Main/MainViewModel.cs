using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Caro_game.Commands;
using Caro_game.Models;

namespace Caro_game.ViewModels;

public partial class MainViewModel : INotifyPropertyChanged
{
    private const string DefaultDarkThemeLabel = "Dark (mặc định)";

    private static readonly Uri DarkThemeUri = new("Resources/Themes/DarkTheme.xaml", UriKind.Relative);
    private static readonly Uri LightThemeUri = new("Resources/Themes/LightTheme.xaml", UriKind.Relative);

    private string _selectedFirstMoveOption;
    private BoardViewModel? _board;
    private bool _isAIEnabled;
    private string _selectedAIMode;
    private TimeOption _selectedTimeOption;
    private string _selectedTheme;
    private bool _isSoundEnabled;
    private bool _isGameActive;
    private bool _isGamePaused;
    private TimeSpan _remainingTime;
    private string _statusMessage;
    private DispatcherTimer? _gameTimer;
    private TimeSpan _configuredDuration = TimeSpan.Zero;

    public ObservableCollection<string> FirstMoveOptions { get; }
    public ObservableCollection<string> AIModes { get; }
    public ObservableCollection<string> GameRules { get; } =
        new ObservableCollection<string> { "Freestyle", "Standard", "Renju" };
    public ObservableCollection<TimeOption> TimeOptions { get; }

    public ObservableCollection<string> Themes { get; } =
        new ObservableCollection<string> { DefaultDarkThemeLabel, "Light" };

    public string SelectedFirstMoveOption
    {
        get => _selectedFirstMoveOption;
        set
        {
            if (_selectedFirstMoveOption != value)
            {
                _selectedFirstMoveOption = value;
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
                OnPropertyChanged(nameof(IsProfessionalModeSelected));

            }
        }
    }

    private string _selectedGameRule = "Freestyle";
    public string SelectedGameRule
    {
        get => _selectedGameRule;
        set
        {
            if (_selectedGameRule != value)
            {
                _selectedGameRule = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsProfessionalModeSelected => SelectedAIMode == "Chuyên nghiệp";

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
        FirstMoveOptions = new ObservableCollection<string>
        {
            "Bạn đi trước (X)",
            "Máy đi trước (X)",
            "Ngẫu nhiên"
        };
        AIModes = new ObservableCollection<string> { "Dễ", "Khó", "Chuyên nghiệp" };
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

        SelectedFirstMoveOption = "Ngẫu nhiên";
        IsAIEnabled = true;
        SelectedAIMode = "Khó";

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

    public void SetStatus(string message)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            StatusMessage = message;
        }
        else
        {
            Application.Current?.Dispatcher?.Invoke(() => StatusMessage = message);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private FirstMoveOption ResolveFirstMoveOption() => SelectedFirstMoveOption switch
    {
        string s when s.StartsWith("Máy", StringComparison.OrdinalIgnoreCase) => FirstMoveOption.ComputerFirst,
        string s when s.StartsWith("Ngẫu", StringComparison.OrdinalIgnoreCase) => FirstMoveOption.Random,
        _ => FirstMoveOption.PlayerFirst
    };

    private string BuildStartStatus(BoardViewModel board)
    {
        if (IsAIEnabled)
        {
            return board.HumanPiece == "X"
                ? "Đang chơi - Bạn đi trước"
                : "Đang chơi - Máy đi trước";
        }

        return board.HumanPiece == "X"
            ? "Đang chơi - X đi trước"
            : "Đang chơi - O đi trước";
    }

    private string GetDisplayLabelForOption(FirstMoveOption option) => option switch
    {
        FirstMoveOption.ComputerFirst => "Máy đi trước (X)",
        FirstMoveOption.Random => "Ngẫu nhiên",
        _ => "Bạn đi trước (X)"
    };

    private FirstMoveOption ParseSavedFirstMoveOption(string? value, string? humanPiece)
    {
        if (Enum.TryParse<FirstMoveOption>(value, true, out var option))
        {
            return option;
        }

        if (string.Equals(humanPiece, "X", StringComparison.OrdinalIgnoreCase))
        {
            return FirstMoveOption.PlayerFirst;
        }

        if (string.Equals(humanPiece, "O", StringComparison.OrdinalIgnoreCase))
        {
            return FirstMoveOption.ComputerFirst;
        }

        return FirstMoveOption.PlayerFirst;
    }

    private GameRule ParseSavedGameRule(string? ruleValue)
    {
        if (Enum.TryParse<GameRule>(ruleValue, true, out var rule))
        {
            return rule;
        }

        return GameRule.Freestyle;
    }

    private GameRule GetSelectedGameRule() => SelectedGameRule switch
    {
        "Standard" => GameRule.Standard,
        "Renju" => GameRule.Renju,
        _ => GameRule.Freestyle
    };
}
