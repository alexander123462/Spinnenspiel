using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>B11: Builds the Windows player from the Urwald scene into Builds/Windows64/Spinnenspiel.exe.
    /// Es MUSS ein 64-Bit-Player sein: Ein 32-Bit-Player meldet nur 3072 MB Grafikspeicher, Unity verweigert dann Direct3D 12
    /// (Geraetefilter < 3600 MB) und faellt auf Direct3D 11 zurueck, wo HDRP falsch rendert (kein Boden, Pflanzen kopfueber).</summary>
    public static class BuildWindowsTool
    {
        public static void BuildWindows()
        {
            const string exePath = "Builds/Windows64/Spinnenspiel.exe";
            const string urwaldScene = "Assets/_Project/Scenes/Urwald.unity";
            if (!File.Exists(urwaldScene))
            {
                Debug.LogError("[Build] Urwald scene missing - run UrwaldSceneBuilder.Build first.");
                return;
            }

            // Aktives Build-Ziel ausdruecklich auf 64-Bit stellen (sonst kann ein altes 32-Bit-Ziel gewinnen).
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 });

            // Unity 6: Die Windows-Architektur steht in UserBuildSettings (nicht im BuildTarget). Ausdruecklich x64 setzen und protokollieren.
            EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.StandaloneWindows64;
            try
            {
                var t = System.Type.GetType("UnityEditor.WindowsStandalone.UserBuildSettings, UnityEditor.WindowsStandalone.Extensions");
                var pi = t != null ? t.GetProperty("architecture") : null;
                if (pi != null)
                {
                    Debug.Log("[Build] Windows-Architektur vorher: " + pi.GetValue(null));
                    pi.SetValue(null, System.Enum.Parse(pi.PropertyType, "x64"));
                    Debug.Log("[Build] Windows-Architektur jetzt: " + pi.GetValue(null));
                }
                else Debug.Log("[Build] UserBuildSettings.architecture nicht gefunden (Typ " + (t != null) + ")");
            }
            catch (System.Exception e) { Debug.Log("[Build] Architektur setzen fehlgeschlagen: " + e.Message); }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { urwaldScene },
                locationPathName = exePath,
                targetGroup = BuildTargetGroup.Standalone,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Build] Ergebnis=" + report.summary.result + " Plattform=" + report.summary.platform + " Ausgabe=" + report.summary.outputPath);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Build] BuildPlayer failed: " + report.summary.result);
                return;
            }
            if (!File.Exists(exePath))
            {
                Debug.LogError("[Build] exe not found at " + exePath);
                return;
            }
            Debug.Log("[Build] Windows Player built: " + exePath);
        }
    }
}
