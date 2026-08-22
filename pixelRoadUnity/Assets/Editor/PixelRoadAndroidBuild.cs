using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
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

        /// <summary>
        /// 오프라인 심사 빌드에만 켜는 스크립팅 심볼. 이 심볼이 있으면 라이브 타일 요청 코드가 컴파일에서 빠지고
        /// 안드로이드 매니페스트에서 INTERNET 권한도 제거된다. 평소 빌드에는 붙이지 않는다.
        /// </summary>
        private const string OfflineReviewSymbol = "PIXELROAD_OFFLINE_REVIEW";

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
            BuildApk(defaultPath, BuildOptions.Development, "development", false);
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
            BuildApk(defaultPath, BuildOptions.None, "offline review", true);
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

            BuildApk(outputPath, BuildOptions.Development, "development", false);
        }

        /// <summary>
        /// Builds a non-development APK with the offline-review symbol on, so the live
        /// tile requester is compiled out and the manifest ships without INTERNET.
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

            BuildApk(outputPath, BuildOptions.None, "offline review", true);
        }

        /// <summary>
        /// 출력 경로를 검증하고 폴더를 만든 뒤 실제 빌드를 돌린다. 실패하면 예외를 던져 CI가 알아채게 한다.
        /// <paramref name="offlineReview"/>가 true면 빌드하는 동안만 오프라인 심사 심볼을 켠다.
        /// </summary>
        private static void BuildApk(
            string outputPath,
            BuildOptions buildOptions,
            string buildLabel,
            bool offlineReview)
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
            // 심볼은 이 빌드 동안만 바꾸고 끝나면 원래대로 돌려놓는다. 프로젝트 설정에 흔적을 남기지 않기 위해서다.
            string previousSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            BuildReport report;
            try
            {
                PlayerSettings.SetScriptingDefineSymbols(
                    NamedBuildTarget.Android,
                    ApplySymbol(previousSymbols, OfflineReviewSymbol, offlineReview));
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, previousSymbols);
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Pixel Road Android " + buildLabel + " build failed: " + report.summary.result);
            }

            Debug.Log(
                "[PixelRoad] Android " + buildLabel + " APK: " + outputPath
                + " (" + report.summary.totalSize + " bytes)");
        }

        /// <summary>세미콜론으로 구분된 심볼 목록에 하나를 넣거나 뺀다. 이미 원하는 상태면 그대로 돌려준다.</summary>
        private static string ApplySymbol(string symbols, string symbol, bool enabled)
        {
            string[] values = (symbols ?? string.Empty).Split(';');
            System.Collections.Generic.List<string> kept =
                new System.Collections.Generic.List<string>(values.Length + 1);
            for (int index = 0; index < values.Length; index++)
            {
                string value = values[index].Trim();
                if (value.Length == 0 || string.Equals(value, symbol, StringComparison.Ordinal))
                {
                    continue;
                }

                kept.Add(value);
            }

            if (enabled)
            {
                kept.Add(symbol);
            }

            return string.Join(";", kept.ToArray());
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
