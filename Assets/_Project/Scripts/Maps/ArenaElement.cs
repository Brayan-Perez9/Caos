using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Elemento de cada arma. Define el color de la bala y su efecto.</summary>
    public enum ArenaElement
    {
        Normal,
        Fuego,
        Aire,
        Hielo,
        Rayo,
        Veneno
    }

    /// <summary>Colores y textos de los elementos, generados en runtime.</summary>
    public static class ArenaElementInfo
    {
        public static Color Color(ArenaElement element)
        {
            switch (element)
            {
                case ArenaElement.Fuego:
                    return new Color(1f, 0.34f, 0.08f);
                case ArenaElement.Aire:
                    return new Color(0.55f, 0.95f, 1f);
                case ArenaElement.Hielo:
                    return new Color(0.42f, 0.72f, 1f);
                case ArenaElement.Rayo:
                    return new Color(1f, 0.90f, 0.18f);
                case ArenaElement.Veneno:
                    return new Color(0.38f, 0.95f, 0.35f);
                default:
                    return new Color(1f, 0.86f, 0.32f);
            }
        }

        public static string Label(ArenaElement element)
        {
            switch (element)
            {
                case ArenaElement.Fuego: return "FUEGO";
                case ArenaElement.Aire: return "AIRE";
                case ArenaElement.Hielo: return "HIELO";
                case ArenaElement.Rayo: return "RAYO";
                case ArenaElement.Veneno: return "VENENO";
                default: return "NORMAL";
            }
        }

        public static string Effect(ArenaElement element)
        {
            switch (element)
            {
                case ArenaElement.Fuego: return "quema al enemigo";
                case ArenaElement.Aire: return "empuja y dispara más rápido";
                case ArenaElement.Hielo: return "ralentiza al enemigo";
                case ArenaElement.Rayo: return "salta a otro enemigo";
                case ArenaElement.Veneno: return "envenena al enemigo";
                default: return "sin efecto especial";
            }
        }
    }
}
