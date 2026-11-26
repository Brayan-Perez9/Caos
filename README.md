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
- **ESC**: pausa (reanudar / volver al lobby / salir)

## Objetivos de la misión

1. Tumbar 3 dianas.
2. Recoger 5 paquetes.
3. Coger el martillo y derrotar al jefe.

Hay monstruos cada vez más rápidos, eventos aleatorios y patitos secretos.

## Actualizaciones

Cada `git push` a `main` dispara `.github/workflows/build-release.yml`, compila el juego y reemplaza el ZIP de la Release `latest`. El juego comprueba el último commit al abrir el menú y ofrece **ACTUALIZAR Y REINICIAR**; descarga el ZIP, espera a que cierre y vuelve a abrir solo.

Para que GitHub Actions pueda compilar con Unity, añade estos secrets al repo:

- `UNITY_LICENSE`: licencia base64 de Unity (GameCI).
- `UNITY_EMAIL` y `UNITY_PASSWORD`: opcionales, si usas inicio de sesión por usuario.

Más info: https://game.ci/docs/github/activation

## Desarrollo

El proyecto usa Unity 6000.3.29f1. Todas las escenas se regeneran desde `Assets/_Project/Scripts/Editor`:

- `MenuSceneBuilder.CreateMenuSceneInternal`
- `LobbySceneBuilder.CreateLobbySceneInternal`
- `ChaosExpressBuilder.CreateMapInternal`
- `SandboxSceneBuilder.CreateSandboxSceneInternal`

Build local: `Caos.EditorTools.BuildCaos.BuildWindows` → `Builds/Windows/Caos.exe`.
