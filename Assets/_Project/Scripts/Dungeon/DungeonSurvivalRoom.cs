using UnityEngine;

namespace Caos.Dungeon
{
    /// <summary>Supervivencia: resiste el tiempo entre trampas y oleadas cortas.</summary>
    public class DungeonSurvivalRoom : DungeonRoom
    {
        float _remaining;
        float _nextSpawn;

        public override string ProgressText => Completed
            ? "Supervivencia completada" : $"Resiste {Mathf.Max(0f, _remaining):0.0} s";

        public override float TimeRemaining => _remaining;

        protected override void OnBegin()
        {
            _remaining = Definition.duration > 0f ? Definition.duration : 30f;
            _nextSpawn = Time.time + 2.4f;

            AddHazard(DungeonHazardMode.Orbit, 4.2f, 1.5f, 13f,
                new Vector3(0.5f, 0.5f, 0.5f), 0f);
            AddHazard(DungeonHazardMode.Orbit, 7.2f, -1.1f, 15f,
                new Vector3(0.62f, 0.62f, 0.62f), 2.2f);
            AddHazard(DungeonHazardMode.Sweep, Radius * 0.75f, 0.95f, 12f,
                new Vector3(0.42f, 0.42f, Radius * 1.5f), 1.1f);

            Director.ShowBanner(Title + " — " + Objective, 2.6f);
        }

        protected override void OnTick(float deltaTime)
        {
            _remaining -= deltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                Director.ShowBanner("¡Has sobrevivido!", 2.2f);
                Complete();
                return;
            }

            if (Time.time < _nextSpawn || Director.Bridge.ActiveEnemyCount >= 4) return;
            _nextSpawn = Time.time + 4.5f;
            Director.SpawnMonster(RandomPoint(3f), 48f, 3.3f, 13f);
        }

        void AddHazard(DungeonHazardMode mode, float radius, float speed, float damage,
            Vector3 scale, float phase)
        {
            GameObject go = DungeonVisuals.Box("Hazard", Center + Vector3.up * 0.85f,
                scale, DungeonPalette.Hazard, transform, true, true);
            DungeonHazard hazard = go.AddComponent<DungeonHazard>();
            hazard.Configure(Director, Center, radius, speed, damage, mode, phase);
            TrackObject(go);
        }
    }
}
