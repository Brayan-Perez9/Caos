using UnityEngine;

namespace Caos.Dungeon
{
    /// <summary>
    /// Supervivencia: resiste el tiempo entre trampas, oleadas cortas y
    /// lanzadores de rayos. El techo de enemigos crece con el tamaño del grupo.
    /// </summary>
    public class DungeonSurvivalRoom : DungeonRoom
    {
        float _remaining;
        float _total;
        float _nextSpawn;
        float _nextSurge;

        public override string ProgressText => Completed
            ? "Supervivencia completada" : $"Resiste {Mathf.Max(0f, _remaining):0.0} s";

        public override float TimeRemaining => _remaining;

        protected override void OnBegin()
        {
            _total = Mathf.Max(32f, Definition.duration > 0f ? Definition.duration : 32f);
            _remaining = _total;
            _nextSpawn = Time.time + 1.8f;
            _nextSurge = Time.time + _total * 0.42f;

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

            if (Time.time >= _nextSurge)
            {
                _nextSurge = Time.time + Mathf.Max(8f, _total * 0.33f);
                int burst = 2 + Director.PartySize - 1;
                for (int i = 0; i < burst; i++)
                    SpawnOne(i == 0 ? DungeonEnemyKind.Lanzador : DungeonEnemyKind.Normal);
                Director.ShowBanner("¡REFUERZOS ENEMIGOS!", 1.8f);
            }

            int cap = Mathf.Min(12, 6 + Director.PartySize);
            if (Time.time < _nextSpawn || Director.Bridge.ActiveEnemyCount >= cap) return;
            _nextSpawn = Time.time + Mathf.Max(2.1f, 3.4f - 0.35f * (Director.PartySize - 1));

            int roll = Director.RandomInt(0, 7);
            DungeonEnemyKind kind = roll == 0 ? DungeonEnemyKind.Elite
                : roll <= 2 ? DungeonEnemyKind.Lanzador : DungeonEnemyKind.Normal;
            SpawnOne(kind);
        }

        void SpawnOne(DungeonEnemyKind kind)
        {
            float scale = Director.DifficultyScale;
            float health = 70f * scale;
            float speed = 3.3f;
            float damage = 13f;

            if (kind == DungeonEnemyKind.Elite)
            {
                health *= 2f;
                speed *= 0.9f;
                damage *= 1.4f;
            }
            else if (kind == DungeonEnemyKind.Lanzador)
            {
                health *= 0.9f;
                speed *= 0.9f;
                damage *= 1.1f;
            }

            Director.SpawnVariant(RandomPoint(3f), kind, health, speed, damage);
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
