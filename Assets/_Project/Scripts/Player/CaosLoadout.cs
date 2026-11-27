using UnityEngine;
using Caos.Maps;

namespace Caos.Player
{
    /// <summary>
    /// Arma elegida al comenzar. La guarda el lobby y la leen los directores de
    /// arena y mazmorra, para que la elección sobreviva al cambio de escena y a
    /// un reinicio del juego sin tocar el payload de red.
    /// </summary>
    public static class CaosLoadout
    {
        const string WeaponKey = "Caos.StartWeapon";

        public static ArenaWeaponKind StartingWeapon
        {
            get
            {
                int stored = PlayerPrefs.GetInt(WeaponKey, (int)ArenaWeaponKind.Pistola);
                return stored == (int)ArenaWeaponKind.Espada
                    ? ArenaWeaponKind.Espada
                    : ArenaWeaponKind.Pistola;
            }
            set
            {
                ArenaWeaponKind kind = value == ArenaWeaponKind.Espada
                    ? ArenaWeaponKind.Espada
                    : ArenaWeaponKind.Pistola;
                PlayerPrefs.SetInt(WeaponKey, (int)kind);
                PlayerPrefs.Save();
            }
        }

        public static ArenaElement StartingElement => StartingWeapon == ArenaWeaponKind.Espada
            ? ArenaElement.Rayo
            : ArenaElement.Normal;
    }
}
