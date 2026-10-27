using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SudsTycoon.EditorTools
{
    /// <summary>
    /// Tools > Suds Tycoon > Google Play Build: one window that applies the release settings, creates your upload key
    /// the first time (you choose the password), and builds the signed .aab that you upload to the Play Console.
    /// </summary>
    public class PlayStoreBuild : EditorWindow
    {
        /// <summary>Your app's permanent id on Google Play. Change it before the first upload if you want another one.</summary>
        public const string PackageName = "com.sudstycoon.game";
        const string Alias = "upload";
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        /// <summary>Kept outside Assets so it is never packed into the game. Back it up: every update must be signed with it.</summary>
        public static string KeystorePath => Path.Combine(Root, "Keystore", "suds-upload.keystore");
        static string OutDir => Path.Combine(Root, "Builds", "Android");

        string pass = "", pass2 = "", owner = "";
        string status;

        [MenuItem("Tools/Suds Tycoon/Google Play Build...", priority = 80)]
        static void Open()
        {
            var w = GetWindow<PlayStoreBuild>(true, "Google Play Build", true);
            w.minSize = new Vector2(480, 430);
        }

        /// <summary>Release settings for Google Play. Safe to run again.</summary>
        [MenuItem("Tools/Suds Tycoon/Apply Release Settings", priority = 81)]
        public static void ApplySettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.productName = "Suds Tycoon";
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion.StartsWith("0.")) PlayerSettings.bundleVersion = "1.0.0";
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;   // Google Play needs 64-bit
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel25) PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;   // the newest installed SDK
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Minimal);
            SetDebugSymbols("SymbolTable");   // readable crash reports in Play Console, packed inside the .aab
            PlayerSettings.SplashScreen.show = false;   // the game has its own loading screen
            // landscape, either way up
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            AppIcon.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Suds Tycoon] Release settings applied: {PackageName} {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), IL2CPP ARM64, AAB.");
        }

        /// <summary>Android debug symbol level, set by name so this compiles without the Android module's editor assembly.</summary>
        static void SetDebugSymbols(string level)
        {
            var t = System.Type.GetType("UnityEditor.Android.UserBuildSettings+DebugSymbols, UnityEditor.Android.Extensions");
            var p = t?.GetProperty("level", BindingFlags.Public | BindingFlags.Static);
            if (p != null) p.SetValue(null, System.Enum.Parse(p.PropertyType, level));
        }

        void OnGUI()
        {
            var big = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12 };
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("1. Release settings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Package: {PackageName}   (permanent once uploaded)", big);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Version {PlayerSettings.bundleVersion}, version code {PlayerSettings.Android.bundleVersionCode}", big);
            if (GUILayout.Button("Version code +1", GUILayout.Width(120))) PlayerSettings.Android.bundleVersionCode++;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Every upload to Google Play needs a higher version code. A successful build raises it for you.", EditorStyles.miniLabel);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("2. Upload key", EditorStyles.boldLabel);
            bool hasKey = File.Exists(KeystorePath);
            if (!hasKey)
            {
                EditorGUILayout.LabelField("Google Play needs every build signed with your upload key. Create it once, then keep the file and the password safe: every update must be signed with the same key.", big);
                owner = EditorGUILayout.TextField("Your name or studio", owner);
                pass = EditorGUILayout.PasswordField("Choose a password", pass);
                pass2 = EditorGUILayout.PasswordField("Repeat it", pass2);
                using (new EditorGUI.DisabledScope(pass.Length < 6 || pass != pass2 || owner.Trim().Length == 0))
                    if (GUILayout.Button("Create upload key", GUILayout.Height(26)))
                        status = CreateKeystore(pass, owner.Trim(), out var err) ? "Upload key created. Back up the Keystore folder now." : "Could not create the key: " + err;
                if (pass.Length > 0 && pass.Length < 6) EditorGUILayout.HelpBox("Use at least 6 characters.", MessageType.Info);
                else if (pass2.Length > 0 && pass != pass2) EditorGUILayout.HelpBox("The two passwords are different.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Key: Keystore/suds-upload.keystore (keep a backup copy somewhere safe)", big);
                pass = EditorGUILayout.PasswordField("Key password", pass);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("3. Build", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!hasKey || pass.Length < 6))
                if (GUILayout.Button("Build .aab for Google Play", GUILayout.Height(34)))
                {
                    var report = BuildAab(pass);
                    status = report != null && report.summary.result == BuildResult.Succeeded
                        ? $"Done: {report.summary.outputPath} ({report.summary.totalSize / (1024f * 1024f):0.0} MB). Upload it in Play Console > Test and release."
                        : "Build failed. See the Console for the first red error.";
                    if (report != null && report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(report.summary.outputPath);
                }
            EditorGUILayout.LabelField("The first build takes several minutes (IL2CPP compiles the game to native code).", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(status)) { EditorGUILayout.Space(6); EditorGUILayout.HelpBox(status, MessageType.None); }
        }

        /// <summary>Creates the upload keystore with the JDK that ships with Unity's Android module.</summary>
        static bool CreateKeystore(string password, string ownerName, out string error)
        {
            error = null;
            string keytool = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", Application.platform == RuntimePlatform.WindowsEditor ? "keytool.exe" : "keytool");
            if (!File.Exists(keytool)) { error = "keytool not found. Install Android Build Support for this Unity version in Unity Hub."; return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(KeystorePath));
            string dname = "CN=" + ownerName.Replace(",", " ").Replace("\"", "") + ", O=Suds Tycoon";
            var psi = new ProcessStartInfo(keytool,
                $"-genkeypair -keystore \"{KeystorePath}\" -storetype PKCS12 -alias {Alias} -keyalg RSA -keysize 2048 -validity 10000 " +
                $"-storepass \"{password}\" -keypass \"{password}\" -dname \"{dname}\"")
            { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0 || !File.Exists(KeystorePath)) { error = output.Trim(); return false; }
            }
            Debug.Log("[Suds Tycoon] Upload key created at " + KeystorePath + ". Back up this file and remember its password.");
            return true;
        }

        /// <summary>Signed release .aab in Builds/Android. Raises the version code after a successful build.</summary>
        public static BuildReport BuildAab(string keystorePass)
        {
            ApplySettings();
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = KeystorePath;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = Alias;
            PlayerSettings.Android.keyaliasPass = keystorePass;
            var report = Build(Path.Combine(OutDir, $"SudsTycoon-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.aab"));
            if (report.summary.result == BuildResult.Succeeded) { PlayerSettings.Android.bundleVersionCode++; AssetDatabase.SaveAssets(); }
            return report;
        }

        /// <summary>Checks that the game builds for Android, signed with the debug key (not uploadable). Also usable from the command line:
        /// Unity -batchmode -quit -projectPath . -executeMethod SudsTycoon.EditorTools.PlayStoreBuild.BuildTest</summary>
        public static void BuildTest()
        {
            ApplySettings();
            PlayerSettings.Android.useCustomKeystore = false;
            var report = Build(Path.Combine(OutDir, "SudsTycoon-test.aab"));
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Build(string file)
        {
            Directory.CreateDirectory(OutDir);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = file, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[Suds Tycoon] Android build {s.result}: {file} ({s.totalSize / (1024f * 1024f):0.0} MB, {s.totalTime.TotalMinutes:0.0} min, {s.totalErrors} errors)");
            return report;
        }
    }
}
