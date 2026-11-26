// CLI build entry point for the OpenCode unity-game-dev skill.
// Unity.exe -batchmode -quit -projectPath <project> -executeMethod AIUnityTools.BuildRunner.Build
//   -buildTarget Win64|WebGL|Android|... -buildOutput Builds/Win64 [-buildScenes Assets/Scenes/Main.unity] [-development]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AIUnityTools
{
    public static class BuildRunner
    {
        public static void Build()
        {
            int exitCode = 1;
            try
            {
                string targetName = Arg("-buildTarget", "Win64");
                string output = Arg("-buildOutput", null);
                string scenesArg = Arg("-buildScenes", null);
                bool development = HasFlag("-development");

                BuildTarget target = ParseTarget(targetName);
                if (string.IsNullOrEmpty(output)) output = "Builds/" + target;
                if (!Path.IsPathRooted(output)) output = Path.Combine(Directory.GetCurrentDirectory(), output);
                output = NormalizeOutput(output, target);

                string[] scenes = string.IsNullOrEmpty(scenesArg)
                    ? EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray()
                    : scenesArg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                if (scenes.Length == 0)
                    throw new Exception("No enabled scenes in Build Settings and no -buildScenes provided.");

                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = development ? BuildOptions.Development : BuildOptions.None;
                Debug.Log("AI_BUILD_START target=" + target + " output=" + output + " scenes=" + string.Join(";", scenes));
                var report = BuildPipeline.BuildPlayer(scenes, output, target, options);
                var summary = report.summary;

                var errors = new List<string>();
                foreach (var step in report.steps)
                    foreach (var message in step.messages)
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                            if (errors.Count < 30) errors.Add(message.content);

                Debug.Log("AI_BUILD_RESULT " + summary.result + " size=" + summary.totalSize + " errors=" + summary.totalErrors + " warnings=" + summary.totalWarnings + " output=" + output);
                foreach (var error in errors) Debug.LogError("AI_BUILD_ERROR " + error);
                exitCode = summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("AI_BUILD_FAILED " + e);
                exitCode = 2;
            }
            EditorApplication.Exit(exitCode);
        }

        static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return fallback;
        }

        static bool HasFlag(string name)
        {
            return Environment.GetCommandLineArgs().Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }

        static BuildTarget ParseTarget(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "win64":
                case "windows":
                case "win":
                case "standalonewindows64": return BuildTarget.StandaloneWindows64;
                case "win32":
                case "standalonewindows": return BuildTarget.StandaloneWindows;
                case "linux":
                case "linux64":
                case "standalonelinux64": return BuildTarget.StandaloneLinux64;
                case "mac":
                case "osx":
                case "standaloneosx": return BuildTarget.StandaloneOSX;
                case "webgl": return BuildTarget.WebGL;
                case "android": return BuildTarget.Android;
                case "ios": return BuildTarget.iOS;
                default: throw new Exception("Unknown build target: " + value);
            }
        }

        static string NormalizeOutput(string output, BuildTarget target)
        {
            bool standalone = target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows
                || target == BuildTarget.StandaloneLinux64 || target == BuildTarget.StandaloneOSX;
            if (!standalone) return output;
            string extension = Path.GetExtension(output);
            if (extension == ".exe" || extension == ".x86_64" || extension == ".app") return output;
            string product = PlayerSettings.productName;
            if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
                return Path.Combine(output, product + ".exe");
            if (target == BuildTarget.StandaloneLinux64)
                return Path.Combine(output, product + ".x86_64");
            return Path.Combine(output, product + ".app");
        }
    }
}
