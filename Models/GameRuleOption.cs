namespace Caro_game.Models;

public record GameRuleOption(GameRuleType Type, string Name, int BoardSize)
{
    public override string ToString() => Name;
}
