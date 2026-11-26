using UnityEngine;

namespace Caos.UI
{
    /// <summary>
    /// Colores disponibles para el personaje.
    /// Compartidos por la pantalla de inicio y por el menú in-game (tecla P).
    /// </summary>
    public static class CharacterPalette
    {
        public static readonly Color[] Colors =
        {
            new Color32(226, 85, 79, 255),   // rojo
            new Color32(242, 153, 74, 255),  // naranja
            new Color32(242, 201, 76, 255),  // amarillo
            new Color32(111, 207, 151, 255), // verde
            new Color32(86, 204, 242, 255),  // cian
            new Color32(47, 128, 237, 255),  // azul
            new Color32(155, 81, 224, 255),  // morado
            new Color32(235, 95, 168, 255),  // rosa
            new Color32(245, 245, 245, 255), // blanco
            new Color32(140, 106, 74, 255),  // marrón
        };
    }
}
