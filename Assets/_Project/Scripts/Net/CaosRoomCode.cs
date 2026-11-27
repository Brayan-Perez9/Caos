using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
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
            var addresses = LocalAddresses();
            return addresses != null && addresses.Count > 0 ? addresses[0] : "127.0.0.1";
        }

        /// <summary>
        /// Todas las IPv4 utilizables del equipo. Un host puede tener Ethernet y
        /// Wi-Fi a la vez; si el código toma la tarjeta equivocada, el invitado
        /// puede conectar con otra de estas IP seguida del puerto.
        /// </summary>
        public static IReadOnlyList<string> LocalAddresses()
        {
            var found = new List<string>();

            try
            {
                foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    AddAddress(found, address);
            }
            catch
            {
                // Sin DNS todavía podemos leer las tarjetas de red.
            }

            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var info in nic.GetIPProperties().UnicastAddresses)
                        AddAddress(found, info.Address);
                }
            }
            catch
            {
                // Plataforma sin información de interfaces: usamos DNS.
            }

            if (found.Count == 0) found.Add("127.0.0.1");
            return found;
        }

        static void AddAddress(List<string> found, IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork) return;

            string text = address.ToString();
            if (text == "127.0.0.1" || text.StartsWith("169.254.")) return;
            if (!found.Contains(text)) found.Add(text);
        }

        public static bool TryParse(string input, out string host, out int port)
        {
            host = string.Empty;
            port = 7777;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string original = input.Trim();

            // Formato directo: 192.168.1.5:7777 o solo 192.168.1.5.
            // Útil cuando el código apunta a otra tarjeta de red del host.
            if (TryParseDirect(original, out host, out port)) return true;

            string text = original.ToUpperInvariant()
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

        static bool TryParseDirect(string text, out string host, out int port)
        {
            host = string.Empty;
            port = 7777;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string addressText = text;
            int colon = text.LastIndexOf(':');
            if (colon >= 0)
            {
                addressText = text.Substring(0, colon);
                if (!int.TryParse(text.Substring(colon + 1), out port) ||
                    port < 1 || port > 65535) return false;
            }

            if (!IPAddress.TryParse(addressText, out var address) ||
                address.AddressFamily != AddressFamily.InterNetwork) return false;

            host = address.ToString();
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
