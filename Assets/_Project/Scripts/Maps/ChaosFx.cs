using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Efectos baratos de confeti para los golpes y las muertes.</summary>
    public static class ChaosFx
    {
        public static void Burst(Vector3 position, Color color, int count = 8)
        {
            for (int i = 0; i < count; i++)
            {
                var bit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bit.name = "Confetti";
                bit.transform.position = position;
                bit.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);
                bit.GetComponent<Renderer>().material.color = color;
                bit.AddComponent<Rigidbody>().linearVelocity = new Vector3(
                    Random.Range(-5f, 5f), Random.Range(2f, 7f), Random.Range(-5f, 5f));
                Object.Destroy(bit, 1.6f);
            }
        }
    }
}
