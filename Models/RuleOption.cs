namespace Caro_game.Models
{
    public class RuleOption
    {
        public RuleOption(GameRuleType rule, string displayName, string description)
        {
            Rule = rule;
            DisplayName = displayName;
            Description = description;
        }

        public GameRuleType Rule { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public override string ToString()
            => DisplayName;
    }
}
