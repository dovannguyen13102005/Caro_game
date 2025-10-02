using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Caro_game;
using Caro_game.Models;

namespace Caro_game.ViewModels;

public partial class BoardViewModel : BaseViewModel
{
    private int _rows;
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

    private int _columns;
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
    private readonly GameRuleType _rule;
    private readonly string _humanSymbol;
    private readonly string _aiSymbol;
    private static readonly TimeSpan AiThinkingDelay = TimeSpan.FromMilliseconds(600);
    private Cell? _lastMoveCell;
    private Cell? _lastHumanMoveCell;
    private string? _lastMovePlayer;
    private MoveSnapshot? _pendingProfessionalValidation;

    private string _currentPlayer;
    public string CurrentPlayer
    {
        get => _currentPlayer;
        private set
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
                    TryInitializeProfessionalEngine();
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

                if (_aiMode == "Chuyên nghiệp")
                {
                    TryInitializeProfessionalEngine();
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
    public string HumanSymbol => _humanSymbol;
    public string AISymbol => _aiSymbol;
    public GameRuleType Rule => _rule;
    public (int Row, int Col)? LastMovePosition => _lastMoveCell != null
        ? (_lastMoveCell.Row, _lastMoveCell.Col)
        : null;
    public (int Row, int Col)? LastHumanMovePosition => _lastHumanMoveCell != null
        ? (_lastHumanMoveCell.Row, _lastHumanMoveCell.Col)
        : null;
    public string? LastMovePlayer => _lastMovePlayer;

    private EngineClient? _engine;

    public event EventHandler<GameEndedEventArgs>? GameEnded;

    public BoardViewModel(int rows, int columns, string firstPlayer, string aiMode = "Dễ", string? humanSymbol = null, GameRuleType rule = GameRuleType.Freestyle)
    {
        Rows = rows;
        Columns = columns;
        AIMode = aiMode;
        CurrentPlayer = firstPlayer.Equals("O", StringComparison.OrdinalIgnoreCase) ? "O" : "X";

        _initialPlayer = CurrentPlayer;
        _rule = rule;
        _humanSymbol = string.IsNullOrWhiteSpace(humanSymbol)
            ? CurrentPlayer
            : (humanSymbol.Equals("O", StringComparison.OrdinalIgnoreCase) ? "O" : "X");
        _aiSymbol = _humanSymbol == "X" ? "O" : "X";
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

        if (AIMode == "Chuyên nghiệp")
        {
            TryInitializeProfessionalEngine();
        }
    }

    private sealed class MoveSnapshot
    {
        public MoveSnapshot(Cell cell,
            string? previousValue,
            bool previousIsLastMove,
            Cell? previousLastMoveCell,
            string? previousLastMovePlayer,
            Cell? previousLastHumanMoveCell,
            string previousCurrentPlayer)
        {
            Cell = cell;
            PreviousValue = previousValue;
            PreviousIsLastMove = previousIsLastMove;
            PreviousLastMoveCell = previousLastMoveCell;
            PreviousLastMovePlayer = previousLastMovePlayer;
            PreviousLastHumanMoveCell = previousLastHumanMoveCell;
            PreviousCurrentPlayer = previousCurrentPlayer;
        }

        public Cell Cell { get; }
        public string? PreviousValue { get; }
        public bool PreviousIsLastMove { get; }
        public Cell? PreviousLastMoveCell { get; }
        public string? PreviousLastMovePlayer { get; }
        public Cell? PreviousLastHumanMoveCell { get; }
        public string PreviousCurrentPlayer { get; }
    }
}
