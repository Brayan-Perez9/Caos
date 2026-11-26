using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Caos.Net
{
    /// <summary>
    /// Código de sala generado por el juego. Codifica la IP y el puerto del
    /// host en 10 caracteres: CAOS-XXXXXXXXXX. El invitado solo escribe eso.
    /// No necesita servidor externo: funciona en la misma LAN/Wi-Fi.
    /// </summary>
    public static class CaosRoomCode
    {
        const string Alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // 34 símbolos
        const int CodeLength = 10;

        public static string Build(string host, int port)
        {
            if (!IPAddress.TryParse(host, out var address) ||
                address.AddressFamily != AddressFamily.InterNetwork)
            {
                address = IPAddress.Loopback;
            }

            byte[] bytes = address.GetAddressBytes();
            uint ipv4 = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) |
                        ((uint)bytes[2] << 8) | bytes[3];

            ulong value = ((ulong)ipv4 << 16) | (uint)Mathf.Clamp(port, 1, 65535);
            var chars = new char[CodeLength];
            for (int i = CodeLength - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[(int)(value % (uint)Alphabet.Length)];
                value /= (uint)Alphabet.Length;
            }

            return "CAOS-" + new string(chars);
        }

        public static string LocalAddress()
        {
            try
            {
                foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string text = address.ToString();
                    if (text == "127.0.0.1" || text.StartsWith("169.254.")) continue;
                    return text;
                }
            }
            catch
            {
                // Sin DNS: el código seguirá siendo válido para la misma máquina.
            }

            return "127.0.0.1";
        }

        public static bool TryParse(string input, out string host, out int port)
        {
            host = string.Empty;
            port = 7777;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string text = input.Trim().ToUpperInvariant()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty);

            if (text.StartsWith("CAOS", StringComparison.Ordinal))
                text = text.Substring(4);
            if (text.Length != CodeLength) return false;

            ulong value = 0;
            foreach (char character in text)
            {
                int digit = Alphabet.IndexOf(character);
                if (digit < 0) return false;
                value = value * (uint)Alphabet.Length + (uint)digit;
            }

            port = (int)(value & 0xFFFFu);
            uint ipv4 = (uint)(value >> 16);

            if (port < 1 || port > 65535 || ipv4 == 0u || ipv4 == 0xFFFFFFFFu) return false;

            host = $"{ipv4 >> 24}.{(ipv4 >> 16) & 255}.{(ipv4 >> 8) & 255}.{ipv4 & 255}";
            return true;
        }

        /// <summary>Permite abrir el juego con -code=CAOS-XXXXXXXXXX.</summary>
        public static bool TryParseCommandLine(out string host, out int port)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument != null &&
                    argument.StartsWith("-code=", StringComparison.OrdinalIgnoreCase) &&
                    TryParse(argument.Substring(6), out host, out port))
                {
                    return true;
                }
            }

            host = string.Empty;
            port = 7777;
            return false;
        }
    }
}
