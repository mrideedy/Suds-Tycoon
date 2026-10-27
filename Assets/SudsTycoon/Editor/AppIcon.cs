using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SudsTycoon.EditorTools
{
    /// <summary>
    /// The app's launcher icons, from Assets/SudsTycoon/AppIcon: icon.png (square, also the Play Store 512 px icon),
    /// icon_round.png, and the Android 8+ adaptive layers icon_background.png / icon_foreground.png.
    /// </summary>
    public static class AppIcon
    {
        public const string Dir = "Assets/SudsTycoon/AppIcon";

        public static void Apply()
        {
            var icon = Load("icon");
            if (!icon) { Debug.LogWarning("[Suds Tycoon] No app icon in " + Dir + " yet: the build uses Unity's default icon."); return; }
            var round = Load("icon_round") ?? icon;
            var bg = Load("icon_background");
            var fg = Load("icon_foreground");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            var android = NamedBuildTarget.Android;
            // icon kinds by name, so this compiles without the Android module's editor assembly
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(android))
            {
                string k = kind.ToString();
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                if (icons.Length == 0) continue;
                if (k.Contains("Adaptive")) { if (!bg || !fg) continue; foreach (var i in icons) i.SetTextures(bg, fg); }
                else if (k.Contains("Round")) foreach (var i in icons) i.SetTextures(round);
                else if (k.Contains("Legacy")) foreach (var i in icons) i.SetTextures(icon);
                else continue;
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
        }

        static Texture2D Load(string name)
        {
            string path = $"{Dir}/{name}.png";
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && (ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled || ti.npotScale != TextureImporterNPOTScale.None))
            {
                ti.textureType = TextureImporterType.Default;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.alphaIsTransparency = true;
                ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
