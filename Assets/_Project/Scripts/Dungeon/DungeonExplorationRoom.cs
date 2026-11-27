using UnityEngine;
using Caos.Maps;

namespace Caos.Dungeon
{
    /// <summary>Exploración: recoge los cofres escondidos para abrir la salida.</summary>
    public class DungeonExplorationRoom : DungeonRoom
    {
        int _needed;
        int _found;

        public override string ProgressText => Completed
            ? "Salida abierta" : $"Cofres {_found}/{_needed}";

        protected override void OnBegin()
        {
            _needed = Mathf.Max(1, Definition.goal);
            _found = 0;

            for (int i = 0; i < _needed; i++)
            {
                Vector3 position = RandomPoint(3.2f);
                GameObject chest = DungeonVisuals.Box("Chest", position + Vector3.up * 0.4f,
                    new Vector3(0.95f, 0.72f, 0.75f), DungeonPalette.Chest,
                    transform, true, true);
                DungeonPickup pickup = chest.AddComponent<DungeonPickup>();
                pickup.Configure(Director, DungeonPickupKind.Chest,
                    Director.Config.roomClearCoins);
                TrackObject(chest);
            }

            // Un arma suelta recompensa la exploración sin romper el combate.
            GameObject weapon = DungeonVisuals.Sphere("WeaponPickup",
                RandomPoint(3.5f) + Vector3.up * 0.5f, 0.55f,
                DungeonPalette.Weapon, transform, true, true);
            DungeonPickup pickupWeapon = weapon.AddComponent<DungeonPickup>();
            pickupWeapon.Configure(Director, DungeonPickupKind.Weapon, 0,
                Definition.weapon, Definition.element);
            TrackObject(weapon);

            DungeonCannon.Place(Director, this, 1, 9f, 5.5f, 1.6f);
            Director.ShowBanner(Title + " — " + Objective, 2.6f);
        }

        protected override void OnPickup(DungeonPickup pickup)
        {
            if (pickup.Kind != DungeonPickupKind.Chest) return;

            _found++;
            ArenaAudio.Play(ArenaAudio.Sound.Upgrade, pickup.transform.position, 0.45f);
            if (_found >= _needed)
            {
                Director.ShowBanner("¡Todos los cofres! Puerta abierta", 2.2f);
                Complete();
            }
        }
    }
}
