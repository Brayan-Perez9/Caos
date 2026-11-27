using System.Collections.Generic;
using UnityEngine;
using Caos.Maps;
using Caos.Player;

namespace Caos.Dungeon
{
    /// <summary>Carrera: pasa los controles en orden antes de que acabe el tiempo.</summary>
    public class DungeonRaceRoom : DungeonRoom
    {
        readonly List<GameObject> _checkpoints = new List<GameObject>();

        int _index;
        float _remaining;

        public override string ProgressText => Completed
            ? "Carrera completada"
            : $"Controles {_index}/{_checkpoints.Count} · {Mathf.Max(0f, _remaining):0.0} s";

        public override float TimeRemaining => _remaining;

        protected override void OnBegin()
        {
            _checkpoints.Clear();
            _index = 0;
            _remaining = Definition.duration > 0f ? Definition.duration : 42f;

            int count = Mathf.Max(2, Definition.goal);
            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / (count + 1f);
                Vector3 position = Center
                    + Vector3.forward * Mathf.Lerp(-Radius + 4f, Radius - 4f, t)
                    + Vector3.right * Director.RandomRange(-Radius * 0.55f, Radius * 0.55f);

                GameObject marker = DungeonVisuals.Cylinder("Checkpoint",
                    position + Vector3.up * 0.35f, new Vector3(1.5f, 0.35f, 1.5f),
                    DungeonPalette.Checkpoint, transform, true, true);
                _checkpoints.Add(marker);
                TrackObject(marker);
            }

            AddSweeper(0.85f, 11f);
            AddOrbiter(5.5f, -1.35f);
            DungeonCannon.Place(Director, this, 1, 10f, 5f, 2f);
            Director.ShowBanner(Title + " — " + Objective, 2.6f);
        }

        protected override void OnTick(float deltaTime)
        {
            _remaining -= deltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                Fail("Se acabó el tiempo");
                return;
            }

            PlayerController player = Director.Player;
            if (player == null || _index >= _checkpoints.Count) return;

            GameObject current = _checkpoints[_index];
            if (Vector3.Distance(player.transform.position, current.transform.position) > 1.9f) return;

            AdvanceCheckpoint(current);
        }

        /// <summary>Control con trigger: solo cuenta si es el siguiente en orden.</summary>
        public void Reach(int value)
        {
            if (value == _index && _index < _checkpoints.Count &&
                !Completed && !Failed)
            {
                AdvanceCheckpoint(_checkpoints[_index]);
            }
        }

        void AdvanceCheckpoint(GameObject current)
        {
            DungeonVisuals.SetMaterial(current, DungeonPalette.CheckpointDone);
            current.transform.localScale *= 1.12f;
            _index++;

            if (_index < _checkpoints.Count)
            {
                ArenaAudio.Play(ArenaAudio.Sound.Upgrade, current.transform.position, 0.35f);
                return;
            }

            Director.ShowBanner("¡Todos los controles!", 2f);
            Complete();
        }

        void AddSweeper(float speed, float damage)
        {
            GameObject bar = DungeonVisuals.Box("RaceSweeper", Center + Vector3.up * 0.8f,
                new Vector3(0.45f, 0.45f, Radius * 1.45f),
                DungeonPalette.Hazard, transform, true, true);
            DungeonHazard hazard = bar.AddComponent<DungeonHazard>();
            hazard.Configure(Director, Center, Radius * 0.72f, speed, damage,
                DungeonHazardMode.Sweep, 0.7f);
            TrackObject(bar);
        }

        void AddOrbiter(float radius, float speed)
        {
            GameObject orb = DungeonVisuals.Sphere("RaceOrbiter", Center + Vector3.up * 0.85f,
                0.9f, DungeonPalette.Hazard, transform, true, true);
            DungeonHazard hazard = orb.AddComponent<DungeonHazard>();
            hazard.Configure(Director, Center, radius, speed, 14f,
                DungeonHazardMode.Orbit, 1.8f);
            TrackObject(orb);
        }
    }
}
