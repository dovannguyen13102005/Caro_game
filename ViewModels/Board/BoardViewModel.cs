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

    public GameRule GameRule { get; }

    private readonly Dictionary<(int Row, int Col), Cell> _cellLookup;
    private readonly HashSet<(int Row, int Col)> _candidatePositions;
    private readonly object _candidateLock = new();
    private readonly string _initialPlayer;

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

    private EngineClient? _engine;

    public event EventHandler<GameEndedEventArgs>? GameEnded;

    public BoardViewModel(int rows, int columns, string firstPlayer, string aiMode = "Dễ", GameRule gameRule = GameRule.Freestyle)
    {
        Rows = rows;
        Columns = columns;
        GameRule = gameRule;
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

        if (AIMode == "Chuyên nghiệp")
        {
            TryInitializeProfessionalEngine();
        }
    }
}
