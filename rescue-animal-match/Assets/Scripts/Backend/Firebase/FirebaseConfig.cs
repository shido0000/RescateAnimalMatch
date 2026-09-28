#if USE_FIREBASE
using System.IO;
using UnityEngine;

namespace RescueAnimalMatch.Backend.Firebase
{
    /// <summary>
    /// Carga google-services.json desde StreamingAssets. NUNCA hardcodea
    /// secretos: el archivo de configuración se inyecta en el build (CI) y
    /// nunca se sube al repositorio (.gitignore).
    /// </summary>
    public static class FirebaseConfig
    {
        private const string RelativePath = "google-services.json";

        public static bool TryGetProjectId(out string projectId)
        {
            projectId = null;
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, RelativePath);
#if UNITY_ANDROID && !UNITY_EDITOR
                // En Android StreamingAssets vive dentro del APK: leer vía UnityWebRequest
                // queda delegado a FirebaseApp.DefaultFirebaseOptionsCurrentPlatformBuilderAndroid,
                // que ya parsea google-services.json automáticamente.
                projectId = "(auto: android asset)";
                return true;
#else
                if (!File.Exists(path)) return false;
                var json = File.ReadAllText(path);
                int idx = json.IndexOf("\"project_id\"", System.StringComparison.Ordinal);
                if (idx < 0) return false;
                int q1 = json.IndexOf('"', idx + 12);
                int q2 = json.IndexOf('"', q1 + 1);
                if (q1 < 0 || q2 < 0) return false;
                projectId = json.Substring(q1 + 1, q2 - q1 - 1);
                return !string.IsNullOrEmpty(projectId);
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("FirebaseConfig: " + e.Message);
                return false;
            }
        }
    }
}
#endif
