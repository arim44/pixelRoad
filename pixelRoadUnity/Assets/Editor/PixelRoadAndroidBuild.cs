using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PixelRoad.Editor
{
    /// <summary>
    /// 안드로이드 APK 빌드 진입점 모음. 메뉴와 CI 배치 모드에서 같은 설정으로 빌드하도록 경로와 옵션을 한곳에 모았다.
    /// </summary>
    public static class PixelRoadAndroidBuild
    {
        private const string BuildPathArgument = "-pixelRoadBuildPath";

        /// <summary>메뉴에서 개발용 APK를 기본 경로에 빌드한다.</summary>
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

        /// <summary>메뉴에서 오프라인 심사용 APK를 기본 경로에 빌드한다.</summary>
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

        /// <summary>출력 경로를 검증하고 폴더를 만든 뒤 실제 빌드를 돌린다. 실패하면 예외를 던져 CI가 알아채게 한다.</summary>
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
                // 씬 목록은 Build Settings(Loading → MapScene)를 그대로 따른다.
                // 여기에 하드코딩하면 로딩 씬이 빠져 첫 화면이 달라진다.
                scenes = CollectEnabledScenes(),
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

        /// <summary>Build Settings에 등록된 활성 씬 경로를 순서대로 모은다. 하나도 없으면 예외를 던진다.</summary>
        private static string[] CollectEnabledScenes()
        {
            EditorBuildSettingsScene[] registered = EditorBuildSettings.scenes;
            System.Collections.Generic.List<string> paths =
                new System.Collections.Generic.List<string>(registered.Length);
            for (int index = 0; index < registered.Length; index++)
            {
                if (registered[index].enabled)
                {
                    paths.Add(registered[index].path);
                }
            }

            if (paths.Count == 0)
            {
                throw new InvalidOperationException(
                    "Build Settings에 활성 씬이 없습니다. Tools > Pixel Road > Register Build Scenes 를 먼저 실행하세요.");
            }

            return paths.ToArray();
        }

        /// <summary>커맨드라인에서 지정한 인자 값을 찾는다. 없으면 null을 돌려준다.</summary>
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
