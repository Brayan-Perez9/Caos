# Art direction brief — Caos (personaje y accesorios)

## Game frame

- Player fantasy: muñeco de físicas cómicas en un sandbox de empujones, caídas y minijuegos.
- Core verbs: mover, saltar, empujar, personalizar, caer, rebotar.
- Engine and renderer: Unity 6000.3.29f1, URP 17.3.0 (Universal Render Pipeline/Lit).
- Target platforms: Windows (build `Builds/Windows/Caos.exe`).
- Camera/view/facing: cámara cenital estilo LoL en partida (perspectiva 50°, pitch +55°, yaw 0);
  cámara frontal en el menú de personalización (perspectiva 50°). El personaje mira +Z, arriba +Y,
  unidad = 1 metro, pivote en los pies (0,0,0).
- Native viewport and common display scale: ventana de escritorio; HUD OnGUI escalado a píxeles.
- Typical asset size on screen: personaje ~120–260 px de alto en partida según zoom (6–24 m).

## Visual system

- Shape language: formas redondeadas y gruesas (esferas, cápsulas, conos y cilindros); el torso
  va de la cintura (y=0.72) a los hombros (y=1.8) con cabeza sobredimensionada (~1:2.2). Las
  piernas son **dos mallas separadas** (y=0.03–0.80, separación 0.24·legScale) con hueco visible
  entre ellas. Los **brazos** son cápsulas separadas a y=1.15, a `rx + radio + 0.04` del torso,
  con holgura visible en todos los cuerpos. Los cubos se reservan para zapatos, hebillas y
  detalles planos.
- Silhouette priorities: el gorro debe separarse de la cabeza; la panza del personaje gordo
  debe sobresalir **del mismo perfil del torso, nunca como una esfera añadida**; la ropa se
  dibuja como capas de ese mismo perfil; los zapatos se leen desde arriba; la etiqueta del
  nombre va por encima del gorro más alto (y = 3.05).
- Value structure: tres bandas — oscuro (ojos, cejas, sombreros, suelas), medio (piel/color del
  jugador, telas) y brillante (blanco, dorado, cian) para los puntos de interés.
- Palette roles and exact swatches:
  - Jugador (variable): `#E2554F` por defecto, aplicado a cuerpo, cabeza, orejas, nariz y panza.
  - Neutro oscuro: `#24263A`. Blanco: `#F5F5F0`. Oro: `#F6C444`. Rojo: `#E24E4E`.
  - Cian: `#56CCF2`. Azul: `#2F80ED`. Morado: `#8751E0`. Rosa: `#EB5FA8`.
  - Café: `#8C6A4A`. Pelo: `#543A2A`. Albahaca: `#5EB260`.
- Materials and surface cues: tela mate (smoothness 0.10–0.18, metallic 0), piel suave
  (0.25), plástico/candy (0.5), metal (0.65–0.85, metallic 0.55–0.65). Sin texturas: el color
  plano y la forma sostienen la lectura.
- Edge/line treatment: bordes suaves por geometría, sin contorno de tinta; el contraste de valor
  separa las piezas.
- Lighting direction and contrast: direccional cálida `(1, 0.96, 0.88)` a 52°/-38°, intensidad
  1.2, sombras suaves; ambiente trilight con cielo frío y suelo cálido.
- Detail density and focal hierarchy: detalle concentrado en cara y gorro; el torso lleva como
  máximo una franja, corbata o estampado; las piernas y los pies quedan simples. Las capas de
  ropa usan el mismo perfil que el cuerpo con un `inflate` de 0.026–0.038 m, así nunca quedan
  dentro de la panza ni se separan al rebotar.
- Motion character: elástico. La panza deforma el torso y la ropa con el mismo resorte; brazos
  usan resorte propio. En la vitrina del menú hay respiración amplificada para que el balanceo se
  aprecie; en partida responde a aceleración y velocidad. Deformación máxima ±0.14 m en la panza
  y ±0.20 m en brazos, squash/stretch de ±12%.
- Explicit exclusions: sin texto sobre las mallas, sin luces horneadas en el color base, sin
  texturas externas, sin assets de terceros, sin líneas de contorno negras alrededor de todo.

## Technical contract

- Asset dimensions/aspect: mallas procedurales en tiempo de ejecución; sin mapas de imagen.
- Alpha/background: no aplica (geometría opaca).
- Grid/tile/frame size: no aplica.
- Anchor/pivot/baseline: raíz del personaje en los pies; las piezas se parentan a `Outfit`.
- Filtering/mipmaps/compression: no aplica; materiales URP Lit compartidos por perfil.
- Color space: sRGB para los colores base; sin mapas de datos.
- Texture/poly/material budgets: 0 texturas; < 40 piezas por personaje; materiales por instancia
  con perfiles reutilizables; colisiones simplificadas (CharacterController del jugador).
- Naming and folders: `Assets/_Project/Scripts/Player/CharacterCustomization.cs` genera las
  familias `Shirt`, `Pants`, `Belly`, `Hat*`, `Eye*`, `Smile`; paleta en `CharacterPalette.cs`.

## Visual target

- Approved seed/reference paths: `Assets/_Project/Scripts/Player/CharacterCustomization.cs`
  (constructores `BuildFaceDetails`, `BuildHat`, `BuildFatExtras`).
- Required do/don't examples:
  - SÍ: gorro con contenido distinguible a 120 px; ojos con blanco + pupila; sonrisa en arco.
  - NO: sombrero que se funde con la cabeza; panza que tapa la boca; colores de piel distintos
    entre cuerpo y panza; detalles que se corten al hacer zoom máximo.
- Native-scale gameplay capture: pendiente de revisión del usuario en el ejecutable.
- Approval owner/date: usuario del proyecto, 2026-11-26 (dirección propuesta, pendiente de validar).
