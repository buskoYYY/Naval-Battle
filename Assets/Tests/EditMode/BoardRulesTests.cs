using System.Collections.Generic;
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
            var board = CreateBoardWithSingleShip();
            var empty = new CellCoord(5, 5);

            var outcome = board.Fire(empty, out var ship, out var sunk);

            Assert.AreEqual(ShotOutcome.Miss, outcome);
            Assert.IsNull(ship);
            Assert.IsNull(sunk);
            Assert.IsTrue(board.WasShot(empty));
        }

        [Test]
        public void Fire_OnShipCell_ReturnsHit_UntilLastCell()
        {
            var board = CreateBoardWithSingleShip();

            var first = board.Fire(new CellCoord(0, 0), out var ship, out var sunk);
            Assert.AreEqual(ShotOutcome.Hit, first);
            Assert.IsNotNull(ship);
            Assert.IsNull(sunk);
            Assert.IsFalse(ship.IsSunk);

            var second = board.Fire(new CellCoord(1, 0), out ship, out sunk);
            Assert.AreEqual(ShotOutcome.Sunk, second);
            Assert.IsTrue(ship.IsSunk);
            Assert.AreEqual(2, sunk.Count);
        }

        [Test]
        public void Fire_SameCellTwice_Throws()
        {
            var board = CreateBoardWithSingleShip();
            var cell = new CellCoord(5, 5);
            board.Fire(cell, out _, out _);

            Assert.Throws<System.InvalidOperationException>(() => board.Fire(cell, out _, out _));
        }

        [Test]
        public void Fire_OutOfBounds_Throws()
        {
            var board = CreateBoardWithSingleShip();
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                board.Fire(new CellCoord(-1, 0), out _, out _));
        }

        [Test]
        public void AllShipsSunk_WhenEveryShipDestroyed()
        {
            var board = CreateBoardWithSingleShip();
            Assert.IsFalse(board.AllShipsSunk());

            board.Fire(new CellCoord(0, 0), out _, out _);
            Assert.IsFalse(board.AllShipsSunk());

            board.Fire(new CellCoord(1, 0), out _, out _);
            Assert.IsTrue(board.AllShipsSunk());
        }

        [Test]
        public void FogMarks_NeverRevealUntouchedShips()
        {
            var board = CreateBoardWithSingleShip();
            var fog = board.ToFogCellMarks();

            for (var i = 0; i < fog.Length; i++)
                Assert.AreEqual((byte)CellMark.Unknown, fog[i]);

            board.Fire(new CellCoord(0, 0), out _, out _);
            fog = board.ToFogCellMarks();
            Assert.AreEqual((byte)CellMark.Hit, fog[0]);
            Assert.AreEqual((byte)CellMark.Unknown, fog[1]);
        }

        [Test]
        public void OwnMarks_ShowShipsAndShotResults()
        {
            var board = CreateBoardWithSingleShip();
            var own = board.ToOwnCellMarks();
            Assert.AreEqual((byte)CellMark.Ship, own[0]);
            Assert.AreEqual((byte)CellMark.Ship, own[1]);
            Assert.AreEqual((byte)CellMark.Empty, own[2]);

            board.Fire(new CellCoord(5, 5), out _, out _);
            own = board.ToOwnCellMarks();
            Assert.AreEqual((byte)CellMark.Miss, own[5 * board.Size + 5]);
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

        [Test]
        public void Place_ShipsDoNotOverlapOrTouchOrthogonally()
        {
            var config = CreateConfig();
            var board = ShipPlacer.CreateRandomBoard(config, new System.Random(42));
            var occupied = new HashSet<CellCoord>();

            foreach (var ship in board.Ships)
            {
                foreach (var cell in ship.Cells)
                {
                    Assert.IsTrue(board.InBounds(cell));
                    Assert.IsFalse(occupied.Contains(cell), $"Overlap at {cell}");
                    occupied.Add(cell);
                }
            }

            var orthogonal = new[]
            {
                new CellCoord(1, 0), new CellCoord(-1, 0),
                new CellCoord(0, 1), new CellCoord(0, -1)
            };

            foreach (var ship in board.Ships)
            {
                var own = new HashSet<CellCoord>(ship.Cells);
                foreach (var cell in ship.Cells)
                {
                    foreach (var d in orthogonal)
                    {
                        var n = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                        if (!board.InBounds(n) || own.Contains(n))
                            continue;

                        Assert.IsFalse(occupied.Contains(n),
                            $"Ship touches another orthogonally at {cell} -> {n}");
                    }
                }
            }
        }

        private static Board CreateBoardWithSingleShip()
        {
            var ship = new Ship(2);
            ship.Cells.Add(new CellCoord(0, 0));
            ship.Cells.Add(new CellCoord(1, 0));
            return new Board(6, new List<Ship> { ship });
        }

        private static GameConfig CreateConfig()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            config.BoardSize = 6;
            config.ShipLengths = new[] { 3, 2, 2, 1 };
            config.ForbidOrthogonalTouch = true;
            return config;
        }
    }
}
