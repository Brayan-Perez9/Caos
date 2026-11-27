using System;
using System.Collections.Generic;
using UnityEngine;

namespace Caos.Player
{
    /// <summary>
    /// Progreso local persistente del jugador. Guarda las monedas conseguidas
    /// en la mazmorra, las skins desbloqueadas y la última skin equipada.
    /// Se mantiene aparte de CharacterCustomization para que el atuendo siga
    /// viajando por red y el dinero nunca se sincronice ni se duplique.
    /// </summary>
    public static class CaosProfile
    {
        const int Version = 1;
        const string VersionKey = "Caos.Profile.Version";
        const string CoinsKey = "Caos.Profile.Coins";
        const string OwnedKey = "Caos.Profile.OwnedSkins";
        const string EquippedKey = "Caos.Profile.EquippedSkin";

        static bool _loaded;
        static int _coins;
        // Vacío = el jugador usa su combinación manual de PlayerPrefs.
        static string _equipped = string.Empty;
        static readonly HashSet<string> Owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static int Coins
        {
            get { EnsureLoaded(); return _coins; }
        }

        public static string EquippedSkinId
        {
            get { EnsureLoaded(); return _equipped; }
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            int storedVersion = PlayerPrefs.GetInt(VersionKey, 0);
            if (storedVersion != Version) PlayerPrefs.SetInt(VersionKey, Version);

            _coins = Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0));
            string owned = PlayerPrefs.GetString(OwnedKey, string.Empty);
            if (!string.IsNullOrEmpty(owned))
            {
                string[] ids = owned.Split(',');
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i].Trim();
                    if (!string.IsNullOrEmpty(id)) Owned.Add(id);
                }
            }

            Owned.Add("clasico");
            _equipped = PlayerPrefs.GetString(EquippedKey, string.Empty);
            if (!string.IsNullOrEmpty(_equipped) && !Owned.Contains(_equipped))
                _equipped = string.Empty;
        }

        static void Save()
        {
            var ids = new List<string>(Owned);
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            PlayerPrefs.SetString(OwnedKey, string.Join(",", ids));
            PlayerPrefs.SetString(EquippedKey, _equipped);
            PlayerPrefs.SetInt(CoinsKey, _coins);
            PlayerPrefs.SetInt(VersionKey, Version);
            PlayerPrefs.Save();
        }

        public static bool Owns(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            EnsureLoaded();
            return Owned.Contains(id.Trim());
        }

        /// <summary>Ingresa monedas al perfil local. Devuelve el total guardado.</summary>
        public static int AddCoins(int amount)
        {
            EnsureLoaded();
            if (amount != 0)
            {
                _coins = Mathf.Max(0, _coins + amount);
                Save();
            }
            return _coins;
        }

        /// <summary>
        /// Compra una skin. No se puede comprar dos veces y nunca deja el saldo
        /// en negativo. La skin comprada queda equipada al instante.
        /// </summary>
        public static bool TryBuy(CaosSkinDefinition skin)
        {
            if (skin == null) return false;
            EnsureLoaded();

            string id = skin.id;
            if (Owned.Contains(id)) return false;
            if (_coins < Mathf.Max(0, skin.price)) return false;

            _coins -= Mathf.Max(0, skin.price);
            Owned.Add(id);
            _equipped = id;
            Save();
            Debug.Log($"[Caos] SKIN comprada: {id} | monedas={_coins}");
            return true;
        }

        public static void Equip(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            EnsureLoaded();
            if (!Owned.Contains(id.Trim())) return;
            if (_equipped == id.Trim()) return;
            _equipped = id.Trim();
            Save();
            Debug.Log($"[Caos] SKIN equipada: {_equipped}");
        }

        /// <summary>
        /// Un cambio manual de ropa deja de marcar la skin anterior como activa,
        /// pero no la elimina de la colección del jugador.
        /// </summary>
        public static void ClearEquippedSkin()
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(_equipped)) return;
            _equipped = string.Empty;
            Save();
        }
    }
}
