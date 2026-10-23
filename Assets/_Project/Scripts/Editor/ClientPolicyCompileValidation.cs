using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>서명·패키징·스토어 호출 없이 Android/iOS 플레이어 C# 어셈블리만 검증하는 로컬 도구.</summary>
    public static class ClientPolicyCompileValidation
    {
        public static void Compile()
        {
            try
            {
                if (!Application.isBatchMode) { throw new InvalidOperationException("Run in batch mode."); }
                string value = Environment.GetEnvironmentVariable("NOVA_POLICY_VALIDATION_TARGET");
                if (!Enum.TryParse(value, out BuildTarget target)
                    || (target != BuildTarget.Android && target != BuildTarget.iOS))
                { throw new ArgumentException("Set NOVA_POLICY_VALIDATION_TARGET to Android or iOS."); }
                string output = Environment.GetEnvironmentVariable("NOVA_POLICY_VALIDATION_OUTPUT");
                if (string.IsNullOrWhiteSpace(output)) { throw new ArgumentException("Set NOVA_POLICY_VALIDATION_OUTPUT."); }
                var group = BuildPipeline.GetBuildTargetGroup(target);
                if (!BuildPipeline.IsBuildTargetSupported(group, target))
                { throw new InvalidOperationException("Required player module is not installed."); }
                Directory.CreateDirectory(output);
                var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
                {
                    group = group,
                    target = target,
                    options = ScriptCompilationOptions.None,
                }, output);
                if (result.assemblies == null || !result.assemblies.Any())
                { throw new InvalidOperationException("Player script compilation produced no assemblies."); }
                File.WriteAllText(Path.Combine(output, "compile-result.txt"),
                    "PASS: managed player compilation only\nTarget: " + target + "\n" + string.Join("\n", result.assemblies));
                Debug.Log("Client policy player script compilation PASS: " + target);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
