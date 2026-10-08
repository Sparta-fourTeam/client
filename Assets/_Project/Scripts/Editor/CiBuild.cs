using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>CI 빌드 진입점. game-ci 기본 빌드 스크립트는 빌드 프로파일로 빌드할 때 Android 서명·버전 코드 설정을
    /// 건너뛰므로(프로파일 분기에서 AndroidSettings.Apply를 부르지 않는다), 같은 인자를 읽어 직접 적용한 뒤 프로파일로 빌드한다.
    /// 앱 번들/APK 여부, 개발 빌드 여부는 프로파일이 정한다</summary>
    public static class CiBuild
    {
        public static void Build()
        {
            var args = CiBuildArgs.Parse(Environment.GetCommandLineArgs());

            if (!args.TryGetValue("customBuildProfile", out var profilePath) || string.IsNullOrEmpty(profilePath) ||
                !args.TryGetValue("customBuildPath", out var outputPath) || string.IsNullOrEmpty(outputPath))
            {
                Fail("-customBuildProfile 과 -customBuildPath 가 필요하다");
                return;
            }

            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null)
            {
                Fail($"빌드 프로파일을 찾을 수 없다: {profilePath}");
                return;
            }

            ApplyVersion(args);
            ApplyAndroidSigning(args);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = outputPath,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"빌드 {summary.result}: {profile.name}, {summary.totalSize} bytes, {summary.totalErrors} errors, {summary.totalTime}");
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 2);
        }

        private static void ApplyVersion(IReadOnlyDictionary<string, string> args)
        {
            if (args.TryGetValue("buildVersion", out var version) && !string.IsNullOrEmpty(version))
            {
                PlayerSettings.bundleVersion = version;
            }

            if (args.TryGetValue("androidVersionCode", out var code) && int.TryParse(code, out int versionCode) && versionCode > 0)
            {
                PlayerSettings.Android.bundleVersionCode = versionCode;
            }
        }

        private static void ApplyAndroidSigning(IReadOnlyDictionary<string, string> args)
        {
            if (!args.TryGetValue("androidKeystoreName", out var keystore) || string.IsNullOrEmpty(keystore))
            {
                return;   // 키스토어를 주지 않으면 프로젝트 설정 그대로(디버그 서명 등) 빌드한다
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = args.GetValueOrDefault("androidKeystorePass", "");
            PlayerSettings.Android.keyaliasName = args.GetValueOrDefault("androidKeyaliasName", "");
            PlayerSettings.Android.keyaliasPass = args.GetValueOrDefault("androidKeyaliasPass", "");
        }

        private static void Fail(string message)
        {
            Debug.LogError(message);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>유니티 명령줄(-키 값)을 읽는다. 비밀번호가 '-'로 시작해도 값으로 읽도록, 값을 받는 키는 다음 토큰을 그대로 값으로 쓴다</summary>
    public static class CiBuildArgs
    {
        private static readonly HashSet<string> ValueKeys = new()
        {
            "customBuildProfile", "customBuildPath", "buildVersion", "androidVersionCode",
            "androidKeystoreName", "androidKeystorePass", "androidKeyaliasName", "androidKeyaliasPass",
        };

        public static Dictionary<string, string> Parse(IReadOnlyList<string> args)
        {
            var result = new Dictionary<string, string>();
            for (int i = 0; i < args.Count; i++)
            {
                var token = args[i];
                if (token.Length < 2 || token[0] != '-')
                {
                    continue;
                }

                var key = token.TrimStart('-');
                if (ValueKeys.Contains(key))
                {
                    result[key] = i + 1 < args.Count ? args[++i] : "";
                }
                else
                {
                    result[key] = "";
                }
            }

            return result;
        }
    }
}
