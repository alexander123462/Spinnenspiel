using System.IO;
using UnityEditor;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>B11: Builds the Windows player from the Urwald scene into Builds/Windows/Spinnenspiel.exe.</summary>
    public static class BuildWindowsTool
    {
        public static void BuildWindows()
    {
        const string exePath = "Builds/Windows/Spinnenspiel.exe";
        const string urwaldScene = "Assets/_Project/Scenes/Urwald.unity";
        if (!System.IO.File.Exists(urwaldScene))
        {
            Debug.LogError("[Build] Urwald scene missing – run UrwaldSceneBuilder.Build first.");
            return;
        }

        bool ok = BuildPipeline.BuildPlayer(new string[] { urwaldScene }, exePath, BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (!ok)
        {
            Debug.LogError("[Build] BuildPlayer failed.");
            return;
        }
        if (!System.IO.File.Exists(exePath))
        {
            Debug.LogError("[Build] exe not found at " + exePath);
            return;
        }
        Debug.Log("[Build] Windows Player built: " + exePath);
        }
        }
        }
