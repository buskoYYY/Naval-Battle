using System;
using System.Collections.Generic;
using NavalBattle.Messages;
using UnityEngine;

namespace NavalBattle.Transport
{
    /// <summary>
    /// In-process transport: ordered delivery while connected.
    /// Each peer has its own receive LatencyMs (S->C). C->S is immediate
    /// so a slow client does not stall the fast client's updates.
    /// Disconnect drops in-flight messages for that peer.
    /// </summary>
    public sealed class InProcessTransportHub : ITransportHub
    {
        private sealed class PendingMessage
        {
            public string TargetPeerId;
            public bool ToServer;
            public string FromPeerId;
            public NetworkEnvelope Envelope;
            public float DeliverAt;
            public float DelayMs;
            public string PeerIdForLog;
        }

        private sealed class ClientPeer : INetworkPeer
        {
            private readonly InProcessTransportHub _hub;
            private float _latencyMs;

            public ClientPeer(InProcessTransportHub hub, string peerId, float latencyMs)
            {
                _hub = hub;
                PeerId = peerId;
                _latencyMs = Mathf.Max(0f, latencyMs);
            }

            public string PeerId { get; }
            public bool IsConnected { get; set; }

            public float LatencyMs
            {
                get => _latencyMs;
                set => _latencyMs = Mathf.Max(0f, value);
            }

            public event Action<NetworkEnvelope> MessageReceived;
            public event Action Disconnected;
            public event Action Connected;

            public void Send(NetworkEnvelope envelope)
            {
                if (!IsConnected)
                    return;

                _hub.EnqueueToServer(PeerId, envelope);
            }

            public void Disconnect() => _hub.DisconnectPeer(PeerId);

            public void Connect() => _hub.ConnectPeer(PeerId);

            public void RaiseMessage(NetworkEnvelope envelope) => MessageReceived?.Invoke(envelope);

            public void RaiseDisconnected() => Disconnected?.Invoke();

            public void RaiseConnected() => Connected?.Invoke();
        }

        private readonly List<PendingMessage> _queue = new();
        private readonly Dictionary<string, ClientPeer> _peers = new();
        private Action<string, NetworkEnvelope> _serverHandler;
        private bool _alive = true;
        private int _nextSeq = 1;

        public float DefaultLatencyMs { get; set; }
        public bool LogEnabled { get; set; } = true;

        public event Action<string> LogLine;

        public INetworkPeer CreateClientPeer(string peerId, float? latencyMs = null)
        {
            if (_peers.ContainsKey(peerId))
                throw new InvalidOperationException($"Peer '{peerId}' already exists.");

            var peer = new ClientPeer(this, peerId, latencyMs ?? DefaultLatencyMs)
            {
                IsConnected = true
            };
            _peers[peerId] = peer;
            return peer;
        }

        public void BindServerHandler(Action<string, NetworkEnvelope> onClientMessage)
        {
            _serverHandler = onClientMessage;
        }

        public void SendToClient(string peerId, NetworkEnvelope envelope)
        {
            if (!_alive || !_peers.TryGetValue(peerId, out var peer) || !peer.IsConnected)
                return;

            if (envelope.Seq <= 0)
                envelope.Seq = _nextSeq++;

            Enqueue(new PendingMessage
            {
                TargetPeerId = peerId,
                ToServer = false,
                Envelope = envelope,
                DelayMs = peer.LatencyMs,
                PeerIdForLog = peerId,
                DeliverAt = Time.realtimeSinceStartup + peer.LatencyMs / 1000f
            });

            WriteLog($"[queue +{peer.LatencyMs:0}ms] {MessageSerializer.ToLogLine("S->C", peerId, envelope)}");
        }

        public void DisconnectPeer(string peerId)
        {
            if (!_peers.TryGetValue(peerId, out var peer) || !peer.IsConnected)
                return;

            peer.IsConnected = false;
            _queue.RemoveAll(m =>
                (!m.ToServer && m.TargetPeerId == peerId) ||
                (m.ToServer && m.FromPeerId == peerId));

            WriteLog($"[transport] disconnected {peerId}, in-flight dropped");
            peer.RaiseDisconnected();
        }

        public void ConnectPeer(string peerId)
        {
            if (!_peers.TryGetValue(peerId, out var peer))
                return;

            if (peer.IsConnected)
                return;

            peer.IsConnected = true;
            WriteLog($"[transport] connected {peerId}");
            peer.RaiseConnected();
        }

        public void Shutdown()
        {
            _alive = false;
            _queue.Clear();
            _serverHandler = null;

            foreach (var peer in _peers.Values)
            {
                if (peer.IsConnected)
                {
                    peer.IsConnected = false;
                    peer.RaiseDisconnected();
                }
            }

            _peers.Clear();
        }

        public void Tick(float nowSeconds)
        {
            if (!_alive || _queue.Count == 0)
                return;

            for (var i = 0; i < _queue.Count;)
            {
                var msg = _queue[i];
                if (msg.DeliverAt > nowSeconds)
                {
                    i++;
                    continue;
                }

                _queue.RemoveAt(i);
                Deliver(msg);
            }
        }

        private void EnqueueToServer(string fromPeerId, NetworkEnvelope envelope)
        {
            if (!_alive || !_peers.TryGetValue(fromPeerId, out var peer))
                return;

            if (envelope.Seq <= 0)
                envelope.Seq = _nextSeq++;

            Enqueue(new PendingMessage
            {
                ToServer = true,
                FromPeerId = fromPeerId,
                Envelope = envelope,
                // Upload is not delayed: otherwise the shooter's latency stalls BOTH
                // clients until FireRequest arrives, which looks like a shared delay.
                DelayMs = 0f,
                PeerIdForLog = fromPeerId,
                DeliverAt = Time.realtimeSinceStartup
            });

            WriteLog($"[queue +0ms upload] {MessageSerializer.ToLogLine("C->S", fromPeerId, envelope)}");
        }

        private void Enqueue(PendingMessage message) => _queue.Add(message);

        private void Deliver(PendingMessage message)
        {
            if (!_alive)
                return;

            if (message.ToServer)
            {
                if (!_peers.TryGetValue(message.FromPeerId, out var peer) || !peer.IsConnected)
                    return;

                WriteLog($"[deliver after {message.DelayMs:0}ms] {MessageSerializer.ToLogLine("C->S", message.PeerIdForLog, message.Envelope)}");
                _serverHandler?.Invoke(message.FromPeerId, message.Envelope);
                return;
            }

            if (!_peers.TryGetValue(message.TargetPeerId, out var target) || !target.IsConnected)
                return;

            WriteLog($"[deliver after {message.DelayMs:0}ms] {MessageSerializer.ToLogLine("S->C", message.PeerIdForLog, message.Envelope)}");
            target.RaiseMessage(message.Envelope);
        }

        private void WriteLog(string line)
        {
            if (!LogEnabled)
                return;

            LogLine?.Invoke(line);
            Debug.Log(line);
        }
    }
}
