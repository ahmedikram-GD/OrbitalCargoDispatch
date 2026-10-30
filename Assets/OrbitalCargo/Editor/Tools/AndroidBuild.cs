using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OrbitalCargo.Shift.EditorTools
{
    /// <summary>
    /// One-click pitch build: IL2CPP / ARM64 / release APK of the enabled build scenes to
    /// <c>Builds/OCD_pitch.apk</c>, switching the active target to Android first if needed. The result
    /// (status, size, duration, errors) is also written to <c>Builds/last_build.txt</c> so a build kicked
    /// off without watching the console can still be checked. Batchmode:
    /// <c>-executeMethod OrbitalCargo.Shift.EditorTools.AndroidBuild.BuildApk</c>.
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputFolder = "Builds";
        public const string ApkPath = OutputFolder + "/OCD_pitch.apk";
        private const string ReportPath = OutputFolder + "/last_build.txt";

        [MenuItem("Tools/OrbitalCargo/Build Android APK")]
        public static void BuildApk() => Build(BuildOptions.None);

        [MenuItem("Tools/OrbitalCargo/Build Android APK and Run")]
        public static void BuildAndRun() => Build(BuildOptions.AutoRunPlayer);

        private static void Build(BuildOptions options)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[AndroidBuild] Could not switch the active build target to Android — is the Android module installed?");
                return;
            }

            // The pitch settings (PLAN.md 8.1). Re-asserted every build so a stray Editor change can't ship.
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.buildAppBundle = false;

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[AndroidBuild] No enabled scenes in Build Settings.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options,
            });

            var summary = report.summary;
            string errors = string.Join("\n", report.steps
                .SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Select(m => m.content));
            string text = $"result={summary.result}\nsizeMB={summary.totalSize / (1024f * 1024f):0.0}\n" +
                          $"seconds={summary.totalTime.TotalSeconds:0}\nerrors={summary.totalErrors}\n" +
                          $"warnings={summary.totalWarnings}\npath={ApkPath}\n{errors}";
            File.WriteAllText(ReportPath, text);

            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[AndroidBuild] {ApkPath} built ({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0} s).");
            else
                Debug.LogError($"[AndroidBuild] Build {summary.result} with {summary.totalErrors} errors — see {ReportPath}.");
        }
    }
}
