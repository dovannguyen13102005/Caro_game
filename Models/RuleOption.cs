namespace Caro_game.Models
{
    public class RuleOption
    {
        public RuleOption(GameRule rule, string display)
        {
            Rule = rule;
            Display = display;
        }

        public GameRule Rule { get; }
        public string Display { get; }

        public override string ToString() => Display;
    }
}
