# Ilustraciones de la entrada

La herramienta de generación de imágenes no estuvo disponible en esta sesión
(credencial rechazada), así que **no se han generado archivos de imagen**.
Mientras tanto, `CaosFrontendArt` dibuja fondos procedurales con la misma
paleta para que la navegación sea funcional.

Para sustituir los fondos procedurales, coloca aquí estos archivos (PNG/JPG,
16:9, recomendado 1920x1080 o superior). Unity los importa como `Texture2D` y
`Resources.LoadAsync<Texture2D>` los usa automáticamente; no hace falta tocar
código ni recompilar:

| Archivo | Uso | Contenido sugerido |
| --- | --- | --- |
| `StartupBackground.png` | Pantalla de carga inicial y fondo del lobby | Arena cartoon flotante, confeti, portales, monstruos alegres. Sin texto. |
| `CustomizationBackground.png` | Fondo de PERSONALIZAR | Bosque mágico o taller fantasy luminoso. Sin texto. |
| `DungeonCard.png` | Ilustración de la tarjeta MAZMORRA | Corredor de piedra, antorchas, cofre y portal. Sin texto. |
| `ArenaCard.png` | Ilustración de la tarjeta ARENAS | Arena circular colorida, banderas y multitud. Sin texto. |

Deja la zona central/izquierda relativamente despejada cuando la imagen se use
de fondo, porque encima se dibuja el personaje 3D existente y la interfaz.
Todo el texto, incluido el crédito `omar hpta`, se construye en UI de Unity.
