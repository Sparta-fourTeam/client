using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>기존 debug key로 별도 패키지의 오프라인 QA APK만 만든다. 기기 설치/실행/게시는 하지 않는다.</summary>
    public static class ClientPolicyQaApkBuild
    {
        public static void Build()
        {
            int exit = 1;
            var target = NamedBuildTarget.Android;
            string originalId = PlayerSettings.GetApplicationIdentifier(target);
            string originalName = PlayerSettings.productName, originalVersion = PlayerSettings.bundleVersion;
            int originalCode = PlayerSettings.Android.bundleVersionCode;
            var originalSdk = PlayerSettings.Android.targetSdkVersion;
            var originalArchitecture = PlayerSettings.Android.targetArchitectures;
            var originalHttp = PlayerSettings.insecureHttpOption;
            bool local = Environment.GetEnvironmentVariable("NOVA_POLICY_QA_LOCAL") == "1";
            bool originalCustom = PlayerSettings.Android.useCustomKeystore;
            string originalKeyName = PlayerSettings.Android.keystoreName, originalAlias = PlayerSettings.Android.keyaliasName;
            bool originalBundle = EditorUserBuildSettings.buildAppBundle, originalExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                if (!Application.isBatchMode) { throw new InvalidOperationException("Batch mode only."); }
                string output = Environment.GetEnvironmentVariable("NOVA_POLICY_QA_APK");
                if (string.IsNullOrWhiteSpace(output) || !output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                { throw new ArgumentException("Set NOVA_POLICY_QA_APK to the test APK path."); }
                string key = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android", "debug.keystore");
                if (!File.Exists(key)) { throw new InvalidOperationException("Existing Android debug keystore required; never generate a new key."); }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                PlayerSettings.SetApplicationIdentifier(target, "com.teamdoodle.doodledefense.policyqa");
                PlayerSettings.productName = "Doodle Policy QA";
                PlayerSettings.bundleVersion = local ? "1.0-policyqa4-local" : "1.0-policyqa2";
                PlayerSettings.Android.bundleVersionCode = local ? 4 : 2;
                if (local) { PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly; }
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = key;
                // Standard public Android debug signing defaults. Not production credentials.
                PlayerSettings.Android.keystorePass = "android";
                PlayerSettings.Android.keyaliasName = "androiddebugkey";
                PlayerSettings.Android.keyaliasPass = "android";
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0 || !scenes[0].EndsWith("/Boot.unity", StringComparison.Ordinal))
                { throw new InvalidOperationException("Boot must be the first enabled scene."); }
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = BuildTarget.Android,
                    locationPathName = output,
                    options = local ? BuildOptions.Development | BuildOptions.CleanBuildCache : BuildOptions.Development,
                    extraScriptingDefines = local ? new[] { "NOVA_POLICY_QA", "NOVA_POLICY_LOCAL" } : new[] { "NOVA_POLICY_QA" },
                });
                var result = report.summary;
                Debug.Log($"Policy QA APK {result.result}; errors={result.totalErrors}; bytes={result.totalSize}; time={result.totalTime}");
                exit = result.result == BuildResult.Succeeded ? 0 : 2;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(target, originalId);
                PlayerSettings.productName = originalName; PlayerSettings.bundleVersion = originalVersion;
                PlayerSettings.Android.bundleVersionCode = originalCode;
                PlayerSettings.Android.targetSdkVersion = originalSdk;
                PlayerSettings.Android.targetArchitectures = originalArchitecture;
                PlayerSettings.insecureHttpOption = originalHttp;
                PlayerSettings.Android.useCustomKeystore = originalCustom;
                PlayerSettings.Android.keystoreName = originalKeyName;
                PlayerSettings.Android.keyaliasName = originalAlias;
                PlayerSettings.Android.keystorePass = ""; PlayerSettings.Android.keyaliasPass = "";
                EditorUserBuildSettings.buildAppBundle = originalBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = originalExport;
                AssetDatabase.SaveAssets();
            }
            EditorApplication.Exit(exit);
        }
    }
}
