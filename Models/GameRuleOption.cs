using System;

namespace Caro_game.Models;

public enum GameRuleType
{
    Freestyle,
    Standard,
    Renju
}

public class GameRuleOption
{
    public GameRuleOption(GameRuleType type, string name, int boardSize, string engineKeyword)
    {
        Type = type;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        BoardSize = boardSize;
        EngineKeyword = engineKeyword ?? string.Empty;
    }

    public GameRuleType Type { get; }
    public string Name { get; }
    public int BoardSize { get; }
    public string EngineKeyword { get; }
    public bool AllowsExpansion { get; init; }
}
