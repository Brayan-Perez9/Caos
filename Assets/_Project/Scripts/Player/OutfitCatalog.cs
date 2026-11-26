namespace Caos.Player
{
    /// <summary>
    /// Nombres de las opciones de personalización. El orden de cada array es
    /// el índice que se guarda en PlayerPrefs, así que no se debe reordenar.
    /// </summary>
    public static class OutfitCatalog
    {
        public static readonly string[] Hats =
        {
            "Sin gorro",
            "Gorro de fiesta",
            "Sombrero de copa",
            "Corona",
            "Orejas de conejo",
            "Sombrero de vaquero",
            "Gorro de bufón",
            "Sombrero de pizza",
            "Helado",
            "Banana",
        };

        public static readonly string[] Shirts =
        {
            "Camiseta",
            "Rayas",
            "Traje",
            "Chaqueta",
            "Sudadera",
            "Camisa hawaiana",
        };

        public static readonly string[] Pants =
        {
            "Pantalón",
            "Shorts",
            "Falda",
            "Rayas",
        };

        public static readonly string[] Faces =
        {
            "Normal",
            "Gafas",
            "Bigote",
            "Nariz de payaso",
            "Parche",
            "Payaso",
        };

        public static readonly string[] Builds =
        {
            "Normal",
            "Flaco",
            "Gordo",
            "Gordísimo",
        };
    }
}
