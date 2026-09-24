using System;
using System.Collections.Generic;
using NavalBattle.Messages;
using NavalBattle.Transport;

namespace NavalBattle.Tests
{
    /// <summary>
    /// Immediate in-memory transport for EditMode server tests.
    /// </summary>
    public sealed class FakeTransportHub : ITransportHub
    {
        private Action<string, NetworkEnvelope> _serverHandler;
        private int _seq = 1;

        public float DefaultLatencyMs { get; set; }
        public bool LogEnabled { get; set; }

        public event Action<string> LogLine;

        public List<(string PeerId, NetworkEnvelope Envelope)> SentToClients { get; } = new();

        public INetworkPeer CreateClientPeer(string peerId, float? latencyMs = null) =>
            throw new NotSupportedException("Not needed for server unit tests.");

        public void BindServerHandler(Action<string, NetworkEnvelope> onClientMessage)
        {
            _serverHandler = onClientMessage;
        }

        public void SendToClient(string peerId, NetworkEnvelope envelope)
        {
            if (envelope.Seq <= 0)
                envelope.Seq = _seq++;

            SentToClients.Add((peerId, envelope));
            LogLine?.Invoke($"S->C {peerId} {envelope.Type}");
        }

        public void DisconnectPeer(string peerId)
        {
        }

        public void ConnectPeer(string peerId)
        {
        }

        public void Shutdown()
        {
            _serverHandler = null;
            SentToClients.Clear();
        }

        public void Tick(float nowSeconds)
        {
        }

        public void DeliverFromClient(string peerId, NetworkEnvelope envelope)
        {
            if (envelope.Seq <= 0)
                envelope.Seq = _seq++;

            _serverHandler?.Invoke(peerId, envelope);
        }

        public void DeliverFromClient<T>(string peerId, MessageType type, T payload)
        {
            DeliverFromClient(peerId, MessageSerializer.Wrap(type, payload, 0));
        }

        public List<T> PayloadsTo<T>(string peerId, MessageType type)
        {
            var list = new List<T>();
            foreach (var (id, envelope) in SentToClients)
            {
                if (id == peerId && envelope.Type == type)
                    list.Add(MessageSerializer.Unwrap<T>(envelope));
            }

            return list;
        }

        public T LastPayloadTo<T>(string peerId, MessageType type)
        {
            for (var i = SentToClients.Count - 1; i >= 0; i--)
            {
                var (id, envelope) = SentToClients[i];
                if (id == peerId && envelope.Type == type)
                    return MessageSerializer.Unwrap<T>(envelope);
            }

            throw new InvalidOperationException($"No {type} sent to {peerId}");
        }

        public int CountTo(string peerId, MessageType type)
        {
            var count = 0;
            foreach (var (id, envelope) in SentToClients)
            {
                if (id == peerId && envelope.Type == type)
                    count++;
            }

            return count;
        }
    }
}
