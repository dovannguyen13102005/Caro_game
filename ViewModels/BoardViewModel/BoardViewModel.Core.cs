using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Caro_game.Models;

namespace Caro_game.ViewModels
{
    public partial class BoardViewModel : BaseViewModel
    {
        private int _rows;
        private int _columns;
        private readonly bool _allowDynamicResize;
        private readonly Random _random = new();

        public int Rows
        {
            get => _rows;
            private set
            {
                if (_rows != value)
                {
                    _rows = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Columns
        {
            get => _columns;
            private set
            {
                if (_columns != value)
                {
                    _columns = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<Cell> Cells { get; }

        private readonly Dictionary<(int Row, int Col), Cell> _cellLookup;
        private readonly HashSet<(int Row, int Col)> _candidatePositions;
        private readonly object _candidateLock = new();
        private readonly string _initialPlayer;
        private string _currentPlayer;
        private EngineClient? _engine;

        public string CurrentPlayer
        {
            get => _currentPlayer;
            set
            {
                if (_currentPlayer != value)
                {
                    _currentPlayer = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isAIEnabled;
        public bool IsAIEnabled
        {
            get => _isAIEnabled;
            set
            {
                if (_isAIEnabled != value)
                {
                    _isAIEnabled = value;
                    OnPropertyChanged();

                    if (_isAIEnabled && AIMode == "Chuyên nghiệp")
                    {
                        TryInitializeMasterEngine();
                    }
                    else if (!_isAIEnabled)
                    {
                        DisposeEngine();
                    }
                }
            }
        }

        private string _aiMode = "Dễ";
        public string AIMode
        {
            get => _aiMode;
            set
            {
                if (_aiMode != value)
                {
                    _aiMode = value;
                    OnPropertyChanged();

                    if (_aiMode == "Chuyên nghiệp" && IsAIEnabled)
                    {
                        TryInitializeMasterEngine();
                    }
                    else
                    {
                        DisposeEngine();
                    }
                }
            }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    OnPropertyChanged();
                }
            }
        }

        public string InitialPlayer => _initialPlayer;

        public event EventHandler<GameEndedEventArgs>? GameEnded;

        public BoardViewModel(int rows, int columns, string firstPlayer, string aiMode = "Dễ", bool allowDynamicResize = false)
        {
            Rows = rows;
            Columns = columns;
            _allowDynamicResize = allowDynamicResize;
            AIMode = aiMode;
            CurrentPlayer = firstPlayer.StartsWith("X", StringComparison.OrdinalIgnoreCase) ? "X" : "O";

            _initialPlayer = CurrentPlayer;
            Cells = new ObservableCollection<Cell>();
            _cellLookup = new Dictionary<(int, int), Cell>(rows * columns);
            _candidatePositions = new HashSet<(int, int)>();

            for (int i = 0; i < rows * columns; i++)
            {
                int r = i / columns;
                int c = i % columns;
                var cell = new Cell(r, c, this);
                Cells.Add(cell);
                _cellLookup[(r, c)] = cell;
            }

            if (AIMode == "Chuyên nghiệp" && IsAIEnabled)
            {
                TryInitializeMasterEngine();
            }
        }

        public void MakeMove(Cell cell)
        {
            if (IsPaused || !string.IsNullOrEmpty(cell.Value))
                return;

            var movingPlayer = CurrentPlayer;
            cell.Value = movingPlayer;
            UpdateCandidatePositions(cell.Row, cell.Col);

            if (CheckWin(cell.Row, cell.Col, movingPlayer))
            {
                HighlightWinningCells(cell.Row, cell.Col, movingPlayer);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new Views.WinDialog($"Người chơi {movingPlayer} thắng!")
                    {
                        Owner = Application.Current.MainWindow
                    };

                    dialog.ShowDialog();

                    GameEnded?.Invoke(this, new GameEndedEventArgs(movingPlayer, dialog.IsPlayAgain, true));

                    if (dialog.IsPlayAgain)
                    {
                        ResetBoard();
                    }
                    else
                    {
                        DisposeEngine();
                        Application.Current.Shutdown();
                    }
                });

                return;
            }

            if (Cells.All(c => !string.IsNullOrEmpty(c.Value)))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Hòa cờ! Bàn đã đầy mà không có người thắng.",
                        "Kết thúc ván", MessageBoxButton.OK, MessageBoxImage.Information);

                    GameEnded?.Invoke(this, new GameEndedEventArgs(null, false, false));
                });
                return;
            }

            CurrentPlayer = movingPlayer == "X" ? "O" : "X";

            ExpandBoardIfNeeded(cell);

            if (IsAIEnabled && CurrentPlayer == "O")
            {
                if (AIMode == "Chuyên nghiệp" && _engine != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainVM = Application.Current.MainWindow?.DataContext as MainViewModel;
                        mainVM?.SetStatus("AI đang suy nghĩ...");
                    });

                    Task.Run(() =>
                    {
                        try
                        {
                            string aiMove = _engine.Turn(cell.Col, cell.Row);
                            PlaceAiIfValid(aiMove);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var mainVM = Application.Current.MainWindow?.DataContext as MainViewModel;
                                mainVM?.SetStatus("Đang chơi");
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"AI engine error: {ex.Message}", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                            DisposeEngine();
                        }
                    });
                }
                else
                {
                    Task.Run(AIMove);
                }
            }
        }

        public void LoadFromState(GameState state)
        {
            if (state.Rows != Rows || state.Columns != Columns)
            {
                throw new ArgumentException("Kích thước bàn không khớp với trạng thái đã lưu.");
            }

            foreach (var cell in Cells)
            {
                cell.Value = string.Empty;
                cell.IsWinningCell = false;
            }

            if (state.Cells != null)
            {
                foreach (var cellState in state.Cells)
                {
                    if (_cellLookup.TryGetValue((cellState.Row, cellState.Col), out var cell))
                    {
                        cell.Value = cellState.Value ?? string.Empty;
                        cell.IsWinningCell = cellState.IsWinningCell;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(state.CurrentPlayer))
            {
                CurrentPlayer = state.CurrentPlayer!;
            }

            RebuildCandidatePositions();

            IsPaused = state.IsPaused;
        }

        public void ResetBoard()
        {
            foreach (var cell in Cells)
            {
                cell.Value = string.Empty;
                cell.IsWinningCell = false;
            }

            lock (_candidateLock) _candidatePositions.Clear();

            CurrentPlayer = _initialPlayer;
            IsPaused = false;

            if (AIMode == "Chuyên nghiệp" && IsAIEnabled)
            {
                TryInitializeMasterEngine();
            }
        }

        public void PauseBoard() => IsPaused = true;
    }
}
