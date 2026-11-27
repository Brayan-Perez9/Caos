# Caos Express

Juego corto de acción procedural con lobby multijugador por código de sala.

## Descargar el juego

Descarga siempre la última versión desde:

**https://github.com/Brayan-Perez9/Caos/releases/latest/download/Caos-Windows.zip**

Extrae el ZIP en una carpeta (por ejemplo, Escritorio > Caos) y abre `Caos.exe`.

## Jugar con un amigo

1. En el menú, pulsa **LOBBY MULTIJUGADOR** (botón independiente, arriba a la izquierda).
2. El anfitrión pulsa **CREAR SALA**.
3. El juego muestra un código tipo `CAOS-XXXXXXXXXX`; pulsa **COPIAR CÓDIGO** y envíalo.
4. Tu amigo pulsa **LOBBY MULTIJUGADOR**, escribe el código y pulsa **UNIRSE**.
5. El anfitrión pulsa **INICIAR MISIÓN**.

Los dos jugadores deben estar en la misma Wi-Fi o LAN. El código guarda la IP y el puerto; no necesita links ni servidor externo.

## Controles

- **WASD**: mover
- **Espacio**: saltar
- **Clic izquierdo**: coger / lanzar objetos; con el martillo, golpear
- **Ratón**: apuntar en la Arena Caos (el personaje mira al cursor)
- **1 / 2 / 3**: elegir mejora cuando aparece el panel
- **ESC**: pausa (reanudar / menos movimiento de cámara / menos destellos / volver al lobby / salir)
- **P**: personalizar (con la pestaña **ATUENDO** puedes poner el **Mono** o **Pororo**)

Si saltas desde el segundo piso de la Arena Caos se abre un **paracaídas automático**: caes despacio y puedes dirigirte mientras desciendes. Se recoge al aterrizar.

## Objetivos de la misión

1. Tumbar 3 dianas.
2. Recoger 5 paquetes.
3. Coger el martillo y derrotar al jefe.

Hay monstruos cada vez más rápidos, eventos aleatorios y patitos secretos.

## Arena Caos

En el menú pulsa **ARENA** para entrar en un mapa grande con dos pisos. Te acercas a un pedestal para recoger el arma y apuntas con el ratón mientras disparas con el botón izquierdo. Hay una escalera con meseta y puentes para subir al piso alto; los monstruos y los jefes la recorren completa (sin quedarse parados en los peldaños) y respetan paredes, cajas y columnas en vez de atravesarlas.

Cada arma tiene un color y un elemento:

| Color | Arma | Efecto |
| --- | --- | --- |
| Amarillo | Pistola normal | Sin efecto especial |
| Naranja | Escopeta de fuego | Quema al enemigo |
| Celeste | Rifle de aire | Empuja y dispara más rápido |
| Azul | Pistola de hielo | Ralentiza al enemigo |
| Dorado | Escopeta de rayo | El disparo salta a otro enemigo |
| Verde | Rifle de veneno | Envenena al enemigo |

- Cada 5 bajas aparece un panel con 3 mejoras: más vida, más daño, disparo rápido, velocidad o resistencia.
- El arma sube de nivel automáticamente con las bajas: más daño, cadencia y, con la escopeta, más perdigones.
- A los **30 s** los monstruos se vuelven más rápidos y aparecen más.
- A los **90 s**, **120 s** y **150 s** llegan tres jefes: **Martillo** (pisotón), **Cañón** (ráfaga de 3 disparos) y **Tormenta** (teletransporte, lluvia de proyectiles y clones). Derrota a los tres para ganar.
- Los monstruos tienen piernas y sangran; al recibir daño aparece sangre también en pantalla y se oyen efectos generados por código, sin archivos de sonido.

## Ambiente y guiado

Los mapas combinan el shader URP procedural `Caos/Grass` (hojas con viento y manchas de tierra) con materiales `Caos/ToonStylized` en plataformas, barandillas y obstáculos: bisel redondeado, sombra saturada, brillo suave y rim sutil, sin texturas ni assets externos. Hay cielo pastel fijo, luz cálida con relleno frío, bloom muy suave, pasto con dos escalas de detalle y rocío procedural, lomas y montañas de fondo, nubes alejadas y bombillas de color con luz tenue, sin niebla ni postes que laven la visión; también hay **franjas guía** que llevan del spawn a la escalera, de la escalera al jefe y, en Caos Express, de cada estación a la siguiente. El hueco sur de Caos Express está marcado con una franja roja.

El ambiente se genera en runtime con `CaosAmbience`: **árboles azules con textura de pasto, arbustos redondos, hierba que ondea, nubes que derivan, luciérnagas parpadeantes y lluvia** (Arena y Caos Express), todo con primitivas y mallas propias, sin Animator ni arte externo. El suelo de los tres mapas usa el shader procedural `Caos/Grass` (hojas con viento y manchas de tierra) y las escaleras, rampas y rellanos usan `Caos/Dirt` (grano y franjas de talud). Cada mapa usa una semilla fija y flujos de RNG independientes por sistema, así que la decoración siempre sale igual y cambiar la lluvia no reordena los árboles. Los árboles se colocan solo en el borde, lejos del pasillo central y del spawn, para no tapar la ruta.

El diseño se ha revisado con las métricas reales del controlador: salto de **1,15 m** y alcance de unos **4,2 m** corriendo. Los peldaños suben **0,28 m** (por debajo del `stepOffset` de 0,35/0,42), la meseta superior empieza en **4,25 m** y hay puentes a ambos lados: la ruta crítica no exige saltos imposibles ni callejones sin salida. Las dianas de Caos Express se cuentan como tumbadas si se lanzan lejos, así que no pueden bloquear la misión.

Los impactos tienen feedback por capas (chispas, rebote, destello corto y sacudida de cámara con `trauma²` que decae con tiempo real). La pausa incluye **CÁMARA: MENOS MOVIMIENTO** y **DESTELLOS: REDUCIDOS** para quien prefiera menos movimiento o menos brillo. Ningún efecto mueve colisiones ni cambia la cadencia de disparo.

## Actualizaciones

Cada `git push` a `main` dispara `.github/workflows/build-release.yml`, compila el juego y reemplaza el ZIP de la Release `latest`. El juego comprueba el último commit al abrir el menú y ofrece **ACTUALIZAR Y REINICIAR**; descarga el ZIP, espera a que cierre y vuelve a abrir solo.

El workflow usa un runner self-hosted `Caos-PC` instalado en `C:\Users\bcgut\actions-runner-caos` y la licencia de Unity de tu PC, así que no hacen falta secrets. Para que funcione, el PC debe estar encendido y el runner en línea. Hay un acceso directo `IniciarRunnerCaos.bat` en el inicio de Windows y una copia en la raíz del repo. Puedes comprobar el estado con:

```bash
gh api /repos/Brayan-Perez9/Caos/actions/runners --jq ".runners[] | {name,status}"
```

## Desarrollo

El proyecto usa Unity 6000.3.29f1. Todas las escenas se regeneran desde `Assets/_Project/Scripts/Editor`:

- `MenuSceneBuilder.CreateMenuSceneInternal`
- `LobbySceneBuilder.CreateLobbySceneInternal`
- `ChaosExpressBuilder.CreateMapInternal`
- `ArenaSceneBuilder.CreateArenaSceneInternal`
- `SandboxSceneBuilder.CreateSandboxSceneInternal`

Build local: `Caos.EditorTools.BuildCaos.BuildWindows` → `Builds/Windows/Caos.exe`.
