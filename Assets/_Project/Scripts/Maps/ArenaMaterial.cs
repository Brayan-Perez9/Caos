using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Materiales URP creados en runtime para que nada salga magenta.</summary>
    public static class ArenaMaterial
    {
        static Shader _shader;

        public static Material Create(Color color, float metallic = 0f, float smoothness = 0.3f)
        {
            if (_shader == null)
            {
                _shader = Shader.Find("Universal Render Pipeline/Lit");
                if (_shader == null) _shader = Shader.Find("Standard");
                if (_shader == null) _shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(_shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }
    }
}
