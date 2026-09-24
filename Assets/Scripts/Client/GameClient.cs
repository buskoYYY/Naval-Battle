using System;
using NavalBattle.Messages;
using NavalBattle.Shared;
using NavalBattle.Transport;
using UnityEngine;

namespace NavalBattle.Client
{
    public sealed class ClientBoardView
    {
        public int Size { get; private set; }
        public CellMark[] OwnCells { get; private set; } = Array.Empty<CellMark>();
        public CellMark[] OpponentFog { get; private set; } = Array.Empty<CellMark>();

        public void Reset(int size)
        {
            Size = size;
            OwnCells = new CellMark[size * size];
            OpponentFog = new CellMark[size * size];
            for (var i = 0; i < OwnCells.Length; i++)
            {
                OwnCells[i] = CellMark.Empty;
                OpponentFog[i] = CellMark.Unknown;
            }
        }

        public void ApplyOwnMarks(byte[] marks)
        {
            EnsureSize(marks.Length);
            for (var i = 0; i < marks.Length; i++)
                OwnCells[i] = (CellMark)marks[i];
        }

        public void ApplyFogMarks(byte[] marks)
        {
            EnsureSize(marks.Length);
            for (var i = 0; i < marks.Length; i++)
                OpponentFog[i] = (CellMark)marks[i];
        }

        public void SetOpponentPending(int x, int y)
        {
            if (!InBounds(x, y))
                return;
            OpponentFog[Index(x, y)] = CellMark.Pending;
        }

        public void ClearPending()
        {
            for (var i = 0; i < OpponentFog.Length; i++)
            {
                if (OpponentFog[i] == CellMark.Pending)
                    OpponentFog[i] = CellMark.Unknown;
            }
        }

        public void ApplyShotAsShooter(ShotResultMessage msg)
        {
            ClearPending();
            if (msg.Outcome == (byte)ShotOutcome.Sunk && msg.SunkCellsX != null)
            {
                for (var i = 0; i < msg.SunkCellsX.Length; i++)
                    OpponentFog[Index(msg.SunkCellsX[i], msg.SunkCellsY[i])] = CellMark.Sunk;
            }
            else
            {
                OpponentFog[Index(msg.X, msg.Y)] = msg.Outcome switch
                {
                    (byte)ShotOutcome.Hit => CellMark.Hit,
                    (byte)ShotOutcome.Sunk => CellMark.Sunk,
                    _ => CellMark.Miss
                };
            }
        }

        public void ApplyShotAsTarget(ShotResultMessage msg)
        {
            if (msg.Outcome == (byte)ShotOutcome.Sunk && msg.SunkCellsX != null)
            {
                for (var i = 0; i < msg.SunkCellsX.Length; i++)
                    OwnCells[Index(msg.SunkCellsX[i], msg.SunkCellsY[i])] = CellMark.Sunk;
            }
            else
            {
                OwnCells[Index(msg.X, msg.Y)] = msg.Outcome switch
                {
                    (byte)ShotOutcome.Hit => CellMark.Hit,
                    (byte)ShotOutcome.Sunk => CellMark.Sunk,
                    _ => CellMark.Miss
                };
            }
        }

        private void EnsureSize(int flatLength)
        {
            var size = Mathf.RoundToInt(Mathf.Sqrt(flatLength));
            if (Size != size || OwnCells.Length != flatLength)
                Reset(size);
        }

        private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

        private int Index(int x, int y) => y * Size + x;
    }

    public sealed class GameClient
    {
        private readonly INetworkPeer _peer;
        private readonly PlayerId _preferredId;
        private bool _alive = true;

        public PlayerId PlayerId { get; private set; } = PlayerId.None;
        public MatchPhase Phase { get; private set; } = MatchPhase.WaitingForPlayers;
        public PlayerId CurrentTurn { get; private set; } = PlayerId.None;
        public PlayerId Winner { get; private set; } = PlayerId.None;
        public string StatusText { get; private set; } = "Connecting...";
        public bool HasPendingShot { get; private set; }
        public string PendingRequestId { get; private set; }
        public ClientBoardView BoardView { get; } = new();

        public event Action StateChanged;
        public event Action<string> MessageLogged;

        public bool IsMyTurn =>
            Phase == MatchPhase.Playing &&
            PlayerId != PlayerId.None &&
            CurrentTurn == PlayerId &&
            !HasPendingShot;

        public GameClient(INetworkPeer peer, PlayerId preferredId)
        {
            _peer = peer;
            _preferredId = preferredId;
            _peer.MessageReceived += OnMessage;
            _peer.Disconnected += OnDisconnected;
            _peer.Connected += OnConnected;
        }

        public void Start()
        {
            Send(MessageType.JoinRequest, new JoinRequest
            {
                PreferredPlayerId = (byte)_preferredId
            });
            StatusText = "Joining...";
            RaiseState();
        }

        public void Shutdown()
        {
            _alive = false;
            _peer.MessageReceived -= OnMessage;
            _peer.Disconnected -= OnDisconnected;
            _peer.Connected -= OnConnected;
        }

        public bool TryFire(int x, int y)
        {
            if (!_alive || !_peer.IsConnected)
                return false;

            if (!IsMyTurn)
                return false;

            if (BoardView.Size > 0)
            {
                var idx = y * BoardView.Size + x;
                if (idx < 0 || idx >= BoardView.OpponentFog.Length)
                    return false;
                var mark = BoardView.OpponentFog[idx];
                if (mark is CellMark.Miss or CellMark.Hit or CellMark.Sunk or CellMark.Pending)
                    return false;
            }

            var requestId = Guid.NewGuid().ToString("N");
            HasPendingShot = true;
            PendingRequestId = requestId;
            BoardView.SetOpponentPending(x, y);
            StatusText = $"Shot sent ({x},{y})...";
            RaiseState();

            Send(MessageType.FireRequest, new FireRequest
            {
                RequestId = requestId,
                X = x,
                Y = y
            });

            return true;
        }

        public void RequestReconnect()
        {
            if (!_peer.IsConnected)
                _peer.Connect();

            Send(MessageType.ReconnectRequest, new ReconnectRequest
            {
                PlayerId = (byte)(PlayerId == PlayerId.None ? _preferredId : PlayerId),
                LastReceivedSeq = 0
            });
        }

        private void OnConnected()
        {
            if (!_alive)
                return;

            StatusText = "Connected. Syncing...";
            RequestReconnect();
            RaiseState();
        }

        private void OnDisconnected()
        {
            if (!_alive)
                return;

            HasPendingShot = false;
            PendingRequestId = null;
            BoardView.ClearPending();
            StatusText = "Disconnected";
            RaiseState();
        }

        private void OnMessage(NetworkEnvelope envelope)
        {
            if (!_alive)
                return;

            MessageLogged?.Invoke(MessageSerializer.ToLogLine("RECV", _peer.PeerId, envelope));

            switch (envelope.Type)
            {
                case MessageType.Welcome:
                    HandleWelcome(MessageSerializer.Unwrap<WelcomeMessage>(envelope));
                    break;
                case MessageType.MatchStarted:
                    HandleMatchStarted(MessageSerializer.Unwrap<MatchStartedMessage>(envelope));
                    break;
                case MessageType.FireRejected:
                    HandleRejected(MessageSerializer.Unwrap<FireRejectedMessage>(envelope));
                    break;
                case MessageType.ShotResult:
                    HandleShotResult(MessageSerializer.Unwrap<ShotResultMessage>(envelope));
                    break;
                case MessageType.StateSnapshot:
                    HandleSnapshot(MessageSerializer.Unwrap<StateSnapshotMessage>(envelope));
                    break;
                case MessageType.OpponentConnectionChanged:
                    HandleOpponentConnection(MessageSerializer.Unwrap<OpponentConnectionChangedMessage>(envelope));
                    break;
                case MessageType.MatchFinished:
                    HandleFinished(MessageSerializer.Unwrap<MatchFinishedMessage>(envelope));
                    break;
                case MessageType.Error:
                    StatusText = MessageSerializer.Unwrap<ErrorMessage>(envelope).Text;
                    RaiseState();
                    break;
            }
        }

        private void HandleWelcome(WelcomeMessage msg)
        {
            PlayerId = (PlayerId)msg.PlayerId;
            Phase = (MatchPhase)msg.Phase;
            StatusText = $"Joined as {PlayerId}";
            RaiseState();
        }

        private void HandleMatchStarted(MatchStartedMessage msg)
        {
            PlayerId = (PlayerId)msg.YourPlayerId;
            CurrentTurn = (PlayerId)msg.CurrentTurnPlayerId;
            Phase = MatchPhase.Playing;
            BoardView.Reset(msg.BoardSize);
            BoardView.ApplyOwnMarks(msg.YourCells);
            StatusText = CurrentTurn == PlayerId ? "Your turn" : "Opponent turn";
            RaiseState();
        }

        private void HandleRejected(FireRejectedMessage msg)
        {
            if (msg.RequestId == PendingRequestId)
            {
                HasPendingShot = false;
                PendingRequestId = null;
                BoardView.ClearPending();
            }

            StatusText = $"Rejected: {(RejectReason)msg.Reason}";
            RaiseState();
        }

        private void HandleShotResult(ShotResultMessage msg)
        {
            var shooter = (PlayerId)msg.ShooterPlayerId;
            if (shooter == PlayerId)
            {
                if (msg.RequestId == PendingRequestId)
                {
                    HasPendingShot = false;
                    PendingRequestId = null;
                }

                BoardView.ApplyShotAsShooter(msg);
            }
            else
            {
                BoardView.ApplyShotAsTarget(msg);
            }

            CurrentTurn = (PlayerId)msg.NextTurnPlayerId;
            if (msg.GameOver)
            {
                Phase = MatchPhase.Finished;
                Winner = (PlayerId)msg.WinnerPlayerId;
                StatusText = Winner == PlayerId ? "You win!" : "You lose";
            }
            else
            {
                StatusText = CurrentTurn == PlayerId ? "Your turn" : "Opponent turn";
            }

            RaiseState();
        }

        private void HandleSnapshot(StateSnapshotMessage msg)
        {
            PlayerId = (PlayerId)msg.YourPlayerId;
            Phase = (MatchPhase)msg.Phase;
            CurrentTurn = (PlayerId)msg.CurrentTurnPlayerId;
            Winner = (PlayerId)msg.WinnerPlayerId;
            BoardView.Reset(msg.BoardSize);
            BoardView.ApplyOwnMarks(msg.YourCells);
            BoardView.ApplyFogMarks(msg.OpponentFogCells);

            HasPendingShot = msg.HasPendingShot;
            PendingRequestId = msg.PendingRequestId;
            if (msg.HasPendingShot)
                BoardView.SetOpponentPending(msg.PendingX, msg.PendingY);

            StatusText = Phase switch
            {
                MatchPhase.Finished => Winner == PlayerId ? "You win!" : "You lose",
                MatchPhase.PausedDisconnected => "Paused: waiting reconnect",
                MatchPhase.Playing => CurrentTurn == PlayerId ? "Your turn" : "Opponent turn",
                _ => "Waiting..."
            };
            RaiseState();
        }

        private void HandleOpponentConnection(OpponentConnectionChangedMessage msg)
        {
            StatusText = msg.IsConnected ? "Opponent reconnected" : "Opponent disconnected";
            if (!msg.IsConnected && Phase == MatchPhase.Playing)
                Phase = MatchPhase.PausedDisconnected;
            if (msg.IsConnected && Phase == MatchPhase.PausedDisconnected)
                Phase = MatchPhase.Playing;
            RaiseState();
        }

        private void HandleFinished(MatchFinishedMessage msg)
        {
            Phase = MatchPhase.Finished;
            Winner = (PlayerId)msg.WinnerPlayerId;
            StatusText = Winner == PlayerId ? "You win!" : "You lose";
            RaiseState();
        }

        private void Send<T>(MessageType type, T payload)
        {
            var envelope = MessageSerializer.Wrap(type, payload, 0);
            MessageLogged?.Invoke(MessageSerializer.ToLogLine("SEND", _peer.PeerId, envelope));
            _peer.Send(envelope);
        }

        private void RaiseState() => StateChanged?.Invoke();
    }
}
