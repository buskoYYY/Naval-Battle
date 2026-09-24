using NavalBattle.Config;
using NavalBattle.Server;
using NavalBattle.Shared;
using NUnit.Framework;

namespace NavalBattle.Tests
{
    public class BoardRulesTests
    {
        [Test]
        public void Fire_OnEmptyCell_ReturnsMiss()
        {
            var config = CreateConfig();
            var board = ShipPlacer.CreateRandomBoard(config, new System.Random(1));
            var empty = FindEmpty(board);

            var outcome = board.Fire(empty, out var ship, out var sunk);
            Assert.AreEqual(ShotOutcome.Miss, outcome);
            Assert.IsNull(ship);
            Assert.IsNull(sunk);
        }

        [Test]
        public void Fire_SameCellTwice_Throws()
        {
            var config = CreateConfig();
            var board = ShipPlacer.CreateRandomBoard(config, new System.Random(2));
            var cell = FindEmpty(board);
            board.Fire(cell, out _, out _);
            Assert.Throws<System.InvalidOperationException>(() => board.Fire(cell, out _, out _));
        }

        [Test]
        public void Place_CreatesExpectedShipCellCount()
        {
            var config = CreateConfig();
            var board = ShipPlacer.CreateRandomBoard(config, new System.Random(3));
            var expected = 0;
            foreach (var length in config.ShipLengths)
                expected += length;

            var shipCells = 0;
            foreach (var ship in board.Ships)
                shipCells += ship.Cells.Count;

            Assert.AreEqual(expected, shipCells);
            Assert.AreEqual(config.ShipLengths.Length, board.Ships.Count);
        }

        private static GameConfig CreateConfig()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            config.BoardSize = 6;
            config.ShipLengths = new[] { 3, 2, 2, 1 };
            config.ForbidOrthogonalTouch = true;
            return config;
        }

        private static CellCoord FindEmpty(Board board)
        {
            for (var y = 0; y < board.Size; y++)
            for (var x = 0; x < board.Size; x++)
            {
                var cell = new CellCoord(x, y);
                if (!board.TryGetShip(cell, out _))
                    return cell;
            }

            Assert.Fail("No empty cell");
            return default;
        }
    }
}
