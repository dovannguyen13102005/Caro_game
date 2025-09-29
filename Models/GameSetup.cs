using System;
using System.Collections.Generic;
using System.Linq;

namespace Caro_game.Models
{
    public class GameSetup
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public int InitialRows { get; set; }
        public int InitialColumns { get; set; }
        public GameRule Rule { get; set; } = GameRule.Freestyle;
        public string FirstPlayer { get; set; } = "X";
        public HashSet<(int Row, int Col)> ForbiddenCells { get; } = new();
        public List<StonePlacement> InitialPlacements { get; } = new();
        public bool AllowExpansion { get; set; }
        public int ExpansionThreshold { get; set; } = 2;
        public int MaxRows { get; set; }
        public int MaxColumns { get; set; }

        public GameSetup()
        {
        }

        public GameSetup(int rows, int columns, GameRule rule, string firstPlayer)
        {
            Rows = rows;
            Columns = columns;
            InitialRows = rows;
            InitialColumns = columns;
            Rule = rule;
            FirstPlayer = NormalizePlayer(firstPlayer);
            MaxRows = Math.Max(rows, 60);
            MaxColumns = Math.Max(columns, 60);
        }

        public string NormalizePlayer(string player)
        {
            if (string.IsNullOrWhiteSpace(player))
            {
                return "X";
            }

            return player.StartsWith("O", StringComparison.OrdinalIgnoreCase) ? "O" : "X";
        }

        public bool IsFirstPlayer(string player)
            => string.Equals(NormalizePlayer(player), NormalizePlayer(FirstPlayer), StringComparison.OrdinalIgnoreCase);

        public GameSetup Clone()
        {
            var clone = new GameSetup(Rows, Columns, Rule, FirstPlayer)
            {
                AllowExpansion = AllowExpansion,
                ExpansionThreshold = ExpansionThreshold,
                MaxRows = MaxRows,
                MaxColumns = MaxColumns,
                InitialRows = InitialRows,
                InitialColumns = InitialColumns
            };

            foreach (var cell in ForbiddenCells)
            {
                clone.ForbiddenCells.Add(cell);
            }

            foreach (var placement in InitialPlacements.Select(p => new StonePlacement
                     {
                         Row = p.Row,
                         Col = p.Col,
                         Player = NormalizePlayer(p.Player)
                     }))
            {
                clone.InitialPlacements.Add(placement);
            }

            return clone;
        }
    }
}
