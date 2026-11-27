using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Caos.UI
{
    /// <summary>
    /// Comprueba el último commit de GitHub al abrir el menú. Si hay versión
    /// nueva, descarga el ZIP de la Release "latest", escribe un pequeño
    /// actualizador .bat/.ps1, cierra el juego y vuelve a abrirlo ya actualizado.
    /// </summary>
    public class CaosUpdater : MonoBehaviour
    {
        const string Repo = "Brayan-Perez9/Caos";
        const string ReleaseApi = "https://api.github.com/repos/" + Repo + "/releases/latest";
        const string DownloadUrl = "https://github.com/" + Repo + "/releases/latest/download/Caos-Windows.zip";
        const string VersionFile = "caos-version.txt";
        const string ZipName = "CaosUpdate.zip";

        [Serializable]
        class ReleaseInfo
        {
            public string body;
        }

        string _localVersion = "local";
        string _remoteVersion = string.Empty;
        string _status = "Comprobando actualizaciones…";
        bool _checking;
        bool _busy;
        bool _updateAvailable;
        bool _dismissed;
        float _progress;

        GUIStyle _titleStyle;
        GUIStyle _textStyle;
        GUIStyle _smallStyle;
        GUIStyle _buttonStyle;
        GUIStyle _smallButtonStyle;

        void Start()
        {
            _localVersion = ReadVersion();
            StartCoroutine(CheckVersion());
        }

        static string ReadVersion()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, VersionFile);
                return File.Exists(path) ? File.ReadAllText(path).Trim() : "local";
            }
            catch
            {
                return "local";
            }
        }

        IEnumerator CheckVersion()
        {
            _checking = true;

            using (var request = UnityWebRequest.Get(ReleaseApi))
            {
                request.SetRequestHeader("User-Agent", "CaosUpdater");
                request.SetRequestHeader("Accept", "application/vnd.github+json");

                var operation = request.SendWebRequest();
                while (!operation.isDone) yield return null;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    _status = "Sin conexión. Usa DESCARGAR ZIP para obtener la última versión.";
                    _checking = false;
                    yield break;
                }

                ReleaseInfo release = null;
                try
                {
                    release = JsonUtility.FromJson<ReleaseInfo>(request.downloadHandler.text);
                }
                catch
                {
                    release = null;
                }

                _checking = false;

                // Se compara contra el commit de la release publicada, no contra main.
                // Así no aparece el botón mientras un build está en cola o fue cancelado.
                _remoteVersion = ExtractCommit(release != null ? release.body : null);
                if (string.IsNullOrEmpty(_remoteVersion))
                {
                    _status = "No se pudo leer la versión publicada.";
                    yield break;
                }

                _updateAvailable = !string.Equals(
                    _localVersion, _remoteVersion, StringComparison.OrdinalIgnoreCase);
                _status = _updateAvailable ? "Hay una versión nueva disponible." : "Juego actualizado.";
            }
        }

        static string ExtractCommit(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            Match match = Regex.Match(text, "\\b[0-9a-fA-F]{40}\\b");
            return match.Success ? match.Value : string.Empty;
        }

        IEnumerator InstallUpdate()
        {
            _busy = true;
            _dismissed = false;

#if !UNITY_STANDALONE_WIN
            _status = "La actualización automática solo funciona en Windows.";
            _busy = false;
            yield break;
#else
            string folder = GameFolder();
            string zip = Path.Combine(folder, ZipName);

            try
            {
                if (File.Exists(zip)) File.Delete(zip);
            }
            catch (Exception exception)
            {
                _status = "Error al preparar la descarga: " + exception.Message;
                _busy = false;
                yield break;
            }

            _status = "Descargando versión nueva…";
            using (var request = new UnityWebRequest(DownloadUrl, UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(zip)
                {
                    removeFileOnAbort = true,
                };
                request.SetRequestHeader("User-Agent", "CaosUpdater");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    _progress = request.downloadProgress;
                    yield return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    _status = "Error al descargar: " + request.error;
                    _busy = false;
                    yield break;
                }
            }

            _status = "Preparando reinicio…";

            try
            {
                WriteUpdateScripts(folder, _remoteVersion);
                StartUpdater(folder);
            }
            catch (Exception exception)
            {
                _status = "Error al preparar la actualización: " + exception.Message;
                _busy = false;
                yield break;
            }

            yield return null;
            Application.Quit();
#endif
        }

#if UNITY_STANDALONE_WIN
        static void WriteUpdateScripts(string folder, string version)
        {
            string batPath = Path.Combine(folder, "Actualizar.bat");
            string psPath = Path.Combine(folder, "apply-update.ps1");

            string bat =
                "@echo off\r\n" +
                "cd /d \"%~dp0\"\r\n" +
                ":wait\r\n" +
                "tasklist /FI \"IMAGENAME eq Caos.exe\" | find /I \"Caos.exe\" >nul\r\n" +
                "if not errorlevel 1 (\r\n" +
                "  timeout /t 1 >nul\r\n" +
                "  goto wait\r\n" +
                ")\r\n" +
                "powershell -NoProfile -ExecutionPolicy Bypass -File \"%~dp0apply-update.ps1\"\r\n" +
                "start \"\" \"%~dp0Caos.exe\"\r\n";

            string powershell = string.Join("\r\n", new[]
            {
                "$ErrorActionPreference = 'Stop'",
                "$root = Split-Path -Parent $MyInvocation.MyCommand.Path",
                "$zip = Join-Path $root 'CaosUpdate.zip'",
                "$tmp = Join-Path $root '_caos_update_tmp'",
                "if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force }",
                "New-Item -ItemType Directory -Path $tmp -Force | Out-Null",
                "Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force",
                "Copy-Item -Path (Join-Path $tmp '*') -Destination $root -Recurse -Force",
                "$versionPath = Join-Path $root 'Caos_Data\\StreamingAssets\\caos-version.txt'",
                "New-Item -ItemType Directory -Path (Split-Path -Parent $versionPath) -Force | Out-Null",
                "Set-Content -LiteralPath $versionPath -Value '" + EscapePowerShell(version) + "' -NoNewline",
                "Remove-Item -LiteralPath $tmp -Recurse -Force",
                "Remove-Item -LiteralPath $zip -Force",
            });

            File.WriteAllText(batPath, bat);
            File.WriteAllText(psPath, powershell);
        }

        static string EscapePowerShell(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        static void StartUpdater(string folder)
        {
            string batPath = Path.Combine(folder, "Actualizar.bat");
            var start = new ProcessStartInfo("cmd.exe", "/c \"" + batPath + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = folder,
            };
            Process.Start(start);
        }
#endif

        static string GameFolder()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows"));
#else
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
#endif
        }

        static string Short(string value)
        {
            if (string.IsNullOrEmpty(value)) return "?";
            return value.Length > 7 ? value.Substring(0, 7) : value;
        }

        void OnGUI()
        {
            // El actualizador nunca se superpone al armazón: entrada, lobby,
            // personalización y carga son módulos limpios.
            if (CaosShell.IsBlocked) return;

            EnsureStyles();

            float buttonWidth = 190f;
            var downloadRect = new Rect(Screen.width - buttonWidth - 20f, 20f, buttonWidth, 34f);
            if (GUI.Button(downloadRect, "DESCARGAR ZIP", _buttonStyle))
                Application.OpenURL(DownloadUrl);

            if ((!_checking && !_busy && !_updateAvailable) || _dismissed)
            {
                if (!_busy && !string.IsNullOrEmpty(_status))
                    GUI.Label(new Rect(downloadRect.x - 10f, 58f, 240f, 22f), _status, _smallStyle);
                return;
            }

            float width = Mathf.Min(390f, Screen.width - 40f);
            float height = _busy ? 190f : 186f;
            var panel = new Rect(Screen.width - width - 20f, 64f, width, height);

            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Space(12f);
            GUILayout.Label(_busy ? "ACTUALIZANDO…" : "NUEVA VERSIÓN DISPONIBLE", _titleStyle);
            GUILayout.Space(6f);
            GUILayout.Label(_status, _textStyle);

            if (_busy)
            {
                GUILayout.Space(10f);
                var bar = GUILayoutUtility.GetRect(panel.width - 40f, 18f);
                GUI.Box(bar, GUIContent.none);
                var fill = new Rect(bar.x + 2f, bar.y + 2f,
                    (bar.width - 4f) * Mathf.Clamp01(_progress), bar.height - 4f);
                GUI.color = new Color(0.35f, 0.95f, 0.55f);
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Space(4f);
                GUILayout.Label(
                    $"Versión local: {Short(_localVersion)}   →   nueva: {Short(_remoteVersion)}",
                    _smallStyle);
                GUILayout.Space(12f);
                if (GUILayout.Button("ACTUALIZAR Y REINICIAR", _buttonStyle, GUILayout.Height(44f)))
                    StartCoroutine(InstallUpdate());
                if (GUILayout.Button("AHORA NO", _smallButtonStyle, GUILayout.Height(30f)))
                    _dismissed = true;
            }

            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _textStyle.normal.textColor = Color.white;

            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _smallStyle.normal.textColor = new Color(0.85f, 0.87f, 0.95f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
            };

            _smallButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        }
    }
}
