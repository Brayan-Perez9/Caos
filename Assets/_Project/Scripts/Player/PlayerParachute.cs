using System.Collections.Generic;
using UnityEngine;

namespace Caos.Player
{
    /// <summary>
    /// Paracaídas automático: si el jugador salta desde el piso alto y empieza
    /// a caer, despliega una cúpula procedural y limita la velocidad de bajada.
    /// Se retira al aterrizar. No usa animaciones ni assets externos.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerParachute : MonoBehaviour
    {
        const float UpperFloorY = 2.8f;

        [SerializeField] float canopyHeight = 3.2f;
        [SerializeField] float fallSpeed = -2.3f;
        [SerializeField] float deployDrop = 1.1f;

        PlayerController _controller;
        bool _deployed;
        float _lastGroundedY = float.NegativeInfinity;
        GameObject _root;
        readonly List<Material> _materials = new List<Material>();
        readonly List<Mesh> _meshes = new List<Mesh>();

        static Shader _litShader;
        static readonly Color CanopyWhite = new Color32(246, 246, 240, 255);
        static readonly Color CanopyRed = new Color32(226, 62, 62, 255);
        static readonly Color RopeColor = new Color32(224, 214, 178, 255);

        void Awake() => _controller = GetComponent<PlayerController>();

        void Update()
        {
            if (_controller == null) return;

            if (_controller.IsGrounded)
            {
                _lastGroundedY = transform.position.y;
                if (_deployed) Retract();
                return;
            }

            if (!_deployed && _lastGroundedY > UpperFloorY &&
                transform.position.y < _lastGroundedY - deployDrop &&
                _controller.Velocity.y < 0f)
            {
                Deploy();
            }

            if (!_deployed) return;

            _controller.FallSpeedLimit = fallSpeed;
            _controller.AirControlMultiplier = 0.72f;
            Follow();
        }

        void OnDisable()
        {
            Retract();
            ClearLimits();
        }

        void OnDestroy()
        {
            Retract();
        }

        /// <summary>Corta el paracaídas al reaparecer o cambiar de escena.</summary>
        public void Cancel()
        {
            Retract();
            _lastGroundedY = float.NegativeInfinity;
        }

        void Deploy()
        {
            if (_deployed) return;
            _deployed = true;

            _root = new GameObject("Paracaidas");
            _root.transform.position = transform.position + Vector3.up * canopyHeight;
            BuildCanopy(_root.transform);
        }

        void Retract()
        {
            if (_root != null) Destroy(_root);
            _root = null;

            foreach (var material in _materials)
                if (material != null) Destroy(material);
            foreach (var mesh in _meshes)
                if (mesh != null) Destroy(mesh);

            _materials.Clear();
            _meshes.Clear();
            _deployed = false;
            ClearLimits();
        }

        void ClearLimits()
        {
            if (_controller == null) return;
            _controller.FallSpeedLimit = float.NegativeInfinity;
            _controller.AirControlMultiplier = 1f;
        }

        void Follow()
        {
            if (_root == null) return;

            _root.transform.position = transform.position + Vector3.up * canopyHeight;
            float tiltX = Mathf.Clamp(-_controller.Velocity.z * 1.1f, -14f, 14f);
            float tiltZ = Mathf.Clamp(_controller.Velocity.x * 1.1f, -14f, 14f);
            _root.transform.rotation = Quaternion.Euler(tiltX, 0f, tiltZ);
        }

        // ------------------------------------------------------------ visual

        void BuildCanopy(Transform root)
        {
            AddMesh(root, "Cupula", DomeMesh(1.62f, 0.62f, 24, 6, 0f, 360f, 0f),
                CanopyWhite);

            for (int i = 0; i < 6; i++)
            {
                AddMesh(root, "Franja", DomeMesh(1.65f, 0.65f, 4, 6, i * 60f, 30f, 0.03f),
                    CanopyRed);
            }

            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Vector3 rim = new Vector3(Mathf.Cos(angle) * 1.45f, 0f,
                    Mathf.Sin(angle) * 1.45f);
                Vector3 anchor = new Vector3(Mathf.Cos(angle) * 0.24f,
                    -canopyHeight + 1.05f, Mathf.Sin(angle) * 0.24f);
                AddRope(root, rim, anchor);
            }
        }

        void AddMesh(Transform root, string name, Mesh mesh, Color color)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(root, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(color);
            _meshes.Add(mesh);
        }

        void AddRope(Transform root, Vector3 from, Vector3 to)
        {
            var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.name = "Cuerda";
            rope.transform.SetParent(root, false);

            Vector3 delta = to - from;
            rope.transform.position = (from + to) * 0.5f;
            rope.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            rope.transform.localScale = new Vector3(0.018f, delta.magnitude * 0.5f, 0.018f);

            Destroy(rope.GetComponent<Collider>());
            rope.GetComponent<Renderer>().sharedMaterial = MakeMaterial(RopeColor);
        }

        Material MakeMaterial(Color color)
        {
            if (_litShader == null)
                _litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var material = new Material(_litShader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);

            _materials.Add(material);
            return material;
        }

        /// <summary>Cúpula abierta por la base, con franjas angulares.</summary>
        static Mesh DomeMesh(float radius, float height, int segments, int rings,
            float startDeg, float sweepDeg, float inflate)
        {
            var mesh = new Mesh { name = "ParachuteDome" };
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var uvs = new Vector2[vertices.Length];

            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float radial = Mathf.Sin(t * Mathf.PI * 0.5f) * radius + inflate * t;
                float y = Mathf.Cos(t * Mathf.PI * 0.5f) * height;

                for (int segment = 0; segment <= segments; segment++)
                {
                    float angle = (startDeg + sweepDeg * segment / (float)segments)
                        * Mathf.Deg2Rad;
                    int index = ring * (segments + 1) + segment;

                    vertices[index] = new Vector3(
                        Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial);
                    uvs[index] = new Vector2(segment / (float)segments, t);
                }
            }

            var triangles = new int[rings * segments * 6];
            int cursor = 0;
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = ring * (segments + 1) + segment;
                    int b = a + segments + 1;

                    triangles[cursor++] = a;
                    triangles[cursor++] = a + 1;
                    triangles[cursor++] = b;

                    triangles[cursor++] = a + 1;
                    triangles[cursor++] = b + 1;
                    triangles[cursor++] = b;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
