using System;
using UnityEngine;

namespace NavalBattle.Messages
{
    public static class MessageSerializer
    {
        public static NetworkEnvelope Wrap<T>(MessageType type, T payload, int seq)
        {
            return new NetworkEnvelope
            {
                Type = type,
                Seq = seq,
                PayloadJson = JsonUtility.ToJson(payload),
                SentUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static T Unwrap<T>(NetworkEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            return JsonUtility.FromJson<T>(envelope.PayloadJson);
        }

        public static string ToLogLine(string direction, string endpointId, NetworkEnvelope envelope)
        {
            return $"[{DateTime.Now:HH:mm:ss.fff}] {direction} {endpointId} seq={envelope.Seq} {envelope.Type} {envelope.PayloadJson}";
        }
    }
}
