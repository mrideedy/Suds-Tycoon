using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace SudsTycoon
{
    /// <summary>
    /// The web build's fonts: Lilita One for the HUD, buttons and numbers, Nunito ExtraBold / Black for small
    /// descriptive text. SDF font assets are created once on the main thread (UI Toolkit's parallel text jobs
    /// cannot convert a plain Font themselves).
    /// </summary>
    public static class Fonts
    {
        public static FontAsset Display { get; private set; }
        public static FontAsset Body { get; private set; }
        public static FontAsset BodyHeavy { get; private set; }

        // Web CSS classes that use the body font: weight 900 -> Nunito Black, 800 -> Nunito ExtraBold.
        static readonly HashSet<string> Heavy = new HashSet<string> { "q-text", "say", "name-tag", "u-lv", "ad-line" };
        static readonly HashSet<string> Regular = new HashSet<string> { "r-sub", "note", "t-sub", "ph-sub", "bn-text", "ad-count", "ad-note", "region-sub", "empire-sub" };

        public static void Init()
        {
            Display = Make("Fonts/LilitaOne-Regular") ?? Make(null);
            Body = Make("Fonts/Nunito-ExtraBold") ?? Display;
            BodyHeavy = Make("Fonts/Nunito-Black") ?? Body;
        }

        static FontAsset Make(string path)
        {
            var font = path != null ? Resources.Load<Font>(path) : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!font) return null;
            var fa = FontAsset.CreateFontAsset(font);
            if (fa) fa.name = font.name + " SDF";
            return fa;
        }

        /// <summary>Default font for a whole tree.</summary>
        public static void ApplyRoot(VisualElement root)
        {
            if (Display) root.style.unityFontDefinition = FontDefinition.FromSDFFont(Display);
        }

        /// <summary>Called whenever classes are added through UiKit: body-text classes switch to Nunito.</summary>
        public static void Apply(VisualElement e, string cls)
        {
            if (string.IsNullOrEmpty(cls) || Body == null) return;
            foreach (var c in cls.Split(' '))
            {
                if (Heavy.Contains(c)) { e.style.unityFontDefinition = FontDefinition.FromSDFFont(BodyHeavy); return; }
                if (Regular.Contains(c)) { e.style.unityFontDefinition = FontDefinition.FromSDFFont(Body); return; }
            }
        }

        /// <summary>Back to the inherited (display) font, e.g. when a label changes role.</summary>
        public static void Reset(VisualElement e) => e.style.unityFontDefinition = StyleKeyword.Null;
    }
}
