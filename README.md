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
- **ESC**: pausa (reanudar / volver al lobby / salir)

## Objetivos de la misión

1. Tumbar 3 dianas.
2. Recoger 5 paquetes.
3. Coger el martillo y derrotar al jefe.

Hay monstruos cada vez más rápidos, eventos aleatorios y patitos secretos.

## Arena Caos

En el menú pulsa **ARENA** para entrar en un mapa grande con dos pisos. Te acercas a un pedestal para recoger el arma y apuntas con el ratón mientras disparas con el botón izquierdo. Hay una escalera para subir al piso alto; los monstruos y los jefes la usan y respetan paredes, cajas y columnas en vez de atravesarlas.

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
