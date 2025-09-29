using System;
using System.Collections.Generic;
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

        private string _firstPlayer = string.Empty;
        private BoardViewModel? _board;
        private bool _isAIEnabled;
        private string _selectedAIMode = string.Empty;
        private TimeOption _selectedTimeOption = null!;
        private string _selectedTheme = DefaultDarkThemeLabel;
        private string _selectedPrimaryColor = string.Empty;
        private bool _isSoundEnabled;
        private bool _isGameActive;
        private bool _isGamePaused;
        private TimeSpan _remainingTime;
        private string _statusMessage = string.Empty;
        private DispatcherTimer? _gameTimer;
        private TimeSpan _configuredDuration = TimeSpan.Zero;
        private RuleOption _selectedRuleOption = null!;
        private string _forbiddenCellsInput = string.Empty;
        private string _handicapInput = string.Empty;
        private bool _isBoardExpansionEnabled;
        private int _expansionThreshold = 2;
        private int _expansionMaxSize = 60;

        // Thuộc tính cho cấu hình bảng
        public ObservableCollection<int> RowOptions { get; }
        public ObservableCollection<int> ColumnOptions { get; }
        public ObservableCollection<string> Players { get; }
        public ObservableCollection<string> AIModes { get; }
        public ObservableCollection<TimeOption> TimeOptions { get; }
        public ObservableCollection<RuleOption> RuleOptions { get; }

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
                    OnPropertyChanged(nameof(IsMasterMode));
                    OnPropertyChanged(nameof(CanEditAdvancedOptions));
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
                    OnPropertyChanged(nameof(IsMasterMode));
                    OnPropertyChanged(nameof(CanEditAdvancedOptions));
                }
            }
        }

        public bool IsMasterMode => IsAIEnabled && SelectedAIMode == "Bậc thầy";

        public bool CanEditAdvancedOptions => !IsMasterMode;

        public RuleOption SelectedRuleOption
        {
            get => _selectedRuleOption;
            set
            {
                if (value != null && _selectedRuleOption != value)
                {
                    _selectedRuleOption = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ForbiddenCellsInput
        {
            get => _forbiddenCellsInput;
            set
            {
                if (_forbiddenCellsInput != value)
                {
                    _forbiddenCellsInput = value;
                    OnPropertyChanged();
                }
            }
        }

        public string HandicapInput
        {
            get => _handicapInput;
            set
            {
                if (_handicapInput != value)
                {
                    _handicapInput = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBoardExpansionEnabled
        {
            get => _isBoardExpansionEnabled;
            set
            {
                if (_isBoardExpansionEnabled != value)
                {
                    _isBoardExpansionEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ExpansionThreshold
        {
            get => _expansionThreshold;
            set
            {
                int sanitized = Math.Max(0, value);
                if (_expansionThreshold != sanitized)
                {
                    _expansionThreshold = sanitized;
                    OnPropertyChanged();
                }
            }
        }

        public int ExpansionMaxSize
        {
            get => _expansionMaxSize;
            set
            {
                int sanitized = Math.Max(5, value);
                if (_expansionMaxSize != sanitized)
                {
                    _expansionMaxSize = sanitized;
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
        public ICommand LoadGameCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        public MainViewModel()
        {
            RowOptions = new ObservableCollection<int> { 15, 20, 25, 30, 40, 50, 75, 100 };
            ColumnOptions = new ObservableCollection<int> { 15, 20, 25, 30, 40, 50, 75, 100 };
            Players = new ObservableCollection<string> { "X (Bạn)", "O" };
            AIModes = new ObservableCollection<string> { "Dễ", "Khó", "Bậc thầy" };
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
            RuleOptions = new ObservableCollection<RuleOption>
            {
                new RuleOption(GameRule.Freestyle, "Freestyle (tự do)"),
                new RuleOption(GameRule.Standard, "Standard Gomoku"),
                new RuleOption(GameRule.Renju, "Renju"),
                new RuleOption(GameRule.Swap, "Swap"),
                new RuleOption(GameRule.Swap2, "Swap2")
            };

            _selectedRuleOption = RuleOptions[0];

            SelectedRows = 40;
            SelectedColumns = 40;
            FirstPlayer = "X (Bạn)";
            IsAIEnabled = true;
            SelectedAIMode = "Khó";

            SelectedTheme = DefaultDarkThemeLabel;
            SelectedPrimaryColor = "Xanh dương";
            IsSoundEnabled = true;
            _selectedTimeOption = TimeOptions[3]; // 15 phút mặc định
            RemainingTime = TimeSpan.FromMinutes(_selectedTimeOption.Minutes);
            StatusMessage = "Chưa bắt đầu";

            _forbiddenCellsInput = string.Empty;
            _handicapInput = string.Empty;
            _isBoardExpansionEnabled = false;
            _expansionThreshold = 2;
            _expansionMaxSize = 60;

            StartGameCommand = new RelayCommand(StartGame);
            TogglePauseCommand = new RelayCommand(_ => TogglePause(), _ => Board != null && IsGameActive);
            SaveGameCommand = new RelayCommand(_ => SaveCurrentGame(), _ => Board != null);
            LoadGameCommand = new RelayCommand(_ => LoadSavedGame());
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        }

        private void StartGame(object? parameter)
        {
            int rows = SelectedRows;
            int cols = SelectedColumns;

            bool masterMode = IsMasterMode;

            if (masterMode)
            {
                int[] supported = { 15, 20, 30 };

                if (!supported.Contains(rows) || rows != cols)
                {
                    rows = 20;
                    cols = 20;
                    MessageBox.Show(
                        "AI Bậc thầy chỉ hỗ trợ các bàn 15×15, 20×20 hoặc 30×30.\n" +
                        "Kích thước đã được đặt về 20×20.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                if (SelectedRuleOption.Rule is not GameRule.Freestyle and not GameRule.Standard)
                {
                    MessageBox.Show("Chế độ Bậc thầy chỉ hỗ trợ luật Freestyle hoặc Standard.",
                        "Bậc thầy", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(ForbiddenCellsInput) ||
                    IsBoardExpansionEnabled ||
                    !string.IsNullOrWhiteSpace(HandicapInput))
                {
                    MessageBox.Show("Bậc thầy không hỗ trợ ô cấm, bàn mở rộng hoặc khai cuộc sẵn.",
                        "Bậc thầy", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (!TryParseCoordinates(ForbiddenCellsInput, rows, cols, out var forbiddenCells, out var coordinateError))
            {
                MessageBox.Show(coordinateError!, "Ô cấm", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParsePlacements(HandicapInput, rows, cols, out var placements, out var placementError))
            {
                MessageBox.Show(placementError!, "Khai cuộc", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string firstPlayerSymbol = FirstPlayer.StartsWith("O", StringComparison.OrdinalIgnoreCase) ? "O" : "X";
            var rule = SelectedRuleOption?.Rule ?? GameRule.Freestyle;

            var setup = new GameSetup(rows, cols, rule, firstPlayerSymbol)
            {
                AllowExpansion = IsBoardExpansionEnabled && !masterMode,
                ExpansionThreshold = ExpansionThreshold,
                MaxRows = Math.Max(Math.Max(rows, cols), ExpansionMaxSize),
                MaxColumns = Math.Max(Math.Max(rows, cols), ExpansionMaxSize)
            };

            foreach (var coord in forbiddenCells)
            {
                setup.ForbiddenCells.Add(coord);
            }

            foreach (var placement in placements)
            {
                setup.InitialPlacements.Add(placement);
                setup.ForbiddenCells.Remove((placement.Row, placement.Col));
            }

            var board = new BoardViewModel(setup, SelectedAIMode)
            {
                IsAIEnabled = IsAIEnabled
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

        private bool TryParseCoordinates(string input, int rows, int cols, out List<(int Row, int Col)> coordinates, out string? error)
        {
            coordinates = new List<(int, int)>();
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return true;
            }

            var tokens = input.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new HashSet<(int, int)>();

            foreach (var token in tokens)
            {
                var trimmed = token.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                var parts = trimmed.Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int row) || !int.TryParse(parts[1], out int col))
                {
                    error = $"Ô cấm không hợp lệ: \"{trimmed}\". Định dạng đúng: hàng,cột (0-based).";
                    return false;
                }

                if (row < 0 || row >= rows || col < 0 || col >= cols)
                {
                    error = $"Ô cấm {trimmed} nằm ngoài bàn {rows}×{cols}.";
                    return false;
                }

                if (seen.Add((row, col)))
                {
                    coordinates.Add((row, col));
                }
            }

            return true;
        }

        private bool TryParsePlacements(string input, int rows, int cols, out List<StonePlacement> placements, out string? error)
        {
            placements = new List<StonePlacement>();
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return true;
            }

            var tokens = input.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new HashSet<(int, int)>();

            foreach (var token in tokens)
            {
                var trimmed = token.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                var parts = trimmed.Split('@');
                if (parts.Length != 2)
                {
                    error = $"Khai cuộc không hợp lệ: \"{trimmed}\". Định dạng: X@hàng,cột.";
                    return false;
                }

                var playerToken = parts[0].Trim().ToUpperInvariant();
                if (playerToken != "X" && playerToken != "O")
                {
                    error = $"Khai cuộc \"{trimmed}\" chứa quân không hợp lệ. Chỉ dùng X hoặc O.";
                    return false;
                }

                var coordParts = parts[1].Split(',');
                if (coordParts.Length != 2 || !int.TryParse(coordParts[0], out int row) || !int.TryParse(coordParts[1], out int col))
                {
                    error = $"Khai cuộc \"{trimmed}\" có tọa độ không hợp lệ.";
                    return false;
                }

                if (row < 0 || row >= rows || col < 0 || col >= cols)
                {
                    error = $"Khai cuộc \"{trimmed}\" nằm ngoài bàn {rows}×{cols}.";
                    return false;
                }

                if (!seen.Add((row, col)))
                {
                    error = $"Khai cuộc trùng ô tại ({row},{col}).";
                    return false;
                }

                placements.Add(new StonePlacement
                {
                    Row = row,
                    Col = col,
                    Player = playerToken
                });
            }

            return true;
        }

        private static string BuildCoordinateString(IEnumerable<CoordinateState>? coordinates)
        {
            if (coordinates == null)
            {
                return string.Empty;
            }

            var items = coordinates.ToList();
            return items.Count == 0
                ? string.Empty
                : string.Join("; ", items.Select(c => $"{c.Row},{c.Col}"));
        }

        private static string BuildPlacementString(IEnumerable<StonePlacementState>? placements)
        {
            if (placements == null)
            {
                return string.Empty;
            }

            var items = placements.ToList();
            return items.Count == 0
                ? string.Empty
                : string.Join("; ", items.Select(p => $"{(string.IsNullOrWhiteSpace(p.Player) ? "X" : p.Player)}@{p.Row},{p.Col}"));
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
                    }).ToList(),
                    Rule = Board.Setup.Rule,
                    ForbiddenCells = Board.Setup.ForbiddenCells
                        .Select(fc => new CoordinateState { Row = fc.Row, Col = fc.Col })
                        .ToList(),
                    InitialStones = Board.Setup.InitialPlacements
                        .Select(p => new StonePlacementState { Row = p.Row, Col = p.Col, Player = p.Player })
                        .ToList(),
                    AllowExpansion = Board.Setup.AllowExpansion,
                    ExpansionThreshold = Board.Setup.ExpansionThreshold,
                    MaxRows = Board.Setup.MaxRows,
                    MaxColumns = Board.Setup.MaxColumns,
                    InitialRows = Board.Setup.InitialRows,
                    InitialColumns = Board.Setup.InitialColumns
                };

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show("Ván đấu đã được lưu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadSavedGame()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Caro Save|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var state = JsonSerializer.Deserialize<GameState>(json);

                    if (state == null)
                    {
                        throw new InvalidOperationException("Không đọc được dữ liệu từ tệp đã chọn.");
                    }

                    ApplyGameState(state);

                    MessageBox.Show("Đã tải ván đấu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể mở tệp đã lưu.\nChi tiết: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ApplyGameState(GameState state)
        {
            StopTimer();

            Board = null;
            IsGameActive = false;
            IsGamePaused = false;

            SelectedRows = state.Rows;
            SelectedColumns = state.Columns;
            FirstPlayer = state.FirstPlayer == "O" ? "O" : "X (Bạn)";

            IsAIEnabled = state.IsAIEnabled;
            var targetMode = string.IsNullOrWhiteSpace(state.AIMode) ? "Dễ" : state.AIMode!;
            SelectedAIMode = targetMode;

            bool masterModeRestored = state.IsAIEnabled && targetMode == "Bậc thầy";
            var boardAIMode = masterModeRestored ? "Khó" : targetMode;

            var setup = new GameSetup(state.Rows, state.Columns, state.Rule, state.FirstPlayer ?? "X")
            {
                AllowExpansion = state.AllowExpansion,
                ExpansionThreshold = state.ExpansionThreshold,
                MaxRows = state.MaxRows > 0 ? state.MaxRows : Math.Max(state.Rows, 60),
                MaxColumns = state.MaxColumns > 0 ? state.MaxColumns : Math.Max(state.Columns, 60)
            };

            if (state.InitialRows > 0)
            {
                setup.InitialRows = state.InitialRows;
            }

            if (state.InitialColumns > 0)
            {
                setup.InitialColumns = state.InitialColumns;
            }

            if (state.ForbiddenCells != null)
            {
                foreach (var cell in state.ForbiddenCells)
                {
                    setup.ForbiddenCells.Add((cell.Row, cell.Col));
                }
            }

            if (state.InitialStones != null)
            {
                foreach (var stone in state.InitialStones)
                {
                    setup.InitialPlacements.Add(new StonePlacement
                    {
                        Row = stone.Row,
                        Col = stone.Col,
                        Player = stone.Player
                    });
                }
            }

            var board = new BoardViewModel(setup, boardAIMode)
            {
                IsAIEnabled = masterModeRestored ? false : state.IsAIEnabled
            };

            board.LoadFromState(state);

            Board = board;

            SelectedRuleOption = RuleOptions.FirstOrDefault(r => r.Rule == state.Rule) ?? RuleOptions[0];
            ForbiddenCellsInput = BuildCoordinateString(state.ForbiddenCells);
            HandicapInput = BuildPlacementString(state.InitialStones);
            IsBoardExpansionEnabled = state.AllowExpansion;
            ExpansionThreshold = state.ExpansionThreshold;
            if (state.MaxRows > 0 || state.MaxColumns > 0)
            {
                ExpansionMaxSize = Math.Max(Math.Max(state.MaxRows, state.MaxColumns), ExpansionMaxSize);
            }

            if (masterModeRestored)
            {
                SelectedAIMode = "Khó";
                IsAIEnabled = false;
                MessageBox.Show("Không thể tiếp tục chế độ AI Bậc thầy cho ván đã lưu. Chế độ đã được chuyển về Khó.",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var option = EnsureTimeOption(state.TimeLimitMinutes);
            SelectedTimeOption = option;

            _configuredDuration = state.TimeLimitMinutes > 0
                ? TimeSpan.FromMinutes(state.TimeLimitMinutes)
                : TimeSpan.Zero;

            if (state.TimeLimitMinutes > 0)
            {
                RemainingTime = state.RemainingSeconds.HasValue
                    ? TimeSpan.FromSeconds(Math.Max(0, state.RemainingSeconds.Value))
                    : _configuredDuration;
            }
            else
            {
                RemainingTime = TimeSpan.Zero;
            }

            bool hasWinner = state.Cells?.Any(c => c.IsWinningCell) == true;
            IsGameActive = !hasWinner;
            IsGamePaused = state.IsPaused && !hasWinner;

            if (Board != null)
            {
                Board.IsPaused = IsGamePaused || hasWinner;
            }

            if (_configuredDuration > TimeSpan.Zero && !IsGamePaused && !hasWinner)
            {
                _gameTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _gameTimer.Tick += OnGameTimerTick;
                _gameTimer.Start();
            }

            StatusMessage = hasWinner
                ? "Ván đấu đã kết thúc."
                : IsGamePaused ? "Đang tạm dừng" : "Đang chơi";

            CommandManager.InvalidateRequerySuggested();
        }

        private TimeOption EnsureTimeOption(int minutes)
        {
            var existing = TimeOptions.FirstOrDefault(t => t.Minutes == minutes);
            if (existing != null)
            {
                return existing;
            }

            var label = minutes > 0 ? $"{minutes} phút" : "Không giới hạn";
            var option = new TimeOption(minutes, label + " (tải)");
            TimeOptions.Add(option);
            return option;
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

                // 👉 Thêm dòng này để giải phóng Rapfi engine
                Board?.DisposeEngine();

                if (_configuredDuration > TimeSpan.Zero)
                {
                    RemainingTime = _configuredDuration;
                }
            }
        }
        public void SetStatus(string message)
        {
            // An toàn cho UI thread
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
                StatusMessage = message;   // setter private nhưng gọi trong chính class nên OK
            else
                Application.Current?.Dispatcher?.Invoke(() => StatusMessage = message);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
