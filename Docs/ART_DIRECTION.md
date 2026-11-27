# Dirección artística — Arena Caos (renovación 2026)

## Objetivo

Arena Caos debe leerse como una fiesta 3D colorida, redonda y juguetona: cercana a la
**calidad visual, paleta y formas amables** de juegos de party, con identidad propia.
No se copian personajes ni elementos protegidos. Todo se genera con mallas y shaders
procedurales en runtime: sin arte externo, sin Animator y sin texturas importadas.

## Sistema visual

| Eje | Decisión observable |
|---|---|
| Forma | Chunky y redondeada. Bisel suave en plataformas y obstáculos; copas orgánicas agrupadas, no una esfera perfecta; troncos ligeramente curvos y con ensanchamiento en la base. |
| Silueta | Enemigos, jefes, armas y pickups deben distinguirse por forma y color a 1080p y en movimiento. La hierba y las flores nunca tapan al jugador. |
| Valor | Tres bandas claras: sombra oscura saturada, color base medio y brillo cálido en bordes. Nada de grises neutros prolongados. |
| Color | Césped verde vivo con tres tonos; tierra cálida; acentos cian/coral/amarillo/violeta para interactivos, peligro, recompensa y elemento. |
| Borde | Bisel geométrico de 2-4 segmentos y rim light sutil. Sin contornos negros gruesos. |
| Material | Stylized semi-mate: muy poca rugosidad metálica, highlight blando, fresnel suave, emisión solo en objetos interactivos o elementos. |
| Luz | Sol cálido direccional equilibrado desde arriba-izquierda, relleno frío bajo, sombras suaves y cielo pastel fijo. Sin niebla y sin bloom, tonemap, vignette ni ajustes de color globales: solo antialiasing de cámara. La profundidad la dan suelo lejano, lomas verdes, montañas y nubes alejadas y discretas. |
| Detalle | Concentrado en bordes, copas, flores, rocas, lomas y HUD. El horizonte se compone de montañas redondas y nubes fuera del área de juego. La repetición se rompe con ruido en dos escalas, rocío procedural y variación por semilla. Las luces de hito son bombillas de color con intensidad baja: iluminan sin lavar la imagen. |
| Cámara | Seguimiento suave en `LateUpdate`, encuadre con ligero look-ahead, shake solo en impactos y FOV que respira al correr. |
| Movimiento | Todo se mueve con senos de baja frecuencia y tiempo real: hierba, copas, nubes, luciérnagas e iconos de HUD. Amplitud pequeña; nunca mareante. |

## Paleta semántica

- Pasto base `#1E6B3A`, medio `#35A85C`, hoja iluminada `#8FE388`.
- Tierra `#6B4A2F`, reflejo `#A67C52`, grietas `#3A2A1C`.
- Cielo `#8FE9FF` → `#FFD6A5`.
- Interactivo/cian `#35D0FF`; peligro/coral `#FF6B6B`; recompensa `#FFD166`; elemento/violeta `#7B61FF`.
- HUD: panel azul marino `#0B1B2B` al 82 %, texto `#F7FBFF`, acento amarillo para nivel y rojo para vida baja.

## Reglas de producción

1. La geometría visual va en hijos; los colliders originales no se tocan.
2. Se reutilizan mallas y materiales cacheados; se marcan los objetos estáticos para batching.
3. Las variaciones se derivan de una semilla fija por sistema; cambiar lluvia no reordena árboles.
4. El HUD conserva todos sus eventos y referencias; solo cambia su presentación.
5. Los efectos usan tiempo real cuando el juego está en pausa o hit-stop.
6. Se ofrecen opciones de menos movimiento y menos destellos en pausa.

## No hacer

- No convertir la arena en una malla única gigante: se perdería batching y feedback.
- No añadir postprocesado costoso sin medirlo antes y después.
- No usar `UnityEngine.Input`, Animator, assets de pago ni arte sin licencia clara.
- No sustituir scripts de combate, spawn, vida, jefes o multijugador por versiones nuevas.
