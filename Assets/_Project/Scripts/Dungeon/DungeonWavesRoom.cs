using UnityEngine;
using Caos.Maps;

namespace Caos.Dungeon
{
    /// <summary>Oleadas: limpia cada grupo antes de que aparezca el siguiente.</summary>
    public class DungeonWavesRoom : DungeonRoom
    {
        int _wave;
        int _spawned;
        int _killed;
        bool _waveActive;
        float _delay;

        public override string ProgressText
        {
            get
            {
                if (Completed) return "Oleadas limpias";
                int total = Mathf.Max(1, Definition.waveCount);
                return $"Oleada {Mathf.Min(_wave, total)}/{total} · bajas {_killed}/{_spawned}";
            }
        }

        protected override void OnBegin()
        {
            _wave = 0;
            _spawned = 0;
            _killed = 0;
            _waveActive = false;
            _delay = 1.1f;
            Director.SetWeapon(Definition.weapon, Definition.element);
            Director.ShowBanner(Title + " — " + Objective, 2.6f);
        }

        protected override void OnTick(float deltaTime)
        {
            if (_waveActive)
            {
                if (_killed < _spawned) return;

                _waveActive = false;
                if (_wave >= Mathf.Max(1, Definition.waveCount))
                {
                    Director.ShowBanner("¡Mapa despejado!", 2.2f);
                    Complete();
                }
                else
                {
                    _delay = 1.8f;
                    Director.ShowBanner($"Oleada {_wave} limpia — prepara la siguiente", 1.8f);
                }
                return;
            }

            _delay -= deltaTime;
            if (_delay <= 0f) SpawnWave();
        }

        protected override void OnEnemyKilled(Vector3 position) => _killed++;

        void SpawnWave()
        {
            _wave++;
            _killed = 0;
            int requested = Mathf.Max(1, Definition.enemiesPerWave + _wave - 1);
            int spawned = 0;

            for (int i = 0; i < requested; i++)
            {
                if (Director.SpawnMonster(RandomPoint(3f),
                        46f + _wave * 6f, 3.1f + _wave * 0.12f, 12f + _wave * 1.5f) != null)
                {
                    spawned++;
                }
            }

            if (spawned == 0)
            {
                _wave = Mathf.Max(0, _wave - 1);
                _spawned = 0;
                _delay = 1f;
                return;
            }

            _spawned = spawned;
            _waveActive = true;
            ArenaAudio.Play(ArenaAudio.Sound.Boss, Center, 0.55f);
            Director.ShowBanner($"OLEADA {_wave}/{Mathf.Max(1, Definition.waveCount)}", 2f);
        }
    }
}
