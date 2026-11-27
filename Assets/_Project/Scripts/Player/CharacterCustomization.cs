using System.Collections.Generic;
using UnityEngine;
using Caos.Maps;

namespace Caos.Player
{
    /// <summary>
    /// Personalización del personaje: nombre, color y atuendo (gorro, camisa,
    /// pantalón y rostro). Todo se aplica en tiempo real y se guarda en PlayerPrefs.
    /// Dibuja además el nombre sobre la cabeza (OnGUI proyectado).
    /// Las prendas se crean con primitivas en tiempo real, así que no hacen falta
    /// assets nuevos ni regenerar la escena.
    /// </summary>
    public class CharacterCustomization : MonoBehaviour
    {
        const string NameKey = "Caos.PlayerName";
        const string ColorKey = "Caos.PlayerColor";
        const string HatKey = "Caos.PlayerHat";
        const string ShirtKey = "Caos.PlayerShirt";
        const string PantsKey = "Caos.PlayerPants";
        const string FaceKey = "Caos.PlayerFace";
        const string BuildKey = "Caos.PlayerBuild";
        const string CostumeKey = "Caos.PlayerCostume";

        const float NameHeight = 3.05f; // por encima incluso de los gorros altos

        // Alturas del esqueleto procedural (en metros desde los pies).
        const float HipY = 0.80f;
        const float KneeY = 0.44f;
        const float AnkleY = 0.19f;
        const float ShoulderY = 1.36f;

        /// <summary>Instancia activa (la usan las pantallas de personalización).</summary>
        public static CharacterCustomization Instance { get; private set; }

        [SerializeField] string playerName = "Jugador";
        [SerializeField] Color playerColor = new Color32(226, 85, 79, 255); // rojo del picker
        [SerializeField] bool netRemote;

        Material[] _bodyMaterials;
        GUIStyle _nameStyle;

        // Cuerpo (se escala el Body/Head que ya trae la escena)
        Transform _bodyTransform;
        Transform _headTransform;
        Vector3 _headBaseScale;
        Vector3 _headBasePosition;
        readonly List<Transform> _eyes = new List<Transform>();
        readonly List<Vector3> _eyeBasePositions = new List<Vector3>();
        float _bodyWidth = 1f;
        float _headScale = 1f;
        float _bellyBulge;
        float _legScale = 1f;
        float _legSpread = 0.24f;

        // Un solo torso deformable: cuerpo y ropa comparten los mismos vértices.
        sealed class DeformMesh
        {
            public Mesh mesh;
            public Vector3[] baseVertices;
        }

        Mesh _bodyMesh;
        Mesh _headBaseMesh;
        Mesh _headMesh;
        readonly List<DeformMesh> _deformMeshes = new List<DeformMesh>();
        Vector3 _bellyOffset;
        Vector3 _bellyVelocity;

        // Atuendo
        Transform _outfitRoot;
        readonly List<Material> _outfitMaterials = new List<Material>();
        readonly List<Material> _fleshMaterials = new List<Material>();
        readonly List<Material> _accentMaterials = new List<Material>();
        readonly List<Mesh> _outfitMeshes = new List<Mesh>();
        int _hatIndex;
        int _shirtIndex;
        int _pantsIndex;
        int _faceIndex;
        int _buildIndex;
        int _costumeIndex;

        // Física casera de la panza: resorte + amortiguación.
        sealed class JigglePart
        {
            public Transform transform;
            public Vector3 rest;
            public Vector3 baseScale;
            public Vector3 offset;
            public Vector3 velocity;
            public float strength = 1f;
        }

        readonly List<JigglePart> _jiggleParts = new List<JigglePart>();

        // Esqueleto procedural: cada pierna cuelga de la cadera, la rodilla de
        // la cadera y el pie del tobillo. Los brazos cuelgan del hombro.
        sealed class LegRig
        {
            public Transform hip;
            public Transform knee;
            public Transform ankle;
        }

        readonly LegRig[] _legRigs = new LegRig[2];
        readonly Transform[] _shoulders = new Transform[2];
        readonly Transform[] _elbows = new Transform[2];
        readonly Transform[] _hands = new Transform[2];
        Transform _gunRoot;
        readonly Transform[] _weaponVariants = new Transform[4];
        ArenaWeaponKind _weaponKind = ArenaWeaponKind.Pistola;
        bool _weaponVisible;
        bool _pistolLogged;
        bool _swordLogged;
        bool _meleeLogged;
        float _meleeSwing;
        float _gaitPhase;
        float _walkBlend;
        float _airBlend;
        float _landImpulse;
        float _lastVerticalSpeed;
        float _rollBlend;
        float _fireHold;
        float _fireRecoil;
        bool _firePoseLogged;
        bool _wasGrounded = true;

        PlayerController _controller;
        Vector3 _lastVelocity;

        public string PlayerName => playerName;
        public Color PlayerColor => playerColor;
        public int HatIndex => _hatIndex;
        public int ShirtIndex => _shirtIndex;
        public int PantsIndex => _pantsIndex;
        public int FaceIndex => _faceIndex;
        public int BuildIndex => _buildIndex;
        public int CostumeIndex => _costumeIndex;

        /// <summary>
        /// Dispara la pose de brazos: hombros hacia delante, codos flexionados y
        /// un pequeño contrarresto. Lo llaman los directores de combate cuando
        /// sale una bala real, no cada frame que el botón está pulsado.
        /// </summary>
        public void NotifyShot()
        {
            if (netRemote) return;
            _fireRecoil = 1f;
            _fireHold = 0.34f;
            if (!_firePoseLogged)
            {
                Debug.Log("[Caos] Pose de disparo: pistola empuñada con dos manos");
                _firePoseLogged = true;
            }
        }

        /// <summary>
        /// Muestra u oculta el arma anclada a la mano derecha. Se conserva por
        /// compatibilidad; los directores usan SetWeaponVisible con el tipo real.
        /// </summary>
        public void SetPistolVisible(bool visible)
        {
            if (visible) SetWeaponVisible(ArenaWeaponKind.Pistola, ArenaElement.Normal);
            else HideWeapon();
        }

        /// <summary>
        /// Enciende el modelo procedural del arma equipada. Antes solo existía
        /// una pistola genérica, así que el arma recogida no se veía en la mano.
        /// </summary>
        public void SetWeaponVisible(ArenaWeaponKind kind, ArenaElement element)
        {
            _weaponKind = kind;
            _weaponVisible = true;
            ApplyWeaponVariant();
            if (_gunRoot != null) _gunRoot.gameObject.SetActive(true);

            if (kind == ArenaWeaponKind.Espada)
            {
                if (!_swordLogged)
                {
                    Debug.Log($"[Caos] Espada eléctrica en mano: hoja {ArenaElementInfo.Label(element)}");
                    _swordLogged = true;
                }
                return;
            }

            if (!_pistolLogged)
            {
                Debug.Log($"[Caos] {kind} en mano: empuñadura con dos manos");
                _pistolLogged = true;
            }
        }

        /// <summary>Apaga el arma de la mano (menús y avatares de sala).</summary>
        public void HideWeapon()
        {
            _weaponVisible = false;
            ApplyWeaponVariant();
            if (_gunRoot != null) _gunRoot.gameObject.SetActive(false);
        }

        void ApplyWeaponVariant()
        {
            int active = (int)_weaponKind;
            for (int i = 0; i < _weaponVariants.Length; i++)
                if (_weaponVariants[i] != null)
                    _weaponVariants[i].gameObject.SetActive(_weaponVisible && i == active);
        }

        /// <summary>
        /// Arco de golpe de la espada. El remoto no recibe el tipo de arma en el
        /// mensaje de melee, así que le mostramos la hoja al ver el golpe.
        /// </summary>
        public void NotifyMelee()
        {
            if (netRemote && !_weaponVisible)
                SetWeaponVisible(ArenaWeaponKind.Espada, ArenaElement.Rayo);

            _meleeSwing = 1f;
            if (!_meleeLogged)
            {
                Debug.Log("[Caos] Golpe con espada eléctrica: arco de brazo");
                _meleeLogged = true;
            }
        }

        /// <summary>
        /// Cuerpo estándar: pelaje blanco de panda. Los trajes y atuendos se
        /// montan encima de este cuerpo; ninguno cambia la especie base.
        /// </summary>
        Color EffectiveBodyColor => White;

        /// <summary>Orejas, extremidades y almohadillas: negro de panda.</summary>
        Color EffectiveLimbColor => Dark;

        int EffectiveBuild => _costumeIndex == 0 ? _buildIndex : 0;

        void Awake()
        {
            if (!netRemote) Instance = this;

            // Importante: capturar los materiales del cuerpo ANTES de crear el
            // atuendo, para que el color del jugador no tiñe también la ropa.
            _bodyMaterials = CollectBodyMaterials();
            FindBodyParts();
            _controller = GetComponent<PlayerController>();

            Load();
            LoadEquippedSkin();
            CreateOutfitRoot();
            RebuildOutfit();
        }

        void OnDestroy()
        {
            if (!netRemote && Instance == this) Instance = null;
            ClearOutfit();
        }

        void OnApplicationQuit() => Save();

        /// <summary>
        /// Anima piernas, pies y brazos con un ciclo de marcha procedural y
        /// una pose de salto, y aplica después el rebote de las partes blandas.
        /// </summary>
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            Vector3 acceleration = Vector3.zero;
            float speed = 0f;
            float verticalSpeed = 0f;
            bool grounded = true;

            if (_controller != null)
            {
                acceleration = (_controller.Velocity - _lastVelocity) / dt;
                acceleration = Vector3.ClampMagnitude(acceleration, 30f);
                _lastVelocity = _controller.Velocity;
                speed = _controller.HorizontalSpeed;
                verticalSpeed = _controller.Velocity.y;
                grounded = _controller.IsGrounded;
            }

            bool rolling = _controller != null && _controller.IsRolling;
            _rollBlend = Mathf.MoveTowards(_rollBlend, rolling ? 1f : 0f, dt * 14f);
            _fireHold = Mathf.MoveTowards(_fireHold, 0f, dt * 1.6f);
            _fireRecoil = Mathf.MoveTowards(_fireRecoil, 0f, dt * 6f);
            _meleeSwing = Mathf.MoveTowards(_meleeSwing, 0f, dt * 2.6f);

            AnimateLimbs(dt, speed, verticalSpeed, grounded);
            AnimateSoftParts(dt, acceleration, speed);
            ApplyRollPose();
        }

        /// <summary>
        /// Giro completo alrededor de la cadera. Se rota el cuerpo base, la
        /// cabeza y toda la ropa con el mismo ángulo; desplazamos cada raíz para
        /// que el pivote sea la cadera y no los pies. Al llegar a 360° la pose
        /// vuelve a la normal sin petar ni desincronizar al personaje remoto.
        /// </summary>
        void ApplyRollPose()
        {
            float angle = 0f;
            if (_controller != null && _controller.IsRolling)
            {
                float phase = _controller.RollPhase;
                float eased = phase * phase * (3f - 2f * phase);
                angle = 360f * eased;
            }

            Quaternion rotation = Quaternion.Euler(angle, 0f, 0f);
            Vector3 pivot = new Vector3(0f, HipY, 0f);
            Vector3 offset = pivot - rotation * pivot;

            if (_outfitRoot != null)
            {
                _outfitRoot.localRotation = rotation;
                _outfitRoot.localPosition = offset;
            }
            if (_bodyTransform != null)
            {
                _bodyTransform.localRotation = rotation;
                _bodyTransform.localPosition = offset;
            }
            if (_headTransform != null)
            {
                _headTransform.localRotation = rotation;
                _headTransform.localPosition = pivot + rotation * (_headBasePosition - pivot);
            }
        }

        /// <summary>
        /// Marcha: brazos y piernas se balances en oposición, la rodilla se
        /// dobla al avanzar y el tobillo compensa para que el pie no se clave
        /// en el suelo. Salto: piernas recogidas y brazos arriba; caída:
        /// piernas extendidas y brazos abiertos; aterrizaje: pequeño golpe.
        /// </summary>
        void AnimateLimbs(float dt, float speed, float verticalSpeed, bool grounded)
        {
            if (_legRigs[0] == null || _legRigs[0].hip == null) return;

            if (grounded && speed > 0.15f)
                _gaitPhase = (_gaitPhase + speed * 3f * dt) % (Mathf.PI * 2f);

            _walkBlend = Mathf.MoveTowards(_walkBlend,
                grounded ? Mathf.Clamp01(speed / 2f) : 0f, dt * 4f);
            _airBlend = Mathf.MoveTowards(_airBlend, grounded ? 0f : 1f, dt * 7f);

            if (!_wasGrounded && grounded)
                _landImpulse = Mathf.Clamp01(-_lastVerticalSpeed / 8f);
            _landImpulse = Mathf.MoveTowards(_landImpulse, 0f, dt * 2.5f);
            _wasGrounded = grounded;
            _lastVerticalSpeed = verticalSpeed;

            // El giro recoge piernas y brazos; el disparo adelanta hombros y
            // dobla codos. Ninguno de los dos toca el CharacterController.
            float rollTuck = 0f;
            if (_controller != null && _controller.IsRolling)
                rollTuck = _rollBlend * Mathf.Sin(Mathf.PI * _controller.RollPhase);
            float walk = _walkBlend * (1f - rollTuck);
            float air = _airBlend * (1f - rollTuck);
            float fire = Mathf.Clamp01(_fireHold * 1.8f + _fireRecoil * 0.55f
                + _meleeSwing * 0.8f);
            bool sword = _weaponVisible && _weaponKind == ArenaWeaponKind.Espada;
            // Con arma equipada los brazos la sostienen incluso en reposo; la
            // espada se empuña con una mano y traza un arco al golpear.
            float hold = Mathf.Clamp01(Mathf.Max(fire,
                _weaponVisible ? (sword ? 0.45f : 0.62f) : 0f));

            float ascend = Mathf.Clamp01(verticalSpeed / 5f);
            float descend = Mathf.Clamp01(-verticalSpeed / 7f);
            float bob = Mathf.Abs(Mathf.Cos(_gaitPhase)) * 0.045f * walk
                - _landImpulse * 0.08f;
            float idle = _controller == null ? 1f : 1f - walk;

            for (int i = 0; i < 2; i++)
            {
                var rig = _legRigs[i];
                float side = i == 0 ? -1f : 1f;
                float phase = _gaitPhase + (i == 0 ? 0f : Mathf.PI);

                float swing = Mathf.Sin(phase) * 34f * walk;
                float kneeWalk = Mathf.Max(0f, Mathf.Sin(phase + 0.7f)) * 58f * walk;
                float idleFlex = 5f * (1f - walk) * (1f - air);
                float airHip = -10f + side * 6f - 30f * ascend - 14f * descend;
                float airKnee = 8f + 58f * ascend + 18f * descend;

                float hipAngle = Mathf.Lerp(swing, airHip, air) - idleFlex * 0.5f;
                float kneeAngle = Mathf.Lerp(kneeWalk, airKnee, air)
                    + _landImpulse * 28f + idleFlex;
                hipAngle = Mathf.Lerp(hipAngle, -78f, rollTuck);
                kneeAngle = Mathf.Lerp(kneeAngle, 120f, rollTuck);
                float footAngle = -hipAngle - kneeAngle * 0.75f
                    + Mathf.Max(0f, swing) * 0.4f * (1f - air);

                rig.hip.localRotation = Quaternion.Euler(hipAngle, 0f, 0f);
                rig.hip.localPosition = new Vector3(0f, HipY + bob, 0f);
                rig.knee.localRotation = Quaternion.Euler(kneeAngle, 0f, 0f);
                rig.ankle.localRotation = Quaternion.Euler(footAngle, 0f, 0f);

                var shoulder = _shoulders[i];
                if (shoulder == null) continue;

                float armSwing = -Mathf.Sin(phase) * 40f * walk;
                float armAir = -20f - 62f * ascend - 28f * descend;
                float armAngle = Mathf.Lerp(armSwing, armAir, air)
                    + Mathf.Sin(Time.time * 2.2f) * 3f * idle * (1f - air);
                float swingArc = sword
                    ? Mathf.Sin((1f - _meleeSwing) * Mathf.PI) * 62f * _meleeSwing
                    : 0f;
                armAngle = Mathf.Lerp(armAngle, -58f - _fireRecoil * 14f + swingArc, hold);
                armAngle = Mathf.Lerp(armAngle, 70f, rollTuck);
                float armRoll = side * (4f + 6f * Mathf.Abs(Mathf.Sin(phase))
                    * walk * (1f - air)) * (1f - hold);
                // La mano izquierda cruza hacia la empuñadura; la derecha la
                // lleva al centro sin soltar el arma. La espada es a una mano.
                armRoll = Mathf.Lerp(armRoll,
                    sword ? (i == 0 ? 14f : -32f) : (i == 0 ? 52f : -10f), hold);

                shoulder.localRotation = Quaternion.Euler(armAngle, 0f, armRoll);

                var elbow = _elbows[i];
                if (elbow != null)
                {
                    float elbowAngle = Mathf.Lerp(12f, -32f, hold)
                        - _fireRecoil * 14f + rollTuck * 85f;
                    elbow.localRotation = Quaternion.Euler(elbowAngle, 0f, 0f);
                }
                var hand = _hands[i];
                if (hand != null)
                    hand.localRotation = Quaternion.Euler(
                        -swing * side * 0.4f * (1f - hold), 0f,
                        (i == 0 ? 6f : -6f) * hold);
            }
        }

        /// <summary>
        /// Rebote de la panza y los brazos: la aceleración del jugador
        /// (saltos, aterrizajes, arranques y frenadas) empuja las piezas
        /// blandas, que vuelven a su sitio con un resorte amortiguado.
        /// </summary>
        void AnimateSoftParts(float dt, Vector3 acceleration, float speed)
        {
            if (_jiggleParts.Count == 0 && _bellyBulge <= 0f) return;

            // Al caminar, un vaivén rítmico; al saltar/aterrizar, el impulso
            // vertical. En el menú (sin controlador) respira suavemente.
            Vector3 force = new Vector3(
                acceleration.x * 0.35f,
                acceleration.y * 0.55f + Mathf.Sin(_gaitPhase * 2f) * speed * 0.9f,
                acceleration.z * 0.35f);

            if (_controller == null)
            {
                // En la vitrina del menú la respiración debe apreciarse.
                force.y += Mathf.Sin(Time.time * 2.2f) * 8f;
                force.x += Mathf.Sin(Time.time * 1.6f) * 3f;
            }

            // La panza ya no es una bola: es el propio torso el que se deforma.
            if (_bellyBulge > 0f)
            {
                _bellyVelocity += (-_bellyOffset * 95f - _bellyVelocity * 9f
                    + force * _bellyBulge * 2.2f) * dt;
                _bellyOffset += _bellyVelocity * dt;

                if (_bellyOffset.sqrMagnitude > 0.14f * 0.14f)
                {
                    _bellyOffset = Vector3.ClampMagnitude(_bellyOffset, 0.14f);
                    _bellyVelocity *= 0.5f;
                }

                DeformTorsoMeshes();
            }

            foreach (var part in _jiggleParts)
            {
                part.velocity += (-part.offset * 95f - part.velocity * 9f
                    + force * part.strength) * dt;
                part.offset += part.velocity * dt;

                if (part.offset.sqrMagnitude > 0.20f * 0.20f)
                {
                    part.offset = Vector3.ClampMagnitude(part.offset, 0.20f);
                    part.velocity *= 0.5f;
                }

                part.transform.localPosition = part.rest + part.offset;

                float squash = Mathf.Clamp(part.offset.y, -0.12f, 0.12f);
                part.transform.localScale = new Vector3(
                    part.baseScale.x * (1f - squash * 0.9f),
                    part.baseScale.y * (1f + squash * 1.6f),
                    part.baseScale.z * (1f - squash * 0.9f));

                part.transform.localRotation = Quaternion.Euler(
                    part.offset.z * 25f, 0f, -part.offset.x * 25f);
            }
        }

        /// <summary>Aplica el mismo desplazamiento al cuerpo y a todas las capas de ropa.</summary>
        void DeformTorsoMeshes()
        {
            foreach (var entry in _deformMeshes)
            {
                if (entry.mesh == null || entry.baseVertices == null) continue;

                var vertices = entry.mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float y = entry.baseVertices[i].y / 1.8f;
                    float weight = y >= 0.22f && y <= 0.82f
                        ? Mathf.Sin(Mathf.PI * Mathf.InverseLerp(0.22f, 0.82f, y))
                        : 0f;

                    vertices[i] = entry.baseVertices[i] + new Vector3(
                        _bellyOffset.x * weight * 0.55f,
                        _bellyOffset.y * weight * 0.70f,
                        _bellyOffset.z * weight);
                }

                entry.mesh.vertices = vertices;
                entry.mesh.RecalculateNormals();
                entry.mesh.RecalculateBounds();
            }
        }

        void RegisterJiggle(GameObject piece, float strength)
        {
            if (piece == null) return;

            _jiggleParts.Add(new JigglePart
            {
                transform = piece.transform,
                rest = piece.transform.localPosition,
                baseScale = piece.transform.localScale,
                strength = strength,
            });
        }

        /// <summary>Pivote sin rotación inicial: hijo directo de otro pivote o del atuendo.</summary>
        Transform MakePivot(string name, Vector3 localPosition, Transform parent)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = localPosition;
            return pivot;
        }

        /// <summary>
        /// Traslada una pieza creada en coordenadas del atuendo al interior de
        /// un pivote, manteniendo su posición original. El desplazamiento del
        /// pivote se mide desde la raíz del atuendo (los pivotes no rotan al
        /// crearse, así que basta con restar).
        /// </summary>
        void AttachToPivot(GameObject piece, Transform pivot, Vector3 pivotOffset)
        {
            if (piece == null || pivot == null) return;
            piece.transform.SetParent(pivot, false);
            piece.transform.localPosition -= pivotOffset;
        }

        // ---------------------------------------------------------- cambios

        public void SetName(string name)
        {
            if (string.IsNullOrEmpty(name)) name = string.Empty;
            if (name.Length > 16) name = name.Substring(0, 16);
            if (name == playerName) return;

            playerName = name;
        }

        public void SetColor(Color color)
        {
            if (color == playerColor) return;

            playerColor = color;
            ApplyColor();
            ClearSkinSelection();
        }

        public void SetHat(int index)
        {
            index = Clamp(index, OutfitCatalog.Hats.Length);
            if (index == _hatIndex) return;
            _hatIndex = index;
            RebuildOutfit();
            ClearSkinSelection();
        }

        public void SetShirt(int index)
        {
            index = Clamp(index, OutfitCatalog.Shirts.Length);
            if (index == _shirtIndex) return;
            _shirtIndex = index;
            RebuildOutfit();
            ClearSkinSelection();
        }

        public void SetPants(int index)
        {
            index = Clamp(index, OutfitCatalog.Pants.Length);
            if (index == _pantsIndex) return;
            _pantsIndex = index;
            RebuildOutfit();
            ClearSkinSelection();
        }

        public void SetFace(int index)
        {
            index = Clamp(index, OutfitCatalog.Faces.Length);
            if (index == _faceIndex) return;
            _faceIndex = index;
            RebuildOutfit();
            ClearSkinSelection();
        }

        public void SetBuild(int index)
        {
            index = Clamp(index, OutfitCatalog.Builds.Length);
            if (index == _buildIndex) return;
            _buildIndex = index;
            RebuildOutfit();
            ClearSkinSelection();
        }

        public void SetCostume(int index)
        {
            index = Clamp(index, OutfitCatalog.Costumes.Length);
            if (index == _costumeIndex) return;
            _costumeIndex = index;
            RebuildOutfit();
            ClearSkinSelection();
        }

        /// <summary>Aplica una skin comprada sin tocar el saldo ni el perfil remoto.</summary>
        public void ApplySkin(CaosSkinDefinition skin)
        {
            if (skin == null || netRemote) return;

            CopySkin(skin);
            ApplyColor();
            RebuildOutfit();
            Save();
        }

        /// <summary>Restaura la skin comprada al crear el avatar local.</summary>
        void LoadEquippedSkin()
        {
            if (netRemote) return;
            CaosSkinDefinition skin = CaosSkinCatalog.Find(CaosProfile.EquippedSkinId);
            if (skin != null) CopySkin(skin);
        }

        void CopySkin(CaosSkinDefinition skin)
        {
            playerColor = skin.color;
            _hatIndex = Clamp(skin.hat, OutfitCatalog.Hats.Length);
            _shirtIndex = Clamp(skin.shirt, OutfitCatalog.Shirts.Length);
            _pantsIndex = Clamp(skin.pants, OutfitCatalog.Pants.Length);
            _faceIndex = Clamp(skin.face, OutfitCatalog.Faces.Length);
            _buildIndex = Clamp(skin.build, OutfitCatalog.Builds.Length);
            _costumeIndex = Clamp(skin.costume, OutfitCatalog.Costumes.Length);
        }

        void ClearSkinSelection()
        {
            if (!netRemote) CaosProfile.ClearEquippedSkin();
        }

        /// <summary>Guarda en PlayerPrefs (lo llaman las pantallas al cerrar).</summary>
        public void Save()
        {
            if (netRemote) return;

            PlayerPrefs.SetString(NameKey, playerName);
            PlayerPrefs.SetString(ColorKey, ColorUtility.ToHtmlStringRGB(playerColor));
            PlayerPrefs.SetInt(HatKey, _hatIndex);
            PlayerPrefs.SetInt(ShirtKey, _shirtIndex);
            PlayerPrefs.SetInt(PantsKey, _pantsIndex);
            PlayerPrefs.SetInt(FaceKey, _faceIndex);
            PlayerPrefs.SetInt(BuildKey, _buildIndex);
            PlayerPrefs.SetInt(CostumeKey, _costumeIndex);
            PlayerPrefs.Save();
        }

        /// <summary>Avatar de la sala: usa la apariencia guardada del jugador local.</summary>
        public static CharacterCustomization CreateLocalAvatar()
        {
            var go = new GameObject("LobbyAvatar");
            RemotePart(go.transform, "Body", PrimitiveType.Capsule,
                new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), Color.white);
            RemotePart(go.transform, "Head", PrimitiveType.Sphere,
                new Vector3(0f, 1.9f, 0f), Vector3.one * 0.55f, Color.white);
            return go.AddComponent<CharacterCustomization>();
        }

        /// <summary>
        /// Crea un avatar de otro jugador con el mismo atuendo procedural. El
        /// singleton sigue apuntando al jugador local: un remoto nunca debe
        /// cargar ni guardar PlayerPrefs.
        /// </summary>
        public static CharacterCustomization CreateRemote(string name, Color color,
            int hat, int shirt, int pants, int face, int build, int costume)
        {
            CharacterCustomization previous = Instance;
            var go = new GameObject("RemotePlayer_" + (string.IsNullOrWhiteSpace(name) ? "Invitado" : name));

            RemotePart(go.transform, "Body", PrimitiveType.Capsule,
                new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), color);
            RemotePart(go.transform, "Head", PrimitiveType.Sphere,
                new Vector3(0f, 1.9f, 0f), Vector3.one * 0.55f, color);
            RemotePart(go.transform, "Eye", PrimitiveType.Sphere,
                new Vector3(-0.12f, 1.95f, 0.24f), Vector3.one * 0.13f, Color.white);
            RemotePart(go.transform, "Eye", PrimitiveType.Sphere,
                new Vector3(0.12f, 1.95f, 0.24f), Vector3.one * 0.13f, Color.white);

            var remote = go.AddComponent<CharacterCustomization>();
            remote.netRemote = true;
            Instance = previous != null ? previous : null;
            remote.ApplyRemoteAppearance(name, color, hat, shirt, pants, face, build, costume);
            return remote;
        }

        /// <summary>Aplica el atuendo recibido por red sin tocar los ajustes locales.</summary>
        public void ApplyRemoteAppearance(string name, Color color,
            int hat, int shirt, int pants, int face, int build, int costume)
        {
            netRemote = true;
            playerName = string.IsNullOrWhiteSpace(name) ? "Jugador" : name;
            playerColor = color;
            _hatIndex = Clamp(hat, OutfitCatalog.Hats.Length);
            _shirtIndex = Clamp(shirt, OutfitCatalog.Shirts.Length);
            _pantsIndex = Clamp(pants, OutfitCatalog.Pants.Length);
            _faceIndex = Clamp(face, OutfitCatalog.Faces.Length);
            _buildIndex = Clamp(build, OutfitCatalog.Builds.Length);
            _costumeIndex = Clamp(costume, OutfitCatalog.Costumes.Length);
            RebuildOutfit();
        }

        static void RemotePart(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;

            var collider = part.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            var material = new Material(LitShader);
            material.name = name == "Eye" ? "EyeWhite" : "BodyMaterial";
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---------------------------------------------------------- internos

        void Load()
        {
            playerName = PlayerPrefs.GetString(NameKey, playerName);

            string html = PlayerPrefs.GetString(ColorKey, string.Empty);
            if (!string.IsNullOrEmpty(html) &&
                ColorUtility.TryParseHtmlString("#" + html, out Color saved))
            {
                playerColor = saved;
            }

            _hatIndex = PlayerPrefs.GetInt(HatKey, 0);
            _shirtIndex = PlayerPrefs.GetInt(ShirtKey, 0);
            _pantsIndex = PlayerPrefs.GetInt(PantsKey, 0);
            _faceIndex = PlayerPrefs.GetInt(FaceKey, 0);
            _buildIndex = PlayerPrefs.GetInt(BuildKey, 0);
            _costumeIndex = PlayerPrefs.GetInt(CostumeKey, 0);
        }

        static int Clamp(int value, int length)
        {
            if (length <= 0) return 0;
            if (value < 0) return 0;
            if (value >= length) return 0;
            return value;
        }

        Material[] CollectBodyMaterials()
        {
            // Solo Body y Head: los ojos conservan su color.
            var renderers = GetComponentsInChildren<Renderer>(true);
            var materials = new List<Material>(renderers.Length);

            foreach (var renderer in renderers)
            {
                var shared = renderer.sharedMaterial;
                if (shared == null || shared.name.Contains("Eye")) continue;

                materials.Add(renderer.material); // instancia: no toca el asset
            }

            return materials.ToArray();
        }

        void ApplyColor()
        {
            // El blanco y el negro son fijos: el color elegido por el jugador
            // queda en los interiores de las orejas (accent).
            ApplyColorTo(_bodyMaterials, EffectiveBodyColor);
            ApplyColorTo(_fleshMaterials, Dark);
            ApplyColorTo(_accentMaterials, playerColor);
        }

        void ApplyColorTo(IEnumerable<Material> materials, Color color)
        {
            if (materials == null) return;

            foreach (var material in materials)
            {
                if (material == null) continue;

                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            }
        }

        // ---------------------------------------------------------- cuerpo

        void FindBodyParts()
        {
            _bodyTransform = transform.Find("Body");
            _headTransform = transform.Find("Head");

            if (_headTransform != null)
            {
                _headBaseScale = _headTransform.localScale;
                _headBasePosition = _headTransform.localPosition;
                var headFilter = _headTransform.GetComponent<MeshFilter>();
                if (headFilter != null) _headBaseMesh = headFilter.sharedMesh;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.name.Contains("Eye")) continue;
                _eyes.Add(renderer.transform);
                _eyeBasePositions.Add(renderer.transform.localPosition);
                renderer.enabled = false; // se sustituyen por ojos con blanco + pupila
            }
        }

        /// <summary>
        /// Regenera el torso como una única malla con la forma elegida. La
        /// panza no es una pieza añadida: es el perfil de esta malla.
        /// </summary>
        void ApplyBodyShape()
        {
            // Todas las complexiones son variaciones del mismo panda redondo:
            // incluso "Flaco" conserva el hocico, las orejas y la panza.
            switch (EffectiveBuild)
            {
                case 1: // Flaco
                    _bodyWidth = 1.02f;
                    _headScale = 1.02f;
                    _bellyBulge = 0.18f;
                    break;
                case 2: // Gordo
                    _bodyWidth = 1.34f;
                    _headScale = 1.16f;
                    _bellyBulge = 0.70f;
                    break;
                case 3: // Gordísimo
                    _bodyWidth = 1.52f;
                    _headScale = 1.26f;
                    _bellyBulge = 1f;
                    break;
                default: // Panda estándar
                    _bodyWidth = 1.22f;
                    _headScale = 1.08f;
                    _bellyBulge = 0.50f;
                    break;
            }

            if (_costumeIndex == 1) // Mono: mismo cuerpo panda, algo más compacto.
            {
                _bodyWidth = 1.12f;
                _headScale = 1.08f;
                _bellyBulge = 0.30f;
            }
            else if (_costumeIndex == 2) // Pororo: panda redondo con atuendo de pingüino.
            {
                _bodyWidth = 1.10f;
                _headScale = 1.08f;
                _bellyBulge = 0.28f;
            }

            // Las piernas no crecen tanto como la panza: así se siguen viendo
            // separadas incluso en Gordísimo.
            _legScale = 0.75f + 0.25f * _bodyWidth;
            _legSpread = 0.24f * _legScale;

            if (_bodyTransform != null)
            {
                _bodyTransform.localPosition = Vector3.zero;
                _bodyTransform.localScale = Vector3.one;

                var filter = _bodyTransform.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    // El torso empieza en la cintura; las piernas se crean en
                    // BuildBodyExtras como dos mallas independientes.
                    var mesh = BuildTorsoMesh(0.72f, 1.8f, 0f);
                    filter.sharedMesh = mesh;

                    if (_bodyMesh != null && _bodyMesh != mesh) Destroy(_bodyMesh);
                    _bodyMesh = mesh;
                    RegisterTorsoMesh(mesh);
                }
            }

            if (_headTransform != null)
            {
                // La papada se modela dentro de la cabeza: no es una esfera
                // añadida, así el perfil sigue siendo una sola pieza.
                var filter = _headTransform.GetComponent<MeshFilter>();
                if (filter != null && _headBaseMesh != null)
                {
                    if (_headMesh != null)
                    {
                        Destroy(_headMesh);
                        _headMesh = null;
                    }

                    if (_bellyBulge > 0f)
                    {
                        _headMesh = Instantiate(_headBaseMesh);
                        _headMesh.name = "HeadWithChin";
                        AddDoubleChin(_headMesh, _bellyBulge);
                        filter.sharedMesh = _headMesh;
                    }
                    else
                    {
                        filter.sharedMesh = _headBaseMesh;
                    }
                }

                _headTransform.localScale = _headBaseScale * _headScale;
            }

            for (int i = 0; i < _eyes.Count; i++)
            {
                var eye = _eyes[i];
                if (eye == null) continue;

                Vector3 basePos = i < _eyeBasePositions.Count
                    ? _eyeBasePositions[i] : eye.localPosition;

                eye.localPosition = new Vector3(
                    basePos.x * _headScale,
                    basePos.y,
                    basePos.z + (_headScale - 1f) * 0.30f);
                eye.localScale = Vector3.one * 0.13f * _headScale;
            }
        }

        /// <summary>Perfil del torso en una altura dada (y en metros desde los pies).</summary>
        void TorsoRing(float y, out float radiusX, out float radiusZ, out float centerZ)
        {
            float t = Mathf.Clamp01(y / 1.8f);
            float profile = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.35f);
            // Estrechamos los hombros para que el cuello encaje con la cabeza
            // incluso en los cuerpos más anchos (evita el efecto "aro").
            float taper = t > 0.78f
                ? Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(0.78f, 1f, t))
                : 1f;
            float baseRadius = 0.35f * _bodyWidth * Mathf.Max(profile, 0.03f) * taper;

            float belly = _bellyBulge > 0f
                ? _bellyBulge * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(0.22f, 0.80f, t))
                : 0f;

            radiusX = baseRadius * (1f + belly * 0.12f);
            radiusZ = baseRadius + belly * 0.22f;
            centerZ = belly * 0.12f;
        }

        /// <summary>Integra la papada en la propia esfera de la cabeza.</summary>
        static void AddDoubleChin(Mesh mesh, float bulge)
        {
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (v.y >= 0f || v.z <= 0f) continue;

                float lower = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-v.y * 2f));
                float front = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v.z * 2f));
                float weight = lower * front * bulge;

                v.z += 0.20f * weight;
                v.y -= 0.10f * weight;
                v.x *= 1f + 0.10f * weight;
                vertices[i] = v;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Malla del torso (o de una capa de ropa) generada con el mismo
        /// perfil, así la ropa siempre queda por encima del cuerpo.
        /// </summary>
        Mesh BuildTorsoMesh(float yMin, float yMax, float inflate)
        {
            const int segments = 16;
            const int rings = 14;

            var mesh = new Mesh { name = "Torso" };
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var uvs = new Vector2[vertices.Length];

            for (int ring = 0; ring <= rings; ring++)
            {
                float y = Mathf.Lerp(yMin, yMax, ring / (float)rings);
                TorsoRing(y, out float rx, out float rz, out float centerZ);
                rx += inflate;
                rz += inflate;

                for (int segment = 0; segment <= segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / segments;
                    int index = ring * (segments + 1) + segment;

                    vertices[index] = new Vector3(
                        Mathf.Cos(angle) * rx,
                        y,
                        centerZ + Mathf.Sin(angle) * rz);
                    uvs[index] = new Vector2(segment / (float)segments, ring / (float)rings);
                }
            }

            var triangles = new int[rings * segments * 6];
            int t = 0;
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = ring * (segments + 1) + segment;
                    int b = a + segments + 1;

                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;

                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Capa de ropa: misma forma que el torso, un poco más grande.</summary>
        GameObject TorsoShell(string name, float yMin, float yMax, Color color,
            float inflate = 0.022f, float smoothness = 0.15f)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(_outfitRoot, false);
            piece.transform.localPosition = Vector3.zero;

            var mesh = BuildTorsoMesh(yMin, yMax, inflate);
            _outfitMeshes.Add(mesh);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(color, smoothness);
            RegisterTorsoMesh(mesh);
            return piece;
        }

        /// <summary>Profundidad de la superficie de la ropa en una altura y x dados.</summary>
        float TorsoSurfaceZ(float y, float x = 0f)
        {
            TorsoRing(y, out float rx, out float rz, out float centerZ);
            float nx = Mathf.Clamp01(Mathf.Abs(x) / Mathf.Max(rx, 0.001f));
            return centerZ + rz * Mathf.Sqrt(1f - nx * nx) + 0.045f;
        }

        /// <summary>Posición de un ojo base ya escalada (o la posición estándar).</summary>
        Vector3 EyePosition(int side)
        {
            for (int i = 0; i < _eyeBasePositions.Count; i++)
            {
                Vector3 basePos = _eyeBasePositions[i];
                if (Mathf.Sign(basePos.x) != side) continue;
                return new Vector3(basePos.x * _headScale, basePos.y,
                    basePos.z + (_headScale - 1f) * 0.30f);
            }
            return new Vector3(side * 0.12f * _headScale, 1.95f,
                0.24f + (_headScale - 1f) * 0.30f);
        }

        /// <summary>
        /// Cuerpo canónico del panda: orejas redondas, parches oscuros alrededor
        /// de los ojos y hocico claro. Se crea antes que cualquier atuendo para
        /// que todos los trajes (y los avatares remotos) compartan la base.
        /// </summary>
        void BuildPandaBase(bool pandaFace)
        {
            float s = _headScale;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 ear = new Vector3(side * 0.38f * s, 1.9f + 0.44f * s, -0.02f);
                Quaternion earTilt = Quaternion.Euler(0f, 0f, side * -12f);

                Piece(PrimitiveType.Sphere, "PandaEar", ear,
                    new Vector3(0.19f, 0.22f, 0.11f) * s, Dark,
                    earTilt, false, 0.35f);

                // El color del jugador vive dentro de las orejas: así el picker
                // sigue siendo visible sin teñir el pelaje blanco/negro.
                var inner = Piece(PrimitiveType.Sphere, "PandaEarInner",
                    ear + new Vector3(0f, 0f, 0.075f * s),
                    new Vector3(0.10f, 0.13f, 0.035f) * s,
                    playerColor, earTilt, false, 0.3f);
                _accentMaterials.Add(inner.GetComponent<Renderer>().sharedMaterial);

                if (!pandaFace) continue;

                Vector3 eye = EyePosition(side);
                Piece(PrimitiveType.Sphere, "PandaEyePatch",
                    new Vector3(eye.x, eye.y, eye.z - 0.035f * s),
                    new Vector3(0.23f, 0.25f, 0.075f) * s, Dark,
                    Quaternion.Euler(0f, 0f, side * -18f), false, 0.3f);
            }

            if (!pandaFace) return;

            float front = 0.30f + (s - 1f) * 0.26f;
            Piece(PrimitiveType.Sphere, "PandaMuzzle",
                new Vector3(0f, 1.72f, front),
                new Vector3(0.30f, 0.20f, 0.15f) * s, White, null, false, 0.4f);
            Piece(PrimitiveType.Sphere, "PandaNose",
                new Vector3(0f, 1.79f, front + 0.13f * s),
                Vector3.one * 0.075f * s, Dark, null, false, 0.5f);

            for (int i = 0; i < 5; i++)
            {
                int offset = i - 2;
                Piece(PrimitiveType.Sphere, "PandaSmile",
                    new Vector3(offset * 0.055f * s,
                        1.66f + offset * offset * 0.012f * s,
                        front + 0.10f * s),
                    Vector3.one * 0.020f * s, Dark, null, false, 0.3f);
            }
        }

        // ---------------------------------------------------------- piernas

        /// <summary>
        /// Cadera -> rodilla -> tobillo. Los tres pivotes comparten el eje X
        /// central: como la rotación es alrededor de X, la separación entre
        /// piernas (guardada en los vértices de cada malla) no se altera.
        /// </summary>
        void CreateLegRigs()
        {
            for (int i = 0; i < 2; i++)
            {
                var hip = MakePivot("LegHip", new Vector3(0f, HipY, 0f), _outfitRoot);
                var knee = MakePivot("LegKnee", new Vector3(0f, KneeY - HipY, 0f), hip);
                var ankle = MakePivot("LegAnkle", new Vector3(0f, AnkleY - KneeY, 0f), knee);
                _legRigs[i] = new LegRig { hip = hip, knee = knee, ankle = ankle };
            }
        }

        void LegRing(float y, out float radius)
        {
            float t = Mathf.Clamp01((y - 0.03f) / 0.77f);
            radius = (0.10f + 0.075f * t + 0.015f * Mathf.Sin(Mathf.PI * t)) * _legScale;
        }

        /// <summary>Pierna independiente: dos mallas con hueco entre ellas.</summary>
        Mesh BuildLegMesh(float side, float yMin, float yMax, float inflate)
        {
            const int segments = 12;
            const int rings = 8;

            var mesh = new Mesh { name = "Leg" };
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var uvs = new Vector2[vertices.Length];

            for (int ring = 0; ring <= rings; ring++)
            {
                float y = Mathf.Lerp(yMin, yMax, ring / (float)rings);
                LegRing(y, out float radius);
                radius += inflate;

                for (int segment = 0; segment <= segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / segments;
                    int index = ring * (segments + 1) + segment;

                    vertices[index] = new Vector3(
                        side * _legSpread + Mathf.Cos(angle) * radius,
                        y,
                        Mathf.Sin(angle) * radius);
                    uvs[index] = new Vector2(segment / (float)segments, ring / (float)rings);
                }
            }

            var triangles = new int[rings * segments * 6];
            int t = 0;
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = ring * (segments + 1) + segment;
                    int b = a + segments + 1;

                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;

                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        enum LegPart { Upper, Lower }

        /// <summary>
        /// Segmento de pierna o de pantalón colgado de su pivote. Los vértices
        /// están en alturas absolutas, así que la malla se baja hasta el eje
        /// del pivote para que la pieza quede donde estaba antes de animarla.
        /// </summary>
        GameObject LegSegmentObject(string name, int side, float yMin, float yMax,
            float inflate, Material material, LegPart part)
        {
            if (yMax <= yMin) return null;

            var rig = _legRigs[side < 0 ? 0 : 1];
            bool upper = part == LegPart.Upper;
            var pivot = upper ? rig.hip : rig.knee;
            float offsetY = upper ? HipY : KneeY;

            var piece = new GameObject(name);
            piece.transform.SetParent(pivot, false);
            piece.transform.localPosition = Vector3.down * offsetY;

            var mesh = BuildLegMesh(side, yMin, yMax, inflate);
            _outfitMeshes.Add(mesh);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterial = material;
            return piece;
        }

        /// <summary>Carne de cada pierna; el color del jugador se aplica después.</summary>
        void BuildLegs()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var material = MakeMaterial(EffectiveLimbColor, 0.25f);
                _fleshMaterials.Add(material);

                LegSegmentObject("LegUpper", side, KneeY, 0.80f, 0f, material, LegPart.Upper);
                LegSegmentObject("LegLower", side, 0.03f, KneeY, 0f, material, LegPart.Lower);
            }
        }

        /// <summary>Capa de pantalón sobre una pierna concreta, partida en la rodilla.</summary>
        void LegShell(string name, int side, float yMin, float yMax,
            Color color, float inflate)
        {
            var material = MakeMaterial(color, 0.15f);

            if (yMax > KneeY)
                LegSegmentObject(name + "Upper", side, Mathf.Max(yMin, KneeY), yMax,
                    inflate, material, LegPart.Upper);
            if (yMin < KneeY)
                LegSegmentObject(name + "Lower", side, yMin, Mathf.Min(yMax, KneeY),
                    inflate, material, LegPart.Lower);
        }

        // ---------------------------------------------------------- atuendo

        void CreateOutfitRoot()
        {
            var root = new GameObject("Outfit");
            root.transform.SetParent(transform, false);
            _outfitRoot = root.transform;
        }

        void ClearOutfit()
        {
            if (_outfitRoot != null) Destroy(_outfitRoot.gameObject);

            foreach (var material in _outfitMaterials)
            {
                if (material != null) Destroy(material);
            }
            foreach (var mesh in _outfitMeshes)
            {
                if (mesh != null) Destroy(mesh);
            }

            _outfitMaterials.Clear();
            _fleshMaterials.Clear();
            _accentMaterials.Clear();
            _outfitMeshes.Clear();
            _jiggleParts.Clear();
            _deformMeshes.Clear();
            for (int i = 0; i < 2; i++)
            {
                _legRigs[i] = null;
                _shoulders[i] = null;
                _elbows[i] = null;
                _hands[i] = null;
            }
            _gunRoot = null;
            for (int i = 0; i < _weaponVariants.Length; i++) _weaponVariants[i] = null;
            _meleeSwing = 0f;
            _bellyOffset = Vector3.zero;
            _bellyVelocity = Vector3.zero;

            if (_headMesh != null)
            {
                Destroy(_headMesh);
                _headMesh = null;
            }
            if (_headBaseMesh != null && _headTransform != null)
            {
                var filter = _headTransform.GetComponent<MeshFilter>();
                if (filter != null) filter.sharedMesh = _headBaseMesh;
            }
        }

        void RegisterTorsoMesh(Mesh mesh)
        {
            if (mesh == null) return;
            _deformMeshes.Add(new DeformMesh
            {
                mesh = mesh,
                baseVertices = mesh.vertices,
            });
        }

        void RebuildOutfit()
        {
            if (_outfitRoot != null) ClearOutfit();
            ApplyBodyShape();

            _outfitRoot = new GameObject("Outfit").transform;
            _outfitRoot.SetParent(transform, false);

            BuildBodyExtras(); // pies y, si aplica, barriga / papada / mofletes
            // Orejas siempre; hocico/parches salvo en Pororo, cuya cara de
            // pingüino tapa la del panda sin cambiar el cuerpo base.
            BuildPandaBase(_costumeIndex != 2);

            if (_costumeIndex == 0)
            {
                BuildPants();
                BuildShirt();
            }
            else if (_costumeIndex == 2)
            {
                BuildPororoBelly();
            }

            BuildFaceDetails(); // ojos, cejas, nariz, boca, orejas, pelo
            BuildFace();        // gafas, bigote, parche...
            BuildHat();

            ApplyColor();
            Debug.Log("[Caos] Outfit ready: pieces=" + _outfitRoot.childCount +
                " deform=" + _deformMeshes.Count + " jiggle=" + _jiggleParts.Count +
                " build=" + _buildIndex + " costume=" + _costumeIndex +
                " bulge=" + _bellyBulge + " panda=1");
        }

        static Shader _litShader;
        static Shader LitShader
        {
            get
            {
                if (_litShader == null)
                    _litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                return _litShader;
            }
        }

        Material MakeMaterial(Color color, float smoothness = 0.3f, float metallic = 0f)
        {
            var material = new Material(LitShader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

            _outfitMaterials.Add(material);
            return material;
        }

        GameObject Piece(PrimitiveType type, string name, Vector3 position, Vector3 scale,
            Color color, Quaternion? rotation = null, bool flesh = false,
            float smoothness = 0.3f, float metallic = 0f)
        {
            var piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(_outfitRoot, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            if (rotation.HasValue) piece.transform.localRotation = rotation.Value;

            var collider = piece.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var material = MakeMaterial(color, smoothness, metallic);
            if (flesh) _fleshMaterials.Add(material);
            piece.GetComponent<Renderer>().sharedMaterial = material;
            return piece;
        }

        /// <summary>Pieza anclada directamente a un pivote (arma, mano de apoyo).</summary>
        GameObject GripPiece(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Color color,
            Quaternion? rotation = null, float smoothness = 0.3f, float metallic = 0f)
        {
            var piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;
            if (rotation.HasValue) piece.transform.localRotation = rotation.Value;

            var collider = piece.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            piece.GetComponent<Renderer>().sharedMaterial =
                MakeMaterial(color, smoothness, metallic);
            return piece;
        }

        GameObject Cone(string name, Vector3 position, float radius, float height,
            Color color, Quaternion? rotation = null, float smoothness = 0.3f, float metallic = 0f)
        {
            var cone = new GameObject(name);
            cone.transform.SetParent(_outfitRoot, false);
            cone.transform.localPosition = position;
            if (rotation.HasValue) cone.transform.localRotation = rotation.Value;

            var mesh = CreateConeMesh(radius, height, 16);
            _outfitMeshes.Add(mesh);
            cone.AddComponent<MeshFilter>().sharedMesh = mesh;
            cone.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(color, smoothness, metallic);
            return cone;
        }

        static Mesh CreateConeMesh(float radius, float height, int segments)
        {
            var mesh = new Mesh { name = "Cone" };
            var vertices = new Vector3[segments + 2];
            vertices[0] = new Vector3(0f, height * 0.5f, 0f); // punta

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * radius, -height * 0.5f, Mathf.Sin(angle) * radius);
            }
            vertices[segments + 1] = new Vector3(0f, -height * 0.5f, 0f); // centro base

            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int t = i * 6;

                // Cara lateral (devuelta para que las normales miren hacia fuera)
                triangles[t] = 0;
                triangles[t + 1] = next + 1;
                triangles[t + 2] = i + 1;

                // Base
                triangles[t + 3] = segments + 1;
                triangles[t + 4] = i + 1;
                triangles[t + 5] = next + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------- colores

        static readonly Color Gold = new Color32(246, 196, 68, 255);
        static readonly Color Dark = new Color32(36, 38, 52, 255);
        static readonly Color White = new Color32(245, 245, 240, 255);
        static readonly Color Skin = new Color32(240, 188, 150, 255);
        static readonly Color Pink = new Color32(242, 128, 168, 255);
        static readonly Color Brown = new Color32(126, 82, 54, 255);
        static readonly Color Red = new Color32(226, 78, 78, 255);
        static readonly Color Blue = new Color32(64, 128, 220, 255);
        static readonly Color Purple = new Color32(135, 82, 210, 255);
        static readonly Color Cyan = new Color32(86, 204, 242, 255);
        static readonly Color Hair = new Color32(84, 58, 42, 255);
        static readonly Color Basil = new Color32(94, 178, 96, 255);
        static readonly Color MonkeyFur = new Color32(122, 78, 46, 255);
        static readonly Color MonkeyMuzzle = new Color32(226, 184, 132, 255);
        static readonly Color PororoBlue = new Color32(66, 150, 214, 255);
        static readonly Color BeakOrange = new Color32(246, 154, 42, 255);

        // ---------------------------------------------------------- gorros

        void BuildHat()
        {
            if (_costumeIndex == 2)
            {
                PororoHat();
                return;
            }
            if (_costumeIndex == 1) return; // el mono lleva mechón, no sombrero

            switch (_hatIndex)
            {
                case 1: HatParty(); break;
                case 2: HatTop(); break;
                case 3: HatCrown(); break;
                case 4: HatBunny(); break;
                case 5: HatCowboy(); break;
                case 6: HatJester(); break;
                case 7: HatPizza(); break;
                case 8: HatIceCream(); break;
                case 9: HatBanana(); break;
            }
        }

        void HatParty()
        {
            Cone("PartyCone", new Vector3(0f, 2.36f, 0f), 0.22f, 0.42f, Purple,
                null, 0.18f);
            Piece(PrimitiveType.Cylinder, "PartyBrim", new Vector3(0f, 2.16f, 0f),
                new Vector3(0.48f, 0.015f, 0.48f), Gold, null, false, 0.65f, 0.55f);
            Piece(PrimitiveType.Cylinder, "Stripe1", new Vector3(0f, 2.27f, 0f),
                new Vector3(0.34f, 0.012f, 0.34f), Cyan, null, false, 0.6f, 0.1f);
            Piece(PrimitiveType.Cylinder, "Stripe2", new Vector3(0f, 2.42f, 0f),
                new Vector3(0.22f, 0.012f, 0.22f), Pink, null, false, 0.6f, 0.1f);
            Piece(PrimitiveType.Sphere, "Pomp", new Vector3(0f, 2.60f, 0f),
                Vector3.one * 0.09f, Gold, null, false, 0.7f, 0.5f);
        }

        void HatTop()
        {
            Piece(PrimitiveType.Cylinder, "TopBrim", new Vector3(0f, 2.16f, 0f),
                new Vector3(0.66f, 0.02f, 0.66f), Dark, null, false, 0.55f, 0.05f);
            Piece(PrimitiveType.Cylinder, "TopCrown", new Vector3(0f, 2.36f, 0f),
                new Vector3(0.38f, 0.20f, 0.38f), Dark, null, false, 0.55f, 0.05f);
            Piece(PrimitiveType.Cylinder, "TopBand", new Vector3(0f, 2.20f, 0f),
                new Vector3(0.39f, 0.03f, 0.39f), Red, null, false, 0.35f);
            Piece(PrimitiveType.Cube, "TopBuckle", new Vector3(0f, 2.20f, 0.205f),
                new Vector3(0.10f, 0.07f, 0.02f), Gold, null, false, 0.75f, 0.6f);
            Piece(PrimitiveType.Cube, "TopFeather", new Vector3(0.24f, 2.48f, 0f),
                new Vector3(0.025f, 0.28f, 0.025f), White,
                Quaternion.Euler(0f, 0f, -28f), false, 0.15f);
        }

        void HatCrown()
        {
            Piece(PrimitiveType.Cylinder, "CrownBase", new Vector3(0f, 2.16f, 0f),
                new Vector3(0.50f, 0.04f, 0.50f), Gold, null, false, 0.75f, 0.65f);
            Piece(PrimitiveType.Cylinder, "CrownVelvet", new Vector3(0f, 2.19f, 0f),
                new Vector3(0.47f, 0.025f, 0.47f), Red, null, false, 0.12f);

            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f;
                float x = Mathf.Cos(angle) * 0.19f;
                float z = Mathf.Sin(angle) * 0.19f;
                Piece(PrimitiveType.Cube, "CrownSpike", new Vector3(x, 2.30f, z),
                    new Vector3(0.07f, 0.18f, 0.07f), Gold, null, false, 0.75f, 0.65f);
                Piece(PrimitiveType.Sphere, "CrownGem", new Vector3(x, 2.40f, z),
                    Vector3.one * 0.055f, i % 2 == 0 ? Red : Blue,
                    null, false, 0.85f, 0.2f);
            }
        }

        void HatBunny()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.15f;
                Piece(PrimitiveType.Capsule, "BunnyEar", new Vector3(x, 2.45f, -0.02f),
                    new Vector3(0.13f, 0.30f, 0.13f), White);
                Piece(PrimitiveType.Capsule, "BunnyInner", new Vector3(x, 2.45f, 0.04f),
                    new Vector3(0.07f, 0.24f, 0.07f), Pink);
            }
        }

        void HatCowboy()
        {
            Piece(PrimitiveType.Cylinder, "CowboyBrim", new Vector3(0f, 2.14f, 0f),
                new Vector3(0.78f, 0.02f, 0.78f), Brown);
            Piece(PrimitiveType.Cylinder, "CowboyCrown", new Vector3(0f, 2.29f, 0f),
                new Vector3(0.40f, 0.16f, 0.40f), Brown);
            Piece(PrimitiveType.Cylinder, "CowboyBand", new Vector3(0f, 2.19f, 0f),
                new Vector3(0.41f, 0.03f, 0.41f), Dark, null, false, 0.4f);
            Piece(PrimitiveType.Cube, "CowboyBuckle", new Vector3(0f, 2.19f, 0.21f),
                new Vector3(0.09f, 0.06f, 0.02f), Gold, null, false, 0.75f, 0.6f);
        }

        void HatJester()
        {
            Cone("JesterCone", new Vector3(0f, 2.42f, 0f), 0.21f, 0.48f, Purple);
            Piece(PrimitiveType.Sphere, "JesterBell", new Vector3(0f, 2.68f, 0f),
                Vector3.one * 0.08f, Gold);

            for (int side = -1; side <= 1; side += 2)
            {
                var rotation = Quaternion.Euler(0f, 0f, side * -55f);
                Cone("JesterHorn", new Vector3(side * 0.20f, 2.31f, 0f),
                    0.11f, 0.34f, side < 0 ? Cyan : Red, rotation);
                Piece(PrimitiveType.Sphere, "JesterBell",
                    new Vector3(side * 0.36f, 2.45f, 0f), Vector3.one * 0.07f, Gold);
            }
        }

        void HatPizza()
        {
            Piece(PrimitiveType.Cylinder, "PizzaCrust", new Vector3(0f, 2.13f, 0f),
                new Vector3(0.78f, 0.022f, 0.78f), Brown);
            Piece(PrimitiveType.Cylinder, "PizzaCheese", new Vector3(0f, 2.15f, 0f),
                new Vector3(0.72f, 0.022f, 0.72f), new Color32(246, 196, 88, 255));

            var toppings = new[]
            {
                new Vector3(-0.18f, 2.18f, 0.05f),
                new Vector3(0.16f, 2.18f, -0.14f),
                new Vector3(0.10f, 2.18f, 0.20f),
                new Vector3(-0.05f, 2.18f, -0.24f),
                new Vector3(0.26f, 2.18f, 0.08f),
                new Vector3(-0.28f, 2.18f, -0.12f),
            };
            foreach (var position in toppings)
            {
                Piece(PrimitiveType.Cylinder, "Pepperoni", position,
                    new Vector3(0.13f, 0.008f, 0.13f), Red, null, false, 0.25f);
            }

            var basil = new[]
            {
                new Vector3(-0.08f, 2.19f, 0.18f),
                new Vector3(0.22f, 2.19f, -0.22f),
                new Vector3(-0.24f, 2.19f, -0.02f),
            };
            foreach (var position in basil)
            {
                Piece(PrimitiveType.Cube, "Basil", position,
                    new Vector3(0.09f, 0.008f, 0.06f), Basil,
                    Quaternion.Euler(0f, position.x * 120f, 0f), false, 0.2f);
            }
        }

        void HatIceCream()
        {
            Cone("IceCone", new Vector3(0f, 2.33f, 0f), 0.20f, 0.36f, Brown,
                Quaternion.Euler(180f, 0f, 0f));
            Piece(PrimitiveType.Sphere, "IceScoop", new Vector3(0f, 2.56f, 0f),
                Vector3.one * 0.30f, Pink, null, false, 0.5f);

            var sprinkles = new[]
            {
                new Vector3(-0.12f, 2.74f, 0.10f),
                new Vector3(0.10f, 2.76f, 0.13f),
                new Vector3(0.18f, 2.62f, 0.16f),
                new Vector3(-0.18f, 2.60f, 0.17f),
                new Vector3(0.02f, 2.68f, 0.22f),
            };
            for (int i = 0; i < sprinkles.Length; i++)
            {
                Piece(PrimitiveType.Cube, "Sprinkle", sprinkles[i],
                    new Vector3(0.035f, 0.012f, 0.012f),
                    i % 2 == 0 ? Cyan : Gold,
                    Quaternion.Euler(0f, 0f, i * 37f), false, 0.4f);
            }

            Piece(PrimitiveType.Sphere, "Cherry", new Vector3(0f, 2.75f, 0f),
                Vector3.one * 0.08f, Red, null, false, 0.8f);
        }

        void HatBanana()
        {
            Piece(PrimitiveType.Capsule, "BananaLeft", new Vector3(-0.22f, 2.27f, 0f),
                new Vector3(0.14f, 0.22f, 0.14f), Gold,
                Quaternion.Euler(0f, 0f, -40f), false, 0.5f);
            Piece(PrimitiveType.Capsule, "BananaMid", new Vector3(0f, 2.43f, 0f),
                new Vector3(0.14f, 0.22f, 0.14f), Gold, null, false, 0.5f);
            Piece(PrimitiveType.Capsule, "BananaRight", new Vector3(0.22f, 2.27f, 0f),
                new Vector3(0.14f, 0.22f, 0.14f), Gold,
                Quaternion.Euler(0f, 0f, 40f), false, 0.5f);

            for (int side = -1; side <= 1; side += 2)
            {
                Piece(PrimitiveType.Sphere, "BananaTip",
                    new Vector3(side * 0.36f, 2.15f, 0f), Vector3.one * 0.05f, Brown);
            }
        }

        /// <summary>Gorro de aviador rojo con gafas, como el de Pororo.</summary>
        void PororoHat()
        {
            float s = _headScale;

            Piece(PrimitiveType.Sphere, "PororoCap",
                new Vector3(0f, 2.12f, -0.02f),
                new Vector3(0.60f, 0.30f, 0.60f) * s, Red, null, false, 0.25f);

            for (int side = -1; side <= 1; side += 2)
            {
                Piece(PrimitiveType.Sphere, "PororoEarFlap",
                    new Vector3(side * 0.33f * s, 1.98f, 0f),
                    new Vector3(0.16f, 0.20f, 0.16f) * s, Red);

                var frameRotation = Quaternion.Euler(90f, 0f, 0f);
                Piece(PrimitiveType.Cylinder, "PororoGoggleFrame",
                    new Vector3(side * 0.14f * s, 2.03f, 0.31f * s),
                    new Vector3(0.16f, 0.012f, 0.16f) * s, Dark, frameRotation);
                Piece(PrimitiveType.Cylinder, "PororoGoggleLens",
                    new Vector3(side * 0.14f * s, 2.03f, 0.33f * s),
                    new Vector3(0.13f, 0.010f, 0.13f) * s, Cyan, frameRotation);
            }

            Piece(PrimitiveType.Cube, "PororoGoggleBridge",
                new Vector3(0f, 2.03f, 0.34f * s),
                new Vector3(0.08f, 0.02f, 0.02f) * s, Dark);
        }

        // ---------------------------------------------------------- pies y cuerpo

        void BuildBodyExtras()
        {
            // Los pivotes deben existir antes de crear piernas, zapatos y brazos.
            CreateLegRigs();
            BuildLegs();

            float spread = _legSpread;
            float shoeWidth = 0.24f * _legScale;
            var ankleOffset = new Vector3(0f, AnkleY, 0f);
            Color shoeColor = _costumeIndex == 2 ? BeakOrange
                : _costumeIndex == 1 ? MonkeyMuzzle : Dark;
            Color soleColor = _costumeIndex == 2 ? BeakOrange : White;

            for (int side = -1; side <= 1; side += 2)
            {
                var ankle = _legRigs[side < 0 ? 0 : 1].ankle;

                var shoe = Piece(PrimitiveType.Cube, "Shoe",
                    new Vector3(side * spread, 0.10f, 0.13f),
                    new Vector3(shoeWidth, 0.14f, 0.36f), shoeColor);
                AttachToPivot(shoe, ankle, ankleOffset);

                var sole = Piece(PrimitiveType.Cube, "Sole",
                    new Vector3(side * spread, 0.025f, 0.13f),
                    new Vector3(shoeWidth * 1.05f, 0.03f, 0.38f), soleColor);
                AttachToPivot(sole, ankle, ankleOffset);
            }

            BuildArms();
        }

        /// <summary>
        /// Brazos separados del torso y colgados de un hombro pivotante, con
        /// suficiente holgura para que se vean incluso en Gordísimo. El rebote
        /// blando se registra después de colgarlos para usar su pose final.
        /// </summary>
        void BuildArms()
        {
            TorsoRing(1.15f, out float rx, out _, out _);
            bool mega = EffectiveBuild == 3;
            float radius = 0.10f * (0.85f + 0.15f * _bodyWidth);
            float upperLength = mega ? 0.11f : 0.12f;
            float lowerLength = mega ? 0.10f : 0.11f;
            float elbowDrop = upperLength * 2f;

            for (int side = -1; side <= 1; side += 2)
            {
                int i = side < 0 ? 0 : 1;
                float x = side * (rx + radius + 0.04f);
                Quaternion tilt = Quaternion.Euler(0f, 0f, side * 12f);

                var shoulder = MakePivot("Shoulder", new Vector3(x, ShoulderY, 0f), _outfitRoot);
                _shoulders[i] = shoulder;
                var elbow = MakePivot("Elbow", new Vector3(0f, -elbowDrop, 0f), shoulder);
                _elbows[i] = elbow;
                Vector3 elbowOffset = shoulder.localPosition + new Vector3(0f, -elbowDrop, 0f);

                var upper = Piece(PrimitiveType.Capsule, "ArmUpper",
                    new Vector3(x, ShoulderY - upperLength, 0f),
                    new Vector3(radius, upperLength, radius), EffectiveLimbColor,
                    tilt, true, 0.25f);
                AttachToPivot(upper, shoulder, shoulder.localPosition);
                RegisterJiggle(upper, 0.30f);

                var forearm = Piece(PrimitiveType.Capsule, "ArmLower",
                    new Vector3(x, ShoulderY - elbowDrop - lowerLength, 0f),
                    new Vector3(radius * 0.92f, lowerLength, radius * 0.92f),
                    EffectiveLimbColor, tilt, true, 0.25f);
                AttachToPivot(forearm, elbow, elbowOffset);

                var hand = Piece(PrimitiveType.Sphere, "Hand",
                    new Vector3(x, ShoulderY - elbowDrop - lowerLength * 2.55f, 0f),
                    Vector3.one * radius * 1.15f, EffectiveLimbColor,
                    null, true, 0.25f);
                AttachToPivot(hand, elbow, elbowOffset);
                _hands[i] = hand.transform;
            }

            BuildPistol();
        }

        /// <summary>
        /// Armas procedurales sin assets externos, colgadas de la mano derecha.
        /// El cañón o la hoja apunta por el eje -Y local: al elevar los brazos en
        /// la pose de tiro queda mirando hacia delante. Solo se activa el modelo
        /// del arma equipada y la mano de apoyo pertenece a las de fuego.
        /// Los índices de _weaponVariants siguen los de ArenaWeaponKind.
        /// </summary>
        void BuildPistol()
        {
            if (_hands[1] == null) return;

            var grip = MakePivot("WeaponHold", new Vector3(0f, -0.04f, 0.02f), _hands[1]);
            _gunRoot = grip;

            Color steel = new Color32(58, 64, 74, 255);
            Color dark = new Color32(24, 27, 33, 255);
            Color sight = new Color32(255, 210, 92, 255);
            Color electric = ArenaElementInfo.Color(ArenaElement.Rayo);

            // 0 - pistola
            var pistol = MakePivot("Pistol", Vector3.zero, grip);
            _weaponVariants[0] = pistol;
            GripPiece(PrimitiveType.Cube, "PistolGrip", pistol,
                new Vector3(0f, 0.01f, 0f), new Vector3(0.075f, 0.15f, 0.085f),
                dark, null, 0.35f);
            GripPiece(PrimitiveType.Cube, "PistolFrame", pistol,
                new Vector3(0f, -0.12f, 0f), new Vector3(0.09f, 0.14f, 0.075f),
                steel, null, 0.55f, 0.35f);
            GripPiece(PrimitiveType.Cube, "PistolSlide", pistol,
                new Vector3(0f, -0.26f, 0f), new Vector3(0.095f, 0.20f, 0.085f),
                steel, null, 0.65f, 0.45f);
            GripPiece(PrimitiveType.Cylinder, "PistolBarrel", pistol,
                new Vector3(0f, -0.39f, 0f), new Vector3(0.035f, 0.10f, 0.035f),
                dark, null, 0.7f, 0.5f);
            GripPiece(PrimitiveType.Cube, "PistolTrigger", pistol,
                new Vector3(0f, -0.02f, 0.075f), new Vector3(0.055f, 0.10f, 0.022f),
                steel, null, 0.5f, 0.3f);
            GripPiece(PrimitiveType.Cube, "PistolSight", pistol,
                new Vector3(0f, -0.31f, -0.05f), new Vector3(0.02f, 0.03f, 0.02f),
                sight, null, 0.6f, 0.2f);

            // 1 - escopeta
            var shotgun = MakePivot("Shotgun", Vector3.zero, grip);
            _weaponVariants[1] = shotgun;
            GripPiece(PrimitiveType.Cube, "ShotgunStock", shotgun,
                new Vector3(0f, 0.07f, 0.03f), new Vector3(0.10f, 0.20f, 0.12f),
                dark, null, 0.4f);
            GripPiece(PrimitiveType.Cube, "ShotgunReceiver", shotgun,
                new Vector3(0f, -0.16f, 0f), new Vector3(0.13f, 0.18f, 0.10f),
                steel, null, 0.6f, 0.4f);
            GripPiece(PrimitiveType.Cylinder, "ShotgunBarrelA", shotgun,
                new Vector3(-0.035f, -0.38f, 0f), new Vector3(0.032f, 0.18f, 0.032f),
                dark, null, 0.7f, 0.5f);
            GripPiece(PrimitiveType.Cylinder, "ShotgunBarrelB", shotgun,
                new Vector3(0.035f, -0.38f, 0f), new Vector3(0.032f, 0.18f, 0.032f),
                dark, null, 0.7f, 0.5f);

            // 2 - rifle
            var rifle = MakePivot("Rifle", Vector3.zero, grip);
            _weaponVariants[2] = rifle;
            GripPiece(PrimitiveType.Cube, "RifleStock", rifle,
                new Vector3(0f, 0.06f, 0.05f), new Vector3(0.09f, 0.19f, 0.13f),
                dark, null, 0.4f);
            GripPiece(PrimitiveType.Cube, "RifleBody", rifle,
                new Vector3(0f, -0.15f, 0f), new Vector3(0.10f, 0.22f, 0.09f),
                steel, null, 0.65f, 0.45f);
            GripPiece(PrimitiveType.Cylinder, "RifleBarrel", rifle,
                new Vector3(0f, -0.46f, 0f), new Vector3(0.028f, 0.24f, 0.028f),
                dark, null, 0.7f, 0.55f);
            GripPiece(PrimitiveType.Cube, "RifleSight", rifle,
                new Vector3(0f, -0.31f, -0.05f), new Vector3(0.022f, 0.04f, 0.02f),
                sight, null, 0.6f, 0.2f);

            // 3 - espada eléctrica: empuñadura, guarda y hoja luminosa
            var sword = MakePivot("ElectricSword", Vector3.zero, grip);
            _weaponVariants[3] = sword;
            GripPiece(PrimitiveType.Cylinder, "SwordHilt", sword,
                new Vector3(0f, 0.03f, 0f), new Vector3(0.035f, 0.14f, 0.035f),
                dark, null, 0.4f, 0.2f);
            var guard = GripPiece(PrimitiveType.Cube, "SwordGuard", sword,
                new Vector3(0f, -0.10f, 0f), new Vector3(0.26f, 0.035f, 0.09f),
                electric, null, 0.55f, 0.4f);
            var blade = GripPiece(PrimitiveType.Cube, "SwordBlade", sword,
                new Vector3(0f, -0.43f, 0f), new Vector3(0.07f, 0.62f, 0.022f),
                electric, null, 0.65f, 0.35f);
            GripPiece(PrimitiveType.Cube, "SwordTip", sword,
                new Vector3(0f, -0.77f, 0f), new Vector3(0.035f, 0.12f, 0.016f),
                electric, null, 0.65f, 0.35f);
            AddGlow(guard, electric);
            AddGlow(blade, electric);

            float radius = 0.10f * (0.85f + 0.15f * _bodyWidth);
            for (int i = 0; i < 3; i++)
            {
                GripPiece(PrimitiveType.Sphere, "SupportHand", _weaponVariants[i],
                    new Vector3(-radius * 0.75f, -0.13f, 0.015f),
                    Vector3.one * radius * 1.15f, EffectiveLimbColor, null, 0.25f);
            }

            ApplyWeaponVariant();
            grip.gameObject.SetActive(_weaponVisible);
        }

        static void AddGlow(GameObject piece, Color color)
        {
            if (piece == null) return;
            Material material = piece.GetComponent<Renderer>().sharedMaterial;
            if (material == null || !material.HasProperty("_EmissionColor")) return;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.55f);
        }

        // ---------------------------------------------------------- camisas

        void BuildShirt()
        {
            switch (_shirtIndex)
            {
                case 0: ShirtPlain(new Color32(96, 168, 224, 255)); break;
                case 1: ShirtStripes(); break;
                case 2: ShirtSuit(); break;
                case 3: ShirtJacket(); break;
                case 4: ShirtHoodie(); break;
                case 5: ShirtHawaiian(); break;
            }
        }

        void ShirtPlain(Color color)
        {
            // Capa sobre el torso: sigue exactamente la panza, incluso al rebotar.
            TorsoShell("Shirt", 0.84f, 1.62f, color, 0.030f);
        }

        void ShirtStripes()
        {
            ShirtPlain(White);
            for (int i = 0; i < 3; i++)
            {
                TorsoShell("Stripe", 1.00f + i * 0.20f, 1.06f + i * 0.20f,
                    Red, 0.038f);
            }
        }

        void ShirtSuit()
        {
            ShirtPlain(Dark);
            float z = TorsoSurfaceZ(1.55f, 0.10f);
            Piece(PrimitiveType.Cube, "CollarL", new Vector3(-0.10f, 1.55f, z),
                new Vector3(0.12f, 0.06f, 0.04f), White, Quaternion.Euler(0f, 0f, 25f));
            Piece(PrimitiveType.Cube, "CollarR", new Vector3(0.10f, 1.55f, z),
                new Vector3(0.12f, 0.06f, 0.04f), White, Quaternion.Euler(0f, 0f, -25f));
            Piece(PrimitiveType.Cube, "Tie", new Vector3(0f, 1.36f, TorsoSurfaceZ(1.36f)),
                new Vector3(0.07f, 0.22f, 0.03f), Red);
        }

        void ShirtJacket()
        {
            var color = new Color32(52, 74, 122, 255);
            ShirtPlain(color);
            TorsoRing(1.15f, out float rx, out _, out _);
            for (int side = -1; side <= 1; side += 2)
            {
                var sleeve = Piece(PrimitiveType.Capsule, "Sleeve",
                    new Vector3(side * (rx + 0.02f), 1.15f, 0f),
                    new Vector3(0.14f, 0.32f, 0.14f), color,
                    Quaternion.Euler(0f, 0f, side * 10f));

                // La manga acompaña al brazo al caminar y al saltar.
                var shoulder = _shoulders[side < 0 ? 0 : 1];
                AttachToPivot(sleeve, shoulder, shoulder != null ? shoulder.localPosition : Vector3.zero);
            }
            Piece(PrimitiveType.Cube, "Zipper",
                new Vector3(0f, 1.15f, TorsoSurfaceZ(1.15f)),
                new Vector3(0.025f, 0.72f, 0.02f), new Color32(200, 205, 215, 255));
        }

        void ShirtHoodie()
        {
            ShirtPlain(new Color32(120, 130, 148, 255));
            Piece(PrimitiveType.Sphere, "Hood", new Vector3(0f, 2.00f, -0.22f),
                Vector3.one * 0.56f * _headScale, new Color32(100, 110, 128, 255));
            Piece(PrimitiveType.Cube, "Pocket",
                new Vector3(0f, 0.98f, TorsoSurfaceZ(0.98f)),
                new Vector3(0.30f * _bodyWidth, 0.16f, 0.02f),
                new Color32(100, 110, 128, 255));
        }

        void ShirtHawaiian()
        {
            ShirtPlain(new Color32(246, 214, 96, 255));
            var flowers = new[]
            {
                new Vector2(-0.18f, 1.25f),
                new Vector2(0.20f, 1.05f),
                new Vector2(-0.10f, 0.95f),
                new Vector2(0.16f, 1.48f),
            };
            foreach (var position in flowers)
            {
                float x = position.x * (0.75f + 0.25f * _bodyWidth);
                float z = TorsoSurfaceZ(position.y, x);
                Piece(PrimitiveType.Sphere, "Flower", new Vector3(x, position.y, z),
                    Vector3.one * 0.09f, Pink);
                Piece(PrimitiveType.Sphere, "FlowerCenter",
                    new Vector3(x, position.y, z + 0.06f), Vector3.one * 0.035f, Gold);
            }
        }

        // ---------------------------------------------------------- pantalones

        void BuildPants()
        {
            switch (_pantsIndex)
            {
                case 0: PantsPlain(new Color32(58, 74, 116, 255)); break;
                case 1: PantsShorts(); break;
                case 2: PantsSkirt(); break;
                case 3: PantsStripes(); break;
            }
        }

        void PantsHip(Color color)
        {
            TorsoShell("PantsHip", 0.72f, 0.94f, color, 0.026f);
        }

        void PantsPlain(Color color)
        {
            PantsHip(color);
            for (int side = -1; side <= 1; side += 2)
                LegShell("PantsLeg", side, 0.06f, 0.80f, color, 0.022f);
        }

        void PantsShorts()
        {
            var color = new Color32(58, 74, 116, 255);
            PantsHip(color);
            for (int side = -1; side <= 1; side += 2)
                LegShell("ShortLeg", side, 0.44f, 0.80f, color, 0.022f);
        }

        void PantsSkirt()
        {
            float radius = _legSpread + 0.30f * _legScale;
            Cone("Skirt", new Vector3(0f, 0.52f, 0f), radius, 0.62f, Purple);
            Piece(PrimitiveType.Cylinder, "Waistband", new Vector3(0f, 0.80f, 0f),
                new Vector3(radius * 0.70f, 0.025f, radius * 0.70f), Gold);
        }

        void PantsStripes()
        {
            PantsPlain(White);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    LegShell("PantStripe", side, 0.16f + i * 0.20f, 0.22f + i * 0.20f,
                        Blue, 0.030f);
                }
            }
        }

        // ---------------------------------------------------------- cara

        void BuildFaceDetails()
        {
            if (_costumeIndex == 1)
            {
                BuildMonkeyFace();
                return;
            }
            if (_costumeIndex == 2)
            {
                BuildPororoFace();
                return;
            }

            BuildHumanFace();
        }

        void BuildHumanFace()
        {
            // Ya no hay cara humana: BuildPandaBase aporta orejas, parches y
            // hocico. Solo quedan ojos y cejas, sobre los que se montan gafas,
            // bigote o parche sin cambiar el cuerpo base.
            BuildEyes(true);
        }

        /// <summary>Ojos blancos con pupila; se comparten con los atuendos.</summary>
        void BuildEyes(bool brows, float zPush = 0f)
        {
            float s = _headScale;

            for (int i = 0; i < _eyeBasePositions.Count; i++)
            {
                Vector3 basePos = _eyeBasePositions[i];
                float x = basePos.x * s;
                float y = basePos.y;
                float z = basePos.z + (s - 1f) * 0.30f + zPush;

                Piece(PrimitiveType.Sphere, "EyeWhite",
                    new Vector3(x, y, z), Vector3.one * 0.16f * s, White, null, false, 0.45f);
                Piece(PrimitiveType.Sphere, "Pupil",
                    new Vector3(x, y, z + 0.075f * s), Vector3.one * 0.075f * s,
                    Dark, null, false, 0.65f);

                if (!brows) continue;

                var brow = Piece(PrimitiveType.Cube, "Eyebrow",
                    new Vector3(x, y + 0.14f * s, z + 0.02f),
                    new Vector3(0.17f, 0.035f, 0.03f) * s, Hair,
                    Quaternion.Euler(0f, 0f, Mathf.Sign(x) * -12f), false, 0.1f);
                brow.name = "Eyebrow";
            }
        }

        void BuildMonkeyFace()
        {
            float s = _headScale;

            // Orejas y nariz las aporta BuildPandaBase: el mono es un panda
            // con atuendo de mono, no una especie distinta.
            Piece(PrimitiveType.Sphere, "MonkeyMuzzle",
                new Vector3(0f, 1.71f, 0.33f),
                new Vector3(0.31f, 0.21f, 0.15f) * s, MonkeyMuzzle);

            for (int i = 0; i < 5; i++)
            {
                int offset = i - 2;
                Piece(PrimitiveType.Sphere, "MonkeySmile",
                    new Vector3(offset * 0.06f * s,
                        1.66f + offset * offset * 0.010f * s, 0.47f),
                    Vector3.one * 0.022f * s, Dark);
            }

            // Mechón y cola: se añaden aquí porque el atuendo no tiene sombrero.
            Piece(PrimitiveType.Sphere, "MonkeyTuft",
                new Vector3(-0.08f, 2.13f * s, 0f),
                new Vector3(0.13f, 0.09f, 0.12f) * s, MonkeyFur,
                Quaternion.Euler(0f, 0f, 24f));
            BuildMonkeyTail();

            BuildEyes(true, 0.04f);
        }

        void BuildMonkeyTail()
        {
            var points = new[]
            {
                new Vector3(-0.32f, 0.88f, -0.16f),
                new Vector3(-0.52f, 1.02f, -0.38f),
                new Vector3(-0.62f, 1.26f, -0.56f),
                new Vector3(-0.48f, 1.46f, -0.62f),
            };

            for (int i = 0; i < points.Length; i++)
            {
                Piece(PrimitiveType.Sphere, "MonkeyTail", points[i],
                    Vector3.one * (0.10f - i * 0.012f) * _headScale, MonkeyFur);
            }
        }

        void BuildPororoBelly()
        {
            // Parche blanco delante del torso: sigue la superficie de la ropa
            // y deja ver el azul del cuerpo por los lados.
            float y = 1.15f;
            float z = TorsoSurfaceZ(y) + 0.02f;
            Piece(PrimitiveType.Sphere, "PororoBelly",
                new Vector3(0f, y, z),
                new Vector3(0.40f * _bodyWidth, 0.44f, 0.12f), White, null, false, 0.35f);
        }

        void BuildPororoFace()
        {
            float s = _headScale;

            // Cara blanca redonda y pico naranja (Pororo, el pingüino coreano).
            Piece(PrimitiveType.Sphere, "PororoFace",
                new Vector3(0f, 1.87f, 0.29f),
                new Vector3(0.43f, 0.36f, 0.13f) * s, White, null, false, 0.35f);

            BuildEyes(false, 0.11f);

            Cone("PororoBeak", new Vector3(0f, 1.72f, 0.44f), 0.14f * s, 0.25f * s,
                BeakOrange, Quaternion.Euler(90f, 0f, 0f), 0.35f);
            Piece(PrimitiveType.Sphere, "PororoLowerBeak",
                new Vector3(0f, 1.65f, 0.43f),
                new Vector3(0.12f, 0.05f, 0.10f) * s, BeakOrange);
        }

        // ---------------------------------------------------------- rostro

        void BuildFace()
        {
            if (_costumeIndex != 0) return;

            switch (_faceIndex)
            {
                case 1: FaceGlasses(); break;
                case 2: FaceMustache(); break;
                case 3: FaceNose(); break;
                case 4: FacePatch(); break;
                case 5: FaceGlasses(); FaceMustache(); FaceNose(); break;
            }
        }

        void FaceGlasses()
        {
            var rotation = Quaternion.Euler(90f, 0f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(PrimitiveType.Cylinder, "GlassFrame",
                    new Vector3(side * 0.12f, 1.95f, 0.29f),
                    new Vector3(0.17f, 0.012f, 0.17f), Dark, rotation);
                Piece(PrimitiveType.Cylinder, "GlassLens",
                    new Vector3(side * 0.12f, 1.95f, 0.305f),
                    new Vector3(0.14f, 0.010f, 0.14f), Cyan, rotation);
            }
            Piece(PrimitiveType.Cube, "GlassBridge", new Vector3(0f, 1.95f, 0.31f),
                new Vector3(0.08f, 0.02f, 0.02f), Dark);
        }

        void FaceMustache()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(PrimitiveType.Capsule, "Mustache",
                    new Vector3(side * 0.07f, 1.80f, 0.29f),
                    new Vector3(0.10f, 0.035f, 0.10f), Brown,
                    Quaternion.Euler(0f, 0f, side * 20f));
            }
        }

        void FaceNose()
        {
            Piece(PrimitiveType.Sphere, "ClownNose", new Vector3(0f, 1.88f, 0.32f),
                Vector3.one * 0.18f, Red);
        }

        void FacePatch()
        {
            Piece(PrimitiveType.Sphere, "EyePatch", new Vector3(-0.12f, 1.95f, 0.29f),
                new Vector3(0.15f, 0.15f, 0.06f), Dark);
            Piece(PrimitiveType.Cube, "PatchStrap", new Vector3(0f, 2.03f, 0f),
                new Vector3(0.58f, 0.025f, 0.58f), Dark);
        }

        // ---------------------------------------------------------- nombre

        void OnGUI()
        {
            if (string.IsNullOrEmpty(playerName)) return;
            if (Caos.UI.CaosShell.IsBlocked) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 world = transform.position + Vector3.up * NameHeight;
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) return; // detrás de la cámara

            EnsureStyle();

            const float width = 280f;
            const float height = 26f;
            var rect = new Rect(
                screen.x - width * 0.5f,
                Screen.height - screen.y - height * 0.5f,
                width,
                height);

            // Sombra
            _nameStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height),
                playerName, _nameStyle);

            // Texto
            _nameStyle.normal.textColor = Color.white;
            GUI.Label(rect, playerName, _nameStyle);
        }

        void EnsureStyle()
        {
            if (_nameStyle != null) return;

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }
    }
}
