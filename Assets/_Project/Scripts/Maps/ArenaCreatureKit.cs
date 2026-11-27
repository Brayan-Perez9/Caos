using System.Collections.Generic;
using UnityEngine;

namespace Caos.Maps
{
    /// <summary>
    /// Piezas procedurales compartidas por monstruos y jefes: mallas cacheadas,
    /// materiales toon cacheados y extremidades con pivote. No crea colliders,
    /// no usa Animator y no toca el CharacterController de la criatura.
    /// </summary>
    public static class ArenaCreatureKit
    {
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static Material Toon(Color baseColor, Color shadow, Color rim,
            float rimStrength = 0.22f, float smoothness = 0.24f, float emission = 0f)
        {
            string key = string.Concat(
                ColorUtility.ToHtmlStringRGBA(baseColor), "|",
                ColorUtility.ToHtmlStringRGBA(shadow), "|",
                ColorUtility.ToHtmlStringRGBA(rim), "|",
                rimStrength.ToString("0.###"), "|",
                smoothness.ToString("0.###"), "|",
                emission.ToString("0.###"));

            if (Materials.TryGetValue(key, out Material cached)) return cached;

            Shader shader = Shader.Find("Caos/ToonStylized");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var material = new Material(shader) { name = "CaosCreature_" + key };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);
            if (material.HasProperty("_ShadowColor")) material.SetColor("_ShadowColor", shadow);
            if (material.HasProperty("_RimColor")) material.SetColor("_RimColor", rim);
            if (material.HasProperty("_RimPower")) material.SetFloat("_RimPower", 3.2f);
            if (material.HasProperty("_RimStrength")) material.SetFloat("_RimStrength", rimStrength);
            if (material.HasProperty("_Emission")) material.SetFloat("_Emission", emission);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

            Materials[key] = material;
            return material;
        }

        /// <summary>Pieza visual sin colider. El color base se guarda para el flash.</summary>
        public static Transform Make(Transform parent, string name, Mesh mesh, Material material,
            Vector3 localPosition, Vector3 localScale, Quaternion localRotation,
            List<Renderer> renderers, List<Color> colors, Color baseColor)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            var filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (renderers != null) renderers.Add(renderer);
            if (colors != null) colors.Add(baseColor);
            return part.transform;
        }

        /// <summary>
        /// Extremidad con pivote arriba: rotar el pivote balancea la pieza desde
        /// el hombro o la cadera, no desde su centro.
        /// </summary>
        public static Transform MakeLimb(Transform parent, string name, float radius, float length,
            Material material, Vector3 localPosition, Quaternion localRotation,
            Color baseColor, List<Renderer> renderers, List<Color> colors)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = localPosition;
            pivot.localRotation = localRotation;

            Make(pivot, name + "Mesh", CaosVisualKit.RoundedCylinder(0.5f, 2f, 10, 2),
                material, new Vector3(0f, -length * 0.5f, 0f),
                new Vector3(radius * 2f, length * 0.5f, radius * 2f),
                Quaternion.identity, renderers, colors, baseColor);
            return pivot;
        }

        /// <summary>Cambia el color de la instancia del renderer sin tocar el material cacheado.</summary>
        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            Material material = renderer.material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }
}
