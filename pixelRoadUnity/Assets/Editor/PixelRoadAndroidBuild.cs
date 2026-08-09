using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PixelRoad.Editor
{
    public static class PixelRoadAndroidBuild
    {
        private const string BuildPathArgument = "-pixelRoadBuildPath";

        [MenuItem("Pixel Road/Build Android Development APK")]
        public static void BuildAndroidDevelopmentApk()
        {
            string defaultPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "Build",
                "PixelRoad-development.apk"));
            BuildApk(defaultPath, BuildOptions.Development, "development");
        }

        [MenuItem("Pixel Road/Build Android Offline Review APK")]
        public static void BuildAndroidOfflineReviewApk()
        {
            string defaultPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "Build",
                "PixelRoad-offline-review.apk"));
            BuildApk(defaultPath, BuildOptions.None, "offline review");
        }

        /// <summary>
        /// Batch entry point. An optional -pixelRoadBuildPath argument selects the APK path.
        /// This intentionally creates a development artifact, not a contest submission build.
        /// </summary>
        public static void BuildAndroidDevelopmentFromCommandLine()
        {
            string outputPath = ReadArgument(BuildPathArgument);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "..",
                    "..",
                    "Build",
                    "PixelRoad-development.apk"));
            }

            BuildApk(outputPath, BuildOptions.Development, "development");
        }

        /// <summary>
        /// Builds a non-development APK. Live networking is compiled out unless the
        /// PIXELROAD_LIVE_VECTOR_MAP scripting symbol has been deliberately approved.
        /// </summary>
        public static void BuildAndroidOfflineReviewFromCommandLine()
        {
            string outputPath = ReadArgument(BuildPathArgument);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "..",
                    "..",
                    "Build",
                    "PixelRoad-offline-review.apk"));
            }

            BuildApk(outputPath, BuildOptions.None, "offline review");
        }

        private static void BuildApk(string outputPath, BuildOptions buildOptions, string buildLabel)
        {
            if (!outputPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Android development output must end with .apk.", nameof(outputPath));
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MapScene.unity" },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = buildOptions
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Pixel Road Android development build failed: " + report.summary.result);
            }

            Debug.Log(
                "[PixelRoad] Android " + buildLabel + " APK: " + outputPath
                + " (" + report.summary.totalSize + " bytes)");
        }

        private static string ReadArgument(string argumentName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }
    }
}
