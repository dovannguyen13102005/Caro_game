using System;
using Caro_game.Rules;

namespace Caro_game.Models
{
    public class RuleOption
    {
        private readonly Func<IRule> _ruleFactory;

        public RuleOption(
            string name,
            Func<IRule> ruleFactory,
            int rows,
            int columns,
            string? configFile = null,
            string? configFileWhite = null,
            bool allowExpansion = false)
        {
            Name = name;
            _ruleFactory = ruleFactory;
            Rows = rows;
            Columns = columns;
            ConfigFile = configFile;
            ConfigFileWhite = configFileWhite;
            AllowExpansion = allowExpansion;
        }

        public string Name { get; }

        public int Rows { get; }

        public int Columns { get; }

        public string? ConfigFile { get; }

        public string? ConfigFileWhite { get; }

        public bool AllowExpansion { get; }

        public IRule CreateRule() => _ruleFactory().Clone();

        public string? ResolveConfigFile(bool aiPlaysBlack)
        {
            if (!string.IsNullOrWhiteSpace(ConfigFile) && string.IsNullOrWhiteSpace(ConfigFileWhite))
            {
                return ConfigFile;
            }

            if (!string.IsNullOrWhiteSpace(ConfigFile) && !string.IsNullOrWhiteSpace(ConfigFileWhite))
            {
                return aiPlaysBlack ? ConfigFile : ConfigFileWhite;
            }

            return null;
        }

        public override string ToString() => Name;
    }
}
