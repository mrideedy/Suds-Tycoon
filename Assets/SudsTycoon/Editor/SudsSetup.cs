using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SudsTycoon.EditorTools
{
    /// <summary>Editor menu for setting up and testing the game (Tools > Suds Tycoon).</summary>
    public static class SudsSetup
    {
        const string ScenePath = "Assets/SudsTycoon/Scenes/SudsTycoon.unity";

        [MenuItem("Tools/Suds Tycoon/Create Game Scene", priority = 0)]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<CameraRig>();
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            new GameObject("Suds Tycoon").AddComponent<SudsGame>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Suds Tycoon] Scene created at " + ScenePath + " and set as the first build scene. Press Play.");
        }

        [MenuItem("Tools/Suds Tycoon/Open Game Scene", priority = 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { CreateScene(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Suds Tycoon/Reimport Art (icons, textures, models)", priority = 20)]
        public static void ReimportArt()
        {
            foreach (var dir in new[] { "Assets/SudsTycoon/Resources/Icons", "Assets/SudsTycoon/Resources/Textures", "Assets/SudsTycoon/Resources/Models" })
                if (AssetDatabase.IsValidFolder(dir)) AssetDatabase.ImportAsset(dir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("[Suds Tycoon] Art reimported.");
        }

        [MenuItem("Tools/Suds Tycoon/Delete Save File", priority = 40)]
        public static void DeleteSave()
        {
            var path = Path.Combine(Application.persistentDataPath, "suds_tycoon_save.json");
            if (File.Exists(path) && EditorUtility.DisplayDialog("Delete save?", "Delete " + path + "?\nThe next Play starts a fresh game.", "Delete", "Cancel"))
            {
                File.Delete(path);
                Debug.Log("[Suds Tycoon] Save deleted.");
            }
            else if (!File.Exists(path)) Debug.Log("[Suds Tycoon] No save file yet.");
        }

        [MenuItem("Tools/Suds Tycoon/Show Save Folder", priority = 41)]
        public static void ShowSave() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        [MenuItem("Tools/Suds Tycoon/Use Test Ads (mock)", priority = 60)]
        public static void MockAds() => SetDefine("SUDS_ADMOB", false);
        [MenuItem("Tools/Suds Tycoon/Use AdMob (needs the Google Mobile Ads plugin)", priority = 61)]
        public static void AdMob()
        {
            SetDefine("SUDS_ADMOB", true);
            Debug.Log("[Suds Tycoon] SUDS_ADMOB defined. Also set Cfg.Ads.Provider = \"admob\" in Scripts/Core/Cfg.cs and add your AdMob app ID in Assets > Google Mobile Ads > Settings.");
        }

        static void SetDefine(string define, bool on)
        {
            foreach (var target in new[] { UnityEditor.Build.NamedBuildTarget.Android, UnityEditor.Build.NamedBuildTarget.iOS, UnityEditor.Build.NamedBuildTarget.Standalone })
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out var defs);
                var list = defs.ToList();
                if (on && !list.Contains(define)) list.Add(define);
                if (!on) list.Remove(define);
                PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
            }
        }
    }

    /// <summary>Import settings for the art exported from the web build.</summary>
    public class SudsTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/SudsTycoon/Resources/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.Contains("/Icons/"))
            {
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 256;
                ti.filterMode = FilterMode.Bilinear;
            }
            else if (assetPath.Contains("/Textures/"))
            {
                string file = Path.GetFileNameWithoutExtension(assetPath);
                bool tiles = file.StartsWith("lot_") || file == "water_streaks" || file == "hazard" || file == "grate";
                ti.mipmapEnabled = true;
                ti.wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                ti.anisoLevel = tiles ? 4 : 1;
                ti.maxTextureSize = 512;
            }
        }
    }
}
