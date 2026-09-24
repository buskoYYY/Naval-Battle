using System.Linq;
using NavalBattle.Config;
using NavalBattle.Messages;
using NavalBattle.Server;
using NavalBattle.Shared;
using NUnit.Framework;

namespace NavalBattle.Tests
{
    public class GameServerRulesTests
    {
        private GameConfig _config;
        private FakeTransportHub _transport;
        private GameServer _server;

        [SetUp]
        public void SetUp()
        {
            _config = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            _config.BoardSize = 6;
            _config.ShipLengths = new[] { 3, 2, 2, 1 };
            _config.ForbidOrthogonalTouch = true;
            _config.DefaultLatencyMs = 0f;
            _config.FirstTurnPlayerId = 1; // Player1 starts — deterministic for assertions

            _transport = new FakeTransportHub();
            _server = new GameServer(_config, _transport);
        }

        [TearDown]
        public void TearDown()
        {
            _server.Shutdown();
            _transport.Shutdown();
        }

        [Test]
        public void MatchStarts_WhenBothPlayersJoin()
        {
            JoinBoth();

            Assert.AreEqual(1, _transport.CountTo("c1", MessageType.MatchStarted));
            Assert.AreEqual(1, _transport.CountTo("c2", MessageType.MatchStarted));

            var started = _transport.LastPayloadTo<MatchStartedMessage>("c1", MessageType.MatchStarted);
            Assert.AreEqual(6, started.BoardSize);
            Assert.AreEqual(_config.BoardSize * _config.BoardSize, started.YourCells.Length);
            Assert.IsTrue(started.YourCells.Any(c => c == (byte)CellMark.Ship));
        }

        [Test]
        public void Fire_NotYourTurn_IsRejected()
        {
            JoinBoth();

            var waitingPeer = FindWaitingPeer();
            var turnPeer = waitingPeer == "c1" ? "c2" : "c1";

            // Sanity: turn peer really is the one MatchStarted marked as current.
            var turnStart = _transport.LastPayloadTo<MatchStartedMessage>(turnPeer, MessageType.MatchStarted);
            var waitStart = _transport.LastPayloadTo<MatchStartedMessage>(waitingPeer, MessageType.MatchStarted);
            Assert.AreEqual((PlayerId)turnStart.CurrentTurnPlayerId, (PlayerId)turnStart.YourPlayerId);
            Assert.AreNotEqual((PlayerId)waitStart.CurrentTurnPlayerId, (PlayerId)waitStart.YourPlayerId);

            _transport.DeliverFromClient(waitingPeer, MessageType.FireRequest, new FireRequest
            {
                RequestId = "bad-turn",
                X = 0,
                Y = 0
            });

            Assert.AreEqual(0, _transport.CountTo(waitingPeer, MessageType.ShotResult),
                "Waiting player must not receive ShotResult");
            Assert.Greater(_transport.CountTo(waitingPeer, MessageType.FireRejected), 0,
                "Waiting player must receive FireRejected");

            var rejected = _transport.LastPayloadTo<FireRejectedMessage>(waitingPeer, MessageType.FireRejected);
            Assert.AreEqual("bad-turn", rejected.RequestId);
            Assert.AreEqual((byte)RejectReason.NotYourTurn, rejected.Reason,
                $"Unexpected reject reason: {(RejectReason)rejected.Reason}");
        }

        [Test]
        public void Fire_ValidShot_BroadcastsResultAndSwitchesTurn()
        {
            JoinBoth();
            var shooter = CurrentTurnPeer();
            var before = CurrentTurnPlayerId();

            _transport.DeliverFromClient(shooter, MessageType.FireRequest, new FireRequest
            {
                RequestId = "shot-1",
                X = 0,
                Y = 0
            });

            var resultToShooter = _transport.LastPayloadTo<ShotResultMessage>(shooter, MessageType.ShotResult);
            Assert.AreEqual("shot-1", resultToShooter.RequestId);
            Assert.AreEqual(0, resultToShooter.X);
            Assert.AreEqual(0, resultToShooter.Y);
            Assert.AreNotEqual(before, (PlayerId)resultToShooter.NextTurnPlayerId);
            Assert.IsFalse(resultToShooter.GameOver);
        }

        [Test]
        public void Fire_DuplicateRequestId_ReplaysCachedResult_WithoutExtraBroadcast()
        {
            JoinBoth();
            var shooter = CurrentTurnPeer();
            var other = shooter == "c1" ? "c2" : "c1";

            var request = new FireRequest { RequestId = "dup-1", X = 1, Y = 1 };
            _transport.DeliverFromClient(shooter, MessageType.FireRequest, request);

            var first = _transport.LastPayloadTo<ShotResultMessage>(shooter, MessageType.ShotResult);
            var resultsToOtherAfterFirst = _transport.CountTo(other, MessageType.ShotResult);

            _transport.DeliverFromClient(shooter, MessageType.FireRequest, request);

            var second = _transport.LastPayloadTo<ShotResultMessage>(shooter, MessageType.ShotResult);
            Assert.AreEqual(first.RequestId, second.RequestId);
            Assert.AreEqual(first.Outcome, second.Outcome);
            Assert.AreEqual(first.NextTurnPlayerId, second.NextTurnPlayerId);

            // Idempotent replay is sent only to the shooter, opponent is not notified again.
            Assert.AreEqual(resultsToOtherAfterFirst, _transport.CountTo(other, MessageType.ShotResult));
        }

        [Test]
        public void Fire_RetryAfterLostResult_DoesNotAdvanceTurnTwice()
        {
            JoinBoth();
            // Simulates: first FireRequest applied, ShotResult "lost", client resends same id.
            _transport.DeliverFromClient("c1", MessageType.FireRequest, new FireRequest
            {
                RequestId = "lost-result",
                X = 3,
                Y = 3
            });

            var first = _transport.LastPayloadTo<ShotResultMessage>("c1", MessageType.ShotResult);
            Assert.AreEqual((byte)PlayerId.Player2, first.NextTurnPlayerId);
            var resultsToC2 = _transport.CountTo("c2", MessageType.ShotResult);

            _transport.DeliverFromClient("c1", MessageType.FireRequest, new FireRequest
            {
                RequestId = "lost-result",
                X = 3,
                Y = 3
            });

            var replay = _transport.LastPayloadTo<ShotResultMessage>("c1", MessageType.ShotResult);
            Assert.AreEqual(first.NextTurnPlayerId, replay.NextTurnPlayerId);
            Assert.AreEqual(first.Outcome, replay.Outcome);
            Assert.AreEqual(resultsToC2, _transport.CountTo("c2", MessageType.ShotResult),
                "Opponent must not get a second ShotResult for a retried requestId");
        }

        [Test]
        public void Fire_SameCellNewRequest_AfterTurnPass_RejectedAsAlreadyShot()
        {
            JoinBoth();
            // Each player shoots the opponent's board. Same coordinates on different
            // boards are independent — so to hit AlreadyShot we must return the turn
            // to the original shooter and repeat their cell.
            _transport.DeliverFromClient("c1", MessageType.FireRequest, new FireRequest
            {
                RequestId = "a",
                X = 2,
                Y = 2
            });

            var first = _transport.LastPayloadTo<ShotResultMessage>("c1", MessageType.ShotResult);
            Assert.AreEqual((byte)PlayerId.Player2, first.NextTurnPlayerId);

            _transport.DeliverFromClient("c2", MessageType.FireRequest, new FireRequest
            {
                RequestId = "b",
                X = 0,
                Y = 0
            });

            var second = _transport.LastPayloadTo<ShotResultMessage>("c2", MessageType.ShotResult);
            Assert.AreEqual((byte)PlayerId.Player1, second.NextTurnPlayerId);

            _transport.DeliverFromClient("c1", MessageType.FireRequest, new FireRequest
            {
                RequestId = "c",
                X = 2,
                Y = 2
            });

            Assert.Greater(_transport.CountTo("c1", MessageType.FireRejected), 0);
            var rejected = _transport.LastPayloadTo<FireRejectedMessage>("c1", MessageType.FireRejected);
            Assert.AreEqual("c", rejected.RequestId);
            Assert.AreEqual((byte)RejectReason.AlreadyShot, rejected.Reason);
        }

        [Test]
        public void Snapshot_DoesNotIncludeOpponentShipCells()
        {
            JoinBoth();
            _server.NotifyPeerDisconnected("c2");
            _server.NotifyPeerConnected("c2");

            // Reconnect sends snapshot; also deliver explicit reconnect.
            _transport.DeliverFromClient("c2", MessageType.ReconnectRequest, new ReconnectRequest
            {
                PlayerId = (byte)PlayerId.Player2,
                LastReceivedSeq = 0
            });

            var snapshot = _transport.LastPayloadTo<StateSnapshotMessage>("c2", MessageType.StateSnapshot);
            Assert.AreEqual(_config.BoardSize * _config.BoardSize, snapshot.OpponentFogCells.Length);

            foreach (var mark in snapshot.OpponentFogCells)
            {
                Assert.AreNotEqual((byte)CellMark.Ship, mark,
                    "Fog must not contain opponent ship marks");
            }
        }

        private void JoinBoth()
        {
            _transport.DeliverFromClient("c1", MessageType.JoinRequest, new JoinRequest
            {
                PreferredPlayerId = (byte)PlayerId.Player1
            });
            _transport.DeliverFromClient("c2", MessageType.JoinRequest, new JoinRequest
            {
                PreferredPlayerId = (byte)PlayerId.Player2
            });
        }

        private string CurrentTurnPeer()
        {
            foreach (var peer in new[] { "c1", "c2" })
            {
                var started = _transport.LastPayloadTo<MatchStartedMessage>(peer, MessageType.MatchStarted);
                if ((PlayerId)started.YourPlayerId == (PlayerId)started.CurrentTurnPlayerId)
                    return peer;
            }

            throw new System.InvalidOperationException("No turn peer found in MatchStarted messages.");
        }

        private string FindWaitingPeer()
        {
            foreach (var peer in new[] { "c1", "c2" })
            {
                var started = _transport.LastPayloadTo<MatchStartedMessage>(peer, MessageType.MatchStarted);
                if ((PlayerId)started.YourPlayerId != (PlayerId)started.CurrentTurnPlayerId)
                    return peer;
            }

            throw new System.InvalidOperationException("No waiting peer found in MatchStarted messages.");
        }

        private PlayerId CurrentTurnPlayerId()
        {
            var started = _transport.LastPayloadTo<MatchStartedMessage>("c1", MessageType.MatchStarted);
            return (PlayerId)started.CurrentTurnPlayerId;
        }
    }
}
