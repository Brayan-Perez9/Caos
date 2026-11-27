using UnityEngine;
using Caos.Player;

namespace Caos.Dungeon
{
    /// <summary>Sala de entrada: enseña el control y abre la primera puerta.</summary>
    public class DungeonEntranceRoom : DungeonRoom
    {
        public override string ProgressText => Completed
            ? "Puerta abierta" : "Llega a la puerta luminosa";

        protected override void OnBegin()
        {
            Director.ShowBanner("MAZMORRA DEL CAOS — " + Title, 3f);

            Vector3 coinPosition = Center + Vector3.right * 3f
                + Vector3.forward * 2f + Vector3.up * 0.9f;
            GameObject coin = DungeonVisuals.Sphere("Bienvenida", coinPosition, 0.62f,
                DungeonPalette.Coin, transform, true, true);
            DungeonPickup pickup = coin.AddComponent<DungeonPickup>();
            pickup.Configure(Director, DungeonPickupKind.Coin, 6);
            TrackObject(coin);
        }

        protected override void OnTick(float deltaTime)
        {
            PlayerController player = Director.Player;
            if (player == null) return;
            if (Vector3.Distance(player.transform.position, ExitPoint) < 2.2f) Complete();
        }
    }
}
