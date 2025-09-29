using System;
using System.Collections.Generic;
using System.Linq;
using Caro_game.Models;
using Caro_game.ViewModels;
using Xunit;

namespace Caro_game.Tests
{
    public class RuleValidationTests
    {
        [Fact]
        public void Freestyle_AllowsFiveInRowWin()
        {
            var setup = CreateSetup(GameRule.Freestyle, new[]
            {
                Stone("X", 0, 0),
                Stone("X", 0, 1),
                Stone("X", 0, 2),
                Stone("X", 0, 3)
            });

            var board = new BoardViewModel(setup) { IsAIEnabled = false };
            bool ended = false;
            board.GameEnded += (_, e) => ended = e.HasWinner && e.Winner == "X";

            MakeMove(board, 0, 4);

            Assert.True(ended);
            Assert.Equal("O", board.CurrentPlayer);
        }

        [Fact]
        public void Freestyle_AllowsOverlineWin()
        {
            var setup = CreateSetup(GameRule.Freestyle, new[]
            {
                Stone("X", 0, 0),
                Stone("X", 0, 1),
                Stone("X", 0, 2),
                Stone("X", 0, 3),
                Stone("X", 0, 4)
            });

            var board = new BoardViewModel(setup) { IsAIEnabled = false };
            bool ended = false;
            board.GameEnded += (_, e) => ended = e.HasWinner && e.Winner == "X";

            MakeMove(board, 0, 5);

            Assert.True(ended);
        }

        [Fact]
        public void Standard_WinRequiresExactlyFive()
        {
            var setup = CreateSetup(GameRule.Standard, new[]
            {
                Stone("X", 0, 0),
                Stone("X", 0, 1),
                Stone("X", 0, 2),
                Stone("X", 0, 3)
            });

            var board = new BoardViewModel(setup) { IsAIEnabled = false };
            bool ended = false;
            board.GameEnded += (_, e) => ended = e.HasWinner;

            MakeMove(board, 0, 4);

            Assert.True(ended);
        }

        [Fact]
        public void Standard_OverlineDoesNotWin()
        {
            var setup = CreateSetup(GameRule.Standard, new[]
            {
                Stone("X", 0, 0),
                Stone("X", 0, 1),
                Stone("X", 0, 2),
                Stone("X", 0, 3),
                Stone("X", 0, 4)
            });

            var board = new BoardViewModel(setup) { IsAIEnabled = false };
            bool ended = false;
            board.GameEnded += (_, e) => ended = true;

            MakeMove(board, 0, 5);

            Assert.False(ended);
            Assert.Equal("O", board.CurrentPlayer);
            Assert.False(board.Cells.Any(c => c.IsWinningCell));
        }

        [Fact]
        public void Renju_FirstPlayerExactFiveWins()
        {
            var setup = CreateSetup(GameRule.Renju, new[]
            {
                Stone("X", 0, 0),
                Stone("X", 0, 1),
                Stone("X", 0, 2),
                Stone("X", 0, 3)
            });

            var board = new BoardViewModel(setup) { IsAIEnabled = false };
            bool ended = false;
            board.GameEnded += (_, e) => ended = e.HasWinner;

            MakeMove(board, 0, 4);

            Assert.True(ended);
        }

        [Fact]
        public void Renju_OverlineIsForbiddenForFirstPlayer()
        {
            var setup = CreateSetup(GameRule.Renju, new[]
            {
                Stone("X", 0, 0),
                Stone("X", 0, 1),
                Stone("X", 0, 2),
                Stone("X", 0, 3),
                Stone("X", 0, 4)
            });

            var board = new BoardViewModel(setup) { IsAIEnabled = false };

            MakeMove(board, 0, 5);

            Assert.True(IsEmpty(board, 0, 5));
            Assert.Equal("X", board.CurrentPlayer);
        }

        [Fact]
        public void Renju_DoubleFourIsForbidden()
        {
            var placements = new[]
            {
                Stone("X", 7, 4),
                Stone("X", 7, 5),
                Stone("X", 7, 6),
                Stone("X", 4, 7),
                Stone("X", 5, 7),
                Stone("X", 6, 7)
            };
            var setup = CreateSetup(GameRule.Renju, placements);

            var board = new BoardViewModel(setup) { IsAIEnabled = false };

            MakeMove(board, 7, 7);

            Assert.True(IsEmpty(board, 7, 7));
            Assert.Equal("X", board.CurrentPlayer);
        }

        [Fact]
        public void Renju_DoubleThreeIsForbidden()
        {
            var placements = new[]
            {
                Stone("X", 7, 5),
                Stone("X", 7, 6),
                Stone("X", 5, 7),
                Stone("X", 6, 7)
            };
            var setup = CreateSetup(GameRule.Renju, placements);

            var board = new BoardViewModel(setup) { IsAIEnabled = false };

            MakeMove(board, 7, 7);

            Assert.True(IsEmpty(board, 7, 7));
            Assert.Equal("X", board.CurrentPlayer);
        }

        [Fact]
        public void ForbiddenCellRejectsMove()
        {
            var setup = new GameSetup(10, 10, GameRule.Freestyle, "X");
            setup.ForbiddenCells.Add((2, 2));

            var board = new BoardViewModel(setup) { IsAIEnabled = false };

            MakeMove(board, 2, 2);

            Assert.True(IsEmpty(board, 2, 2));
        }

        [Fact]
        public void ExpansionAddsRowWhenNearEdge()
        {
            var setup = new GameSetup(5, 5, GameRule.Freestyle, "X")
            {
                AllowExpansion = true,
                ExpansionThreshold = 1,
                MaxRows = 10,
                MaxColumns = 10
            };

            var board = new BoardViewModel(setup) { IsAIEnabled = false };

            MakeMove(board, 0, 2);

            Assert.True(board.Rows > 5);
            var stone = board.Cells.Single(c => c.Value == "X");
            Assert.Equal(1, stone.Row);
        }

        private static StonePlacement Stone(string player, int row, int col)
            => new StonePlacement { Player = player, Row = row, Col = col };

        private static GameSetup CreateSetup(GameRule rule, IEnumerable<StonePlacement> placements)
        {
            var setup = new GameSetup(15, 15, rule, "X");
            foreach (var placement in placements)
            {
                setup.InitialPlacements.Add(placement);
            }
            return setup;
        }

        private static void MakeMove(BoardViewModel board, int row, int col)
        {
            var cell = board.Cells.Single(c => c.Row == row && c.Col == col);
            board.MakeMove(cell);
        }

        private static bool IsEmpty(BoardViewModel board, int row, int col)
            => board.Cells.Single(c => c.Row == row && c.Col == col).Value == string.Empty;
    }
}
