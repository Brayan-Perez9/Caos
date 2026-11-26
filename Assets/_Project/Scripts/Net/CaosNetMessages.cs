using System;
using System.Text;

namespace Caos.Net
{
    /// <summary>Mensajes de texto muy simples para la sala del lobby.</summary>
    public enum CaosMessageKind
    {
        Hello,
        Welcome,
        PeerJoined,
        PeerLeft,
        Chat,
        Start,
        Snapshot,
        Pose,
        Goal,
        BossHit,
        Weapon,
        Ping,
        Pong,
    }

    /// <summary>Codifica/decodifica mensajes como "Kind|payload" con un solo paquete TCP.</summary>
    public static class CaosNetMessages
    {
        public const int MaxLength = 4096;

        public static string Encode(CaosMessageKind kind, string payload = "")
        {
            string clean = payload ?? string.Empty;
            if (clean.Length > MaxLength - 32) clean = clean.Substring(0, MaxLength - 32);
            return $"{(int)kind}|{clean}";
        }

        public static bool TryDecode(string text, out CaosMessageKind kind, out string payload)
        {
            kind = default;
            payload = string.Empty;
            if (string.IsNullOrEmpty(text)) return false;

            int separator = text.IndexOf('|');
            if (separator <= 0) return false;
            if (!int.TryParse(text.Substring(0, separator), out int value)) return false;
            if (!Enum.IsDefined(typeof(CaosMessageKind), value)) return false;

            kind = (CaosMessageKind)value;
            payload = text.Substring(separator + 1);
            return true;
        }

        public static string Join(params string[] values) => string.Join("\u001f", values);
        public static string[] Split(string payload) =>
            (payload ?? string.Empty).Split(new[] { '\u001f' }, StringSplitOptions.None);

        public static byte[] ToBytes(string text) => Encoding.UTF8.GetBytes(text);
        public static string FromBytes(byte[] data, int count) => Encoding.UTF8.GetString(data, 0, count);
    }
}
