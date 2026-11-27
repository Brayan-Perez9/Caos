using UnityEngine;

namespace Caos.Player
{
    /// <summary>Una skin completa: color del cuerpo y piezas del catálogo.</summary>
    public sealed class CaosSkinDefinition
    {
        public readonly string id;
        public readonly string title;
        public readonly string description;
        public readonly int price;
        public readonly Color color;
        public readonly int hat;
        public readonly int shirt;
        public readonly int pants;
        public readonly int face;
        public readonly int build;
        public readonly int costume;

        public CaosSkinDefinition(string id, string title, string description, int price,
            Color color, int hat, int shirt, int pants, int face, int build, int costume)
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.price = Mathf.Max(0, price);
            this.color = color;
            this.hat = hat;
            this.shirt = shirt;
            this.pants = pants;
            this.face = face;
            this.build = build;
            this.costume = costume;
        }
    }

    /// <summary>
    /// Catálogo de la tienda. No usa assets externos: cada skin recomina
    /// índices ya existentes de OutfitCatalog, así que el avatar en red sigue
    /// enviando los mismos campos de siempre.
    /// </summary>
    public static class CaosSkinCatalog
    {
        static Color C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        public static readonly CaosSkinDefinition[] Skins =
        {
            new CaosSkinDefinition("clasico", "CLÁSICO", "El campeón de siempre", 0,
                C(226, 85, 79), 0, 0, 0, 0, 0, 0),

            new CaosSkinDefinition("rayo", "CABALLERO RAYO", "Traje, corona y mirada eléctrica", 90,
                C(72, 190, 232), 3, 2, 3, 1, 0, 0),

            new CaosSkinDefinition("rey", "REY DEL CAOS", "Corona dorada y ropa de fiesta", 140,
                C(255, 198, 70), 3, 5, 0, 5, 2, 0),

            new CaosSkinDefinition("vaquero", "VAQUERO CÓSMICO", "Sombrero, chaqueta y parche", 110,
                C(196, 118, 64), 5, 3, 1, 4, 0, 0),

            new CaosSkinDefinition("bombonera", "BOMBONERA", "Helado, payaso y colores dulces", 80,
                C(255, 132, 190), 8, 5, 2, 5, 1, 0),

            new CaosSkinDefinition("mono", "MONO LOCO", "Disfraz de mono con orejas de conejo", 120,
                C(139, 92, 60), 4, 0, 0, 0, 0, 1),

            new CaosSkinDefinition("pororo", "PORORO DE FUEGO", "El atuendo azaroso convertido en skin", 150,
                C(88, 170, 236), 8, 2, 0, 0, 0, 2),
        };

        public static CaosSkinDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < Skins.Length; i++)
                if (string.Equals(Skins[i].id, id.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return Skins[i];
            return null;
        }
    }
}
