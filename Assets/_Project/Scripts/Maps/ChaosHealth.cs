using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Vida reutilizable para el jugador, los monstruos y el jefe.</summary>
    public class ChaosHealth : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] bool isPlayer;

        float _current;

        public float Current => _current;
        public float Max => maxHealth;
        public bool IsDead => _current <= 0f;
        public bool IsPlayer => isPlayer;

        public void Configure(float maximum, bool player)
        {
            maxHealth = maximum;
            isPlayer = player;
            _current = maximum;
        }

        void Awake() => _current = maxHealth;

        public void TakeDamage(float amount, string source = "")
        {
            if (IsDead || amount <= 0f) return;

            _current = Mathf.Max(0f, _current - amount);

            if (isPlayer)
                FindFirstObjectByType<ChaosExpressMission>()?.ShowBanner($"¡AUCH! -{amount:0} {source}");

            if (IsDead) GetComponent<ChaosBoss>()?.Die();
        }

        public void Heal(float amount) => _current = Mathf.Min(maxHealth, _current + amount);
        public void ResetHealth() => _current = maxHealth;

        /// <summary>Aumenta la vida máxima sin reiniciar la vida actual.</summary>
        public void IncreaseMax(float amount)
        {
            maxHealth += amount;
            _current = Mathf.Min(maxHealth, _current + amount);
        }

        /// <summary>Ajusta la vida exacta (lo usa la sincronización de red).</summary>
        public void SetCurrent(float value) => _current = Mathf.Clamp(value, 0f, maxHealth);
    }
}
