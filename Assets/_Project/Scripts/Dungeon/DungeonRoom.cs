using System.Collections.Generic;
using UnityEngine;

namespace Caos.Dungeon
{
    /// <summary>
    /// Base de una sala. Gestiona el estado común (objetivo, temporizador,
    /// puerta de salida y objetos creados) y deja el minijuego en OnTick.
    /// Cada tipo de sala vive en su propio script para poder añadir más sin
    /// tocar al director.
    /// </summary>
    public abstract class DungeonRoom : MonoBehaviour
    {
        readonly List<GameObject> _tracked = new List<GameObject>();

        DungeonDirector _director;
        DungeonRoomDefinition _definition;
        GameObject _exit;
        BoxCollider _exitGate;
        Vector3 _exitBaseScale = Vector3.one;

        sealed class ActivationGate
        {
            public Renderer Renderer;
            public Collider Collider;
        }

        /// <summary>Atajos que se abren cuando se completa la sala.</summary>
        readonly List<ActivationGate> _activationGates = new List<ActivationGate>();

        public int Index { get; private set; }
        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }
        public bool Active { get; private set; }
        public bool Completed { get; private set; }
        public bool Failed { get; private set; }
        public string FailureReason { get; private set; }

        public DungeonRoomKind Kind => _definition != null
            ? _definition.kind : DungeonRoomKind.Entrada;
        public DungeonRoomDefinition Definition => _definition;
        public string Title => _definition != null ? _definition.title : "Sala";
        public string Objective => _definition != null
            ? _definition.objective : "Completa la sala";
        public virtual string ProgressText => "";

        /// <summary>Segundos restantes, o -1 si la sala no tiene límite.</summary>
        public virtual float TimeRemaining => -1f;

        /// <summary>Punto donde aparece el jugador al entrar en la sala.</summary>
        public Vector3 EntryPoint => Center + Vector3.back * (Radius - 2.4f)
            + Vector3.up * 0.05f;

        /// <summary>Puerta que se abre al cumplir el objetivo.</summary>
        public Vector3 ExitPoint => Center + Vector3.forward * (Radius - 2.4f)
            + Vector3.up * 0.05f;

        protected DungeonDirector Director => _director;
        protected DungeonRunState State => _director != null ? _director.State : null;

        public void Configure(DungeonDirector director, DungeonRoomDefinition definition,
            int index, Vector3 center, float radius)
        {
            _director = director;
            _definition = definition;
            Index = index;
            Center = center;
            Radius = radius;
            BuildExit();
        }

        void BuildExit()
        {
            _exit = new GameObject("ExitDoor");
            _exit.transform.SetParent(transform, false);
            _exit.transform.position = ExitPoint;
            _exitBaseScale = _exit.transform.localScale;

            DungeonVisuals.Cylinder("Portal", ExitPoint + Vector3.up * 0.04f,
                new Vector3(2.8f, 0.06f, 2.8f), DungeonPalette.ExitClosed, _exit.transform, false);
            DungeonVisuals.Cylinder("Beam", ExitPoint + Vector3.up * 1.3f,
                new Vector3(0.34f, 1.3f, 0.34f), DungeonPalette.ExitClosed, _exit.transform, false);

            // Puerta física: impide entrar en la sala siguiente antes de
            // completar el objetivo, y bloquea el camino de vuelta al terminar.
            _exitGate = _exit.AddComponent<BoxCollider>();
            _exitGate.center = new Vector3(0f, 1.4f, 0f);
            _exitGate.size = new Vector3(3.2f, 2.8f, 0.6f);

            SetExitOpen(false);
        }

        public void Begin()
        {
            Completed = false;
            Failed = false;
            FailureReason = "";
            Active = true;
            SetExitOpen(false);
            if (_exit != null) _exit.transform.localScale = _exitBaseScale;
            OnBegin();
        }

        public void Tick(float deltaTime)
        {
            if (!Active || Completed || Failed) return;
            OnTick(deltaTime);
        }

        public void End()
        {
            Active = false;
            OnEnd();
            for (int i = 0; i < _tracked.Count; i++)
                if (_tracked[i] != null) Destroy(_tracked[i]);
            _tracked.Clear();
        }

        public void NotifyEnemyKilled(Vector3 position)
        {
            if (!Active || Completed || Failed) return;
            OnEnemyKilled(position);
        }

        public void NotifyBossDefeated()
        {
            if (!Active || Completed || Failed) return;
            OnBossDefeated();
        }

        public void NotifyPickup(DungeonPickup pickup)
        {
            if (!Active || Completed || Failed) return;
            OnPickup(pickup);
        }

        public void TrackObject(GameObject go)
        {
            if (go != null && !_tracked.Contains(go)) _tracked.Add(go);
        }

        /// <summary>Registra la compuerta de un camino lateral del mapa único.</summary>
        public void RegisterActivation(Renderer renderer, Collider collider)
        {
            if (renderer == null && collider == null) return;
            _activationGates.Add(new ActivationGate { Renderer = renderer, Collider = collider });
            SetExitOpen(false);
        }

        /// <summary>Mantiene cualquier punto dentro del suelo de esta sala.</summary>
        public Vector3 Clamp(Vector3 point)
        {
            float inset = 1.6f;
            point.x = Mathf.Clamp(point.x, Center.x - Radius + inset,
                Center.x + Radius - inset);
            point.z = Mathf.Clamp(point.z, Center.z - Radius + inset,
                Center.z + Radius - inset);
            point.y = Center.y + 0.95f;
            return point;
        }

        /// <summary>Punto aleatorio reproducible dentro de la sala.</summary>
        public Vector3 RandomPoint(float inset = 2.5f)
        {
            float limit = Mathf.Max(1f, Radius - inset);
            float x = _director != null
                ? _director.RandomRange(-limit, limit) : Random.Range(-limit, limit);
            float z = _director != null
                ? _director.RandomRange(-limit, limit) : Random.Range(-limit, limit);
            return Clamp(Center + new Vector3(x, 0.95f, z));
        }

        protected T Track<T>(GameObject go) where T : Component
        {
            TrackObject(go);
            return go.GetComponent<T>();
        }

        public void Complete()
        {
            if (Completed || Failed) return;
            Completed = true;
            SetExitOpen(true);
            if (_director != null) _director.OnRoomCompleted(this);
        }

        public void Fail(string reason)
        {
            if (Completed || Failed) return;
            Failed = true;
            FailureReason = reason;
            if (_director != null) _director.OnRoomFailed(this, reason);
        }

        void SetExitOpen(bool open)
        {
            if (_exit == null) return;
            if (_exitGate != null) _exitGate.enabled = !open;
            Material material = open ? DungeonPalette.ExitOpen : DungeonPalette.ExitClosed;
            Renderer[] renderers = _exit.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = material;

            for (int i = 0; i < _activationGates.Count; i++)
            {
                ActivationGate gate = _activationGates[i];
                if (gate.Collider != null) gate.Collider.enabled = !open;
                if (gate.Renderer != null) gate.Renderer.sharedMaterial = material;
            }
        }

        void LateUpdate()
        {
            if (!Completed || _exit == null) return;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.2f) * 0.07f;
            _exit.transform.localScale = _exitBaseScale * pulse;
        }

        protected abstract void OnBegin();
        protected virtual void OnTick(float deltaTime) { }
        protected virtual void OnEnd() { }
        protected virtual void OnEnemyKilled(Vector3 position) { }
        protected virtual void OnBossDefeated() { }
        protected virtual void OnPickup(DungeonPickup pickup) { }
    }
}
