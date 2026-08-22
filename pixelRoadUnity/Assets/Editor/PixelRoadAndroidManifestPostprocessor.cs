using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PixelRoad.Editor
{
    /// <summary>
    /// 모든 빌드는 기본적으로 지도를 쓰므로 INTERNET 권한을 유지한다.
    /// PIXELROAD_OFFLINE_REVIEW 심볼을 켠 오프라인 심사 빌드에서만 권한을 빼고,
    /// Unity가 다른 패키지에서 유추해 넣은 중복 선언도 함께 정리한다.
    /// </summary>
    public sealed class PixelRoadAndroidManifestPostprocessor :
        IPreprocessBuildWithReport,
        IPostGenerateGradleAndroidProject,
        IPostprocessBuildWithReport
    {
        private const string OfflineReviewSymbol = "PIXELROAD_OFFLINE_REVIEW";
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private const string InternetPermission = "android.permission.INTERNET";
        public int callbackOrder
        {
            get { return 1000; }
        }

        /// <summary>빌드 시작 시점에 할 일은 없다. 인터페이스를 맞추기 위해 비워 둔다.</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
        }

        /// <summary>
        /// 생성된 Gradle 프로젝트의 매니페스트에서 INTERNET 권한을 빌드 종류에 맞게 넣거나 뺀다. 중복 선언도 함께 정리한다.
        /// </summary>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // 지도가 기본 기능이므로 INTERNET 은 기본 포함이다. 오프라인 심사 빌드에서만 뺀다.
            bool includeInternet = !HasAndroidDefine(OfflineReviewSymbol);

            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException("Generated Android manifest was not found.", manifestPath);
            }

            XmlDocument document = new XmlDocument
            {
                PreserveWhitespace = true
            };
            document.Load(manifestPath);
            XmlElement root = document.DocumentElement;
            if (root == null)
            {
                throw new InvalidOperationException("Generated Android manifest has no root element.");
            }

            bool changed = false;
            bool foundInternet = false;
            XmlNodeList permissions = root.SelectNodes("uses-permission");
            if (permissions != null)
            {
                for (int index = permissions.Count - 1; index >= 0; index--)
                {
                    XmlNode permission = permissions[index];
                    string permissionName = permission.Attributes?["name", AndroidNamespace]?.Value;
                    if (!string.Equals(permissionName, InternetPermission, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (includeInternet && !foundInternet)
                    {
                        foundInternet = true;
                    }
                    else
                    {
                        root.RemoveChild(permission);
                        changed = true;
                    }
                }
            }

            if (includeInternet && !foundInternet)
            {
                XmlElement permission = document.CreateElement("uses-permission");
                XmlAttribute name = document.CreateAttribute("android", "name", AndroidNamespace);
                name.Value = InternetPermission;
                permission.Attributes.Append(name);
                XmlNode application = root.SelectSingleNode("application");
                if (application != null)
                {
                    root.InsertBefore(permission, application);
                }
                else
                {
                    root.AppendChild(permission);
                }

                changed = true;
            }

            if (changed)
            {
                document.Save(manifestPath);
                Debug.Log(includeInternet
                    ? "[PixelRoad] Added INTERNET permission to live-map Android manifest."
                    : "[PixelRoad] Removed INTERNET permission from offline-review Android manifest.");
            }
        }

        /// <summary>빌드 종료 시점에 되돌릴 상태가 없다. 인터페이스를 맞추기 위해 비워 둔다.</summary>
        public void OnPostprocessBuild(BuildReport report)
        {
        }

        /// <summary>안드로이드 플랫폼에 해당 스크립팅 심볼이 켜져 있는지 확인한다.</summary>
        private static bool HasAndroidDefine(string symbol)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            string[] values = defines.Split(';');
            for (int index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index].Trim(), symbol, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
