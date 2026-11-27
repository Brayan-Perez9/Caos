using UnityEngine;

namespace Caos.Dungeon
{
    /// <summary>Primitivas con material de la mazmorra y colisionador opcional.</summary>
    public static class DungeonVisuals
    {
        public static GameObject Box(string name, Vector3 position, Vector3 scale,
            Material material, Transform parent = null, bool collider = true, bool trigger = false) =>
            Primitive(PrimitiveType.Cube, name, position, scale, material, parent, collider, trigger);

        public static GameObject Cylinder(string name, Vector3 position, Vector3 scale,
            Material material, Transform parent = null, bool collider = true, bool trigger = false) =>
            Primitive(PrimitiveType.Cylinder, name, position, scale, material, parent, collider, trigger);

        public static GameObject Sphere(string name, Vector3 position, float size,
            Material material, Transform parent = null, bool collider = true, bool trigger = false) =>
            Primitive(PrimitiveType.Sphere, name, position, Vector3.one * size,
                material, parent, collider, trigger);

        public static GameObject Primitive(PrimitiveType type, string name, Vector3 position,
            Vector3 scale, Material material, Transform parent = null,
            bool collider = true, bool trigger = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;

            Collider existing = go.GetComponent<Collider>();
            if (!collider)
            {
                if (existing != null) Object.Destroy(existing);
            }
            else if (trigger && existing != null)
            {
                existing.isTrigger = true;
            }

            return go;
        }

        public static void SetMaterial(GameObject go, Material material)
        {
            if (go == null) return;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }
    }
}
