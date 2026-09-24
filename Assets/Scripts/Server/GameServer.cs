using System;
using System.Collections.Generic;
using NavalBattle.Config;
using NavalBattle.Messages;
using NavalBattle.Shared;
using NavalBattle.Transport;
using UnityEngine;

namespace NavalBattle.Server
{
    public sealed class GameServer
    {
        private sealed class PlayerBinding
        {
            public PlayerId PlayerId;
            public string PeerId;
            public bool Connected;
            public Board Board;
            public string PendingRequestId;
            public CellCoord? PendingCell;
            public readonly Dictionary<string, ShotResultMessage> ProcessedShots = new();
        }

        private readonly GameConfig _config;
        private readonly ITransportHub _transport;
        private readonly Dictionary<string, PlayerBinding> _byPeer = new();
        private readonly Dictionary<PlayerId, PlayerBinding> _byPlayer = new();

        private MatchPhase _phase = MatchPhase.WaitingForPlayers;
        private PlayerId _currentTurn = PlayerId.None;
        private PlayerId _winner = PlayerId.None;
        private bool _alive = true;
        private float _lastTickNow = -1f;
        private float _turnEndsAt = float.PositiveInfinity;
        private float _pausedTurnRemaining = -1f;

        private float Now => _lastTickNow >= 0f ? _lastTickNow : Time.realtimeSinceStartup;

        public GameServer(GameConfig config, ITransportHub transport)
        {
            _config = config;
            _transport = transport;
            _transport.BindServerHandler(OnClientMessage);
        }

        public void Tick(float nowSeconds)
        {
            if (!_alive)
                return;

            _lastTickNow = nowSeconds;

            if (_phase != MatchPhase.Playing)
                return;

            if (_config.TurnTimeoutSeconds <= 0f)
                return;

            if (nowSeconds < _turnEndsAt)
                return;

            PassTurnDueToTimeout();
        }

        public void Shutdown()
        {
            _alive = false;
            _byPeer.Clear();
            _byPlayer.Clear();
        }

        public void NotifyPeerDisconnected(string peerId)
        {
            if (!_alive || !_byPeer.TryGetValue(peerId, out var binding))
                return;

            binding.Connected = false;
            binding.PendingRequestId = null;
            binding.PendingCell = null;

            if (_phase == MatchPhase.Playing)
            {
                if (_config.TurnTimeoutSeconds > 0f && !float.IsPositiveInfinity(_turnEndsAt))
                    _pausedTurnRemaining = Mathf.Max(0f, _turnEndsAt - Now);

                _phase = MatchPhase.PausedDisconnected;
                BroadcastTurnClock("Timer paused (disconnect)");
            }

            NotifyOpponentConnection(binding.PlayerId, false);
        }

        public void NotifyPeerConnected(string peerId)
        {
            if (!_alive || !_byPeer.TryGetValue(peerId, out var binding))
                return;

            binding.Connected = true;

            if (_phase == MatchPhase.PausedDisconnected && AllPlayersConnected())
            {
                _phase = MatchPhase.Playing;
                if (_config.TurnTimeoutSeconds > 0f && _pausedTurnRemaining >= 0f)
                {
                    _turnEndsAt = Now + _pausedTurnRemaining;
                    _pausedTurnRemaining = -1f;
                }

                BroadcastTurnClock("Timer resumed (reconnect)");
            }

            NotifyOpponentConnection(binding.PlayerId, true);
            SendSnapshot(binding);
        }

        private void OnClientMessage(string peerId, NetworkEnvelope envelope)
        {
            if (!_alive)
                return;

            try
            {
                switch (envelope.Type)
                {
                    case MessageType.JoinRequest:
                        HandleJoin(peerId, MessageSerializer.Unwrap<JoinRequest>(envelope));
                        break;
                    case MessageType.FireRequest:
                        HandleFire(peerId, MessageSerializer.Unwrap<FireRequest>(envelope));
                        break;
                    case MessageType.ReconnectRequest:
                        HandleReconnect(peerId, MessageSerializer.Unwrap<ReconnectRequest>(envelope));
                        break;
                    case MessageType.SyncRequest:
                        HandleSync(peerId);
                        break;
                    default:
                        Send(peerId, MessageType.Error, new ErrorMessage { Text = $"Unknown type {envelope.Type}" });
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Send(peerId, MessageType.Error, new ErrorMessage { Text = ex.Message });
            }
        }

        private void HandleJoin(string peerId, JoinRequest request)
        {
            if (_byPeer.ContainsKey(peerId))
            {
                SendSnapshot(_byPeer[peerId]);
                return;
            }

            if (_byPlayer.Count >= 2)
            {
                Send(peerId, MessageType.Error, new ErrorMessage { Text = "Match is full." });
                return;
            }

            var playerId = AssignPlayerId(request.PreferredPlayerId);
            var binding = new PlayerBinding
            {
                PlayerId = playerId,
                PeerId = peerId,
                Connected = true
            };

            _byPeer[peerId] = binding;
            _byPlayer[playerId] = binding;

            Send(peerId, MessageType.Welcome, new WelcomeMessage
            {
                PlayerId = (byte)playerId,
                Phase = (byte)_phase
            });

            if (_byPlayer.Count == 2)
                StartMatch();
        }

        private void HandleReconnect(string peerId, ReconnectRequest request)
        {
            var playerId = (PlayerId)request.PlayerId;
            if (!_byPlayer.TryGetValue(playerId, out var binding))
            {
                HandleJoin(peerId, new JoinRequest { PreferredPlayerId = request.PlayerId });
                return;
            }

            _byPeer.Remove(binding.PeerId);
            binding.PeerId = peerId;
            binding.Connected = true;
            _byPeer[peerId] = binding;

            if (_phase == MatchPhase.PausedDisconnected && AllPlayersConnected())
            {
                _phase = MatchPhase.Playing;
                if (_config.TurnTimeoutSeconds > 0f && _pausedTurnRemaining >= 0f)
                {
                    _turnEndsAt = Now + _pausedTurnRemaining;
                    _pausedTurnRemaining = -1f;
                }

                BroadcastTurnClock("Timer resumed (reconnect)");
            }

            NotifyOpponentConnection(playerId, true);
            SendSnapshot(binding);
        }

        private void HandleSync(string peerId)
        {
            if (!_byPeer.TryGetValue(peerId, out var binding))
                return;

            SendSnapshot(binding);
        }

        private void HandleFire(string peerId, FireRequest request)
        {
            if (!_byPeer.TryGetValue(peerId, out var shooter))
                return;

            // Allow fire while opponent is disconnected: with latency the request may arrive
            // after Pause. Rejecting it would roll the turn back to the shooter incorrectly.
            if (_phase is MatchPhase.Finished or MatchPhase.WaitingForPlayers)
            {
                Reject(shooter, request.RequestId, RejectReason.GameNotRunning);
                return;
            }

            if (shooter.ProcessedShots.TryGetValue(request.RequestId, out var cached))
            {
                Send(shooter.PeerId, MessageType.ShotResult, cached);
                return;
            }

            if (!string.IsNullOrEmpty(shooter.PendingRequestId) &&
                shooter.PendingRequestId != request.RequestId)
            {
                Reject(shooter, request.RequestId, RejectReason.ShotPending);
                return;
            }

            if (shooter.PlayerId != _currentTurn)
            {
                Reject(shooter, request.RequestId, RejectReason.NotYourTurn);
                return;
            }

            if (!shooter.Connected)
            {
                Reject(shooter, request.RequestId, RejectReason.NotConnected);
                return;
            }

            var cell = new CellCoord(request.X, request.Y);
            var target = OpponentOf(shooter.PlayerId);
            if (target?.Board == null)
            {
                Reject(shooter, request.RequestId, RejectReason.GameNotRunning);
                return;
            }

            if (!target.Board.InBounds(cell))
            {
                Reject(shooter, request.RequestId, RejectReason.OutOfBounds);
                return;
            }

            if (target.Board.WasShot(cell))
            {
                Reject(shooter, request.RequestId, RejectReason.AlreadyShot);
                return;
            }

            shooter.PendingRequestId = request.RequestId;
            shooter.PendingCell = cell;

            var outcome = target.Board.Fire(cell, out _, out var sunkCells);
            var gameOver = target.Board.AllShipsSunk();
            if (gameOver)
            {
                _winner = shooter.PlayerId;
                _phase = MatchPhase.Finished;
            }
            else
            {
                _currentTurn = target.PlayerId;
                _phase = AllPlayersConnected()
                    ? MatchPhase.Playing
                    : MatchPhase.PausedDisconnected;
                if (_phase == MatchPhase.Playing)
                    RestartTurnClock();
            }

            var result = new ShotResultMessage
            {
                RequestId = request.RequestId,
                ShooterPlayerId = (byte)shooter.PlayerId,
                X = cell.X,
                Y = cell.Y,
                Outcome = (byte)outcome,
                SunkCellsX = sunkCells != null ? sunkCells.ConvertAll(c => c.X).ToArray() : Array.Empty<int>(),
                SunkCellsY = sunkCells != null ? sunkCells.ConvertAll(c => c.Y).ToArray() : Array.Empty<int>(),
                NextTurnPlayerId = (byte)_currentTurn,
                GameOver = gameOver,
                WinnerPlayerId = (byte)_winner,
                TurnSecondsRemaining = GetTurnSecondsRemaining(),
                TurnTimeoutSeconds = _config.TurnTimeoutSeconds,
                TurnEndsAtRealtime = GetTurnEndsAtRealtime()
            };

            shooter.ProcessedShots[request.RequestId] = result;
            shooter.PendingRequestId = null;
            shooter.PendingCell = null;

            Broadcast(MessageType.ShotResult, result);

            if (gameOver)
            {
                Broadcast(MessageType.MatchFinished, new MatchFinishedMessage
                {
                    WinnerPlayerId = (byte)_winner,
                    Reason = "All ships sunk"
                });
            }
        }

        private void StartMatch()
        {
            foreach (var binding in _byPlayer.Values)
                binding.Board = ShipPlacer.CreateRandomBoard(_config);

            _currentTurn = ResolveFirstTurn();
            _phase = MatchPhase.Playing;
            _winner = PlayerId.None;
            _pausedTurnRemaining = -1f;
            RestartTurnClock();

            foreach (var binding in _byPlayer.Values)
            {
                Send(binding.PeerId, MessageType.MatchStarted, new MatchStartedMessage
                {
                    YourPlayerId = (byte)binding.PlayerId,
                    CurrentTurnPlayerId = (byte)_currentTurn,
                    BoardSize = _config.BoardSize,
                    YourCells = binding.Board.ToOwnCellMarks(),
                    TurnSecondsRemaining = GetTurnSecondsRemaining(),
                    TurnTimeoutSeconds = _config.TurnTimeoutSeconds,
                    TurnEndsAtRealtime = GetTurnEndsAtRealtime()
                });
            }
        }

        private void PassTurnDueToTimeout()
        {
            if (_phase != MatchPhase.Playing || _currentTurn == PlayerId.None)
                return;

            var timedOutPlayer = _currentTurn;
            if (_byPlayer.TryGetValue(timedOutPlayer, out var binding))
            {
                binding.PendingRequestId = null;
                binding.PendingCell = null;
            }

            var opponent = OpponentOf(timedOutPlayer);
            if (opponent == null)
                return;

            _currentTurn = opponent.PlayerId;
            RestartTurnClock();

            Broadcast(MessageType.TurnUpdate, new TurnUpdateMessage
            {
                CurrentTurnPlayerId = (byte)_currentTurn,
                TimedOut = true,
                TimedOutPlayerId = (byte)timedOutPlayer,
                TurnSecondsRemaining = GetTurnSecondsRemaining(),
                TurnTimeoutSeconds = _config.TurnTimeoutSeconds,
                TurnEndsAtRealtime = GetTurnEndsAtRealtime(),
                Reason = "Turn timeout"
            });
        }

        private void RestartTurnClock()
        {
            if (_config.TurnTimeoutSeconds <= 0f)
            {
                _turnEndsAt = float.PositiveInfinity;
                return;
            }

            _turnEndsAt = Now + _config.TurnTimeoutSeconds;
        }

        private void BroadcastTurnClock(string reason)
        {
            if (_config.TurnTimeoutSeconds <= 0f)
                return;

            Broadcast(MessageType.TurnUpdate, new TurnUpdateMessage
            {
                CurrentTurnPlayerId = (byte)_currentTurn,
                TimedOut = false,
                TimedOutPlayerId = 0,
                TurnSecondsRemaining = GetTurnSecondsRemaining(),
                TurnTimeoutSeconds = _config.TurnTimeoutSeconds,
                TurnEndsAtRealtime = GetTurnEndsAtRealtime(),
                Reason = reason
            });
        }

        private float GetTurnSecondsRemaining()
        {
            if (_config.TurnTimeoutSeconds <= 0f)
                return 0f;

            if (_phase == MatchPhase.PausedDisconnected && _pausedTurnRemaining >= 0f)
                return _pausedTurnRemaining;

            if (float.IsPositiveInfinity(_turnEndsAt))
                return 0f;

            return Mathf.Max(0f, _turnEndsAt - Now);
        }

        private float GetTurnEndsAtRealtime()
        {
            if (_config.TurnTimeoutSeconds <= 0f)
                return -1f;

            // While paused, clients must freeze on TurnSecondsRemaining (absolute
            // deadline would look wrong after receive delay).
            if (_phase == MatchPhase.PausedDisconnected)
                return -1f;

            if (float.IsPositiveInfinity(_turnEndsAt))
                return -1f;

            return _turnEndsAt;
        }

        private void SendSnapshot(PlayerBinding binding)
        {
            var opponent = OpponentOf(binding.PlayerId);
            var snapshot = new StateSnapshotMessage
            {
                YourPlayerId = (byte)binding.PlayerId,
                Phase = (byte)_phase,
                CurrentTurnPlayerId = (byte)_currentTurn,
                BoardSize = _config.BoardSize,
                YourCells = binding.Board != null
                    ? binding.Board.ToOwnCellMarks()
                    : new byte[_config.BoardSize * _config.BoardSize],
                OpponentFogCells = opponent?.Board != null
                    ? opponent.Board.ToFogCellMarks()
                    : new byte[_config.BoardSize * _config.BoardSize],
                GameOver = _phase == MatchPhase.Finished,
                WinnerPlayerId = (byte)_winner,
                HasPendingShot = !string.IsNullOrEmpty(binding.PendingRequestId),
                PendingRequestId = binding.PendingRequestId ?? string.Empty,
                PendingX = binding.PendingCell?.X ?? -1,
                PendingY = binding.PendingCell?.Y ?? -1,
                TurnSecondsRemaining = GetTurnSecondsRemaining(),
                TurnTimeoutSeconds = _config.TurnTimeoutSeconds,
                TurnEndsAtRealtime = GetTurnEndsAtRealtime()
            };

            Send(binding.PeerId, MessageType.StateSnapshot, snapshot);
        }

        private void Reject(PlayerBinding shooter, string requestId, RejectReason reason)
        {
            Send(shooter.PeerId, MessageType.FireRejected, new FireRejectedMessage
            {
                RequestId = requestId,
                Reason = (byte)reason
            });
        }

        private void NotifyOpponentConnection(PlayerId playerId, bool isConnected)
        {
            var opponent = OpponentOf(playerId);
            if (opponent == null || !opponent.Connected)
                return;

            Send(opponent.PeerId, MessageType.OpponentConnectionChanged, new OpponentConnectionChangedMessage
            {
                OpponentPlayerId = (byte)playerId,
                IsConnected = isConnected
            });
        }

        private PlayerId ResolveFirstTurn()
        {
            if (_config.FirstTurnPlayerId == (int)PlayerId.Player1)
                return PlayerId.Player1;
            if (_config.FirstTurnPlayerId == (int)PlayerId.Player2)
                return PlayerId.Player2;
            return UnityEngine.Random.value < 0.5f ? PlayerId.Player1 : PlayerId.Player2;
        }

        private PlayerBinding OpponentOf(PlayerId playerId)
        {
            var other = playerId == PlayerId.Player1 ? PlayerId.Player2 : PlayerId.Player1;
            return _byPlayer.TryGetValue(other, out var binding) ? binding : null;
        }

        private PlayerId AssignPlayerId(byte preferred)
        {
            if (preferred == (byte)PlayerId.Player1 && !_byPlayer.ContainsKey(PlayerId.Player1))
                return PlayerId.Player1;
            if (preferred == (byte)PlayerId.Player2 && !_byPlayer.ContainsKey(PlayerId.Player2))
                return PlayerId.Player2;
            if (!_byPlayer.ContainsKey(PlayerId.Player1))
                return PlayerId.Player1;
            return PlayerId.Player2;
        }

        private bool AllPlayersConnected()
        {
            foreach (var binding in _byPlayer.Values)
            {
                if (!binding.Connected)
                    return false;
            }

            return _byPlayer.Count == 2;
        }

        private void Broadcast<T>(MessageType type, T payload)
        {
            foreach (var binding in _byPlayer.Values)
            {
                if (binding.Connected)
                    Send(binding.PeerId, type, payload);
            }
        }

        private void Send<T>(string peerId, MessageType type, T payload)
        {
            var envelope = MessageSerializer.Wrap(type, payload, 0);
            _transport.SendToClient(peerId, envelope);
        }
    }
}
