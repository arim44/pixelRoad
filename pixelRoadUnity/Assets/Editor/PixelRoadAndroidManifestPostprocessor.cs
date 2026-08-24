using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PixelRoad.Editor
{
    /// <summary>
    /// 모든 빌드는 지도를 쓰므로 INTERNET 권한을 항상 유지한다.
    /// Unity가 다른 패키지에서 유추해 넣은 중복 선언은 정리한다.
    /// </summary>
    public sealed class PixelRoadAndroidManifestPostprocessor :
        IPreprocessBuildWithReport,
        IPostGenerateGradleAndroidProject,
        IPostprocessBuildWithReport
    {
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
        /// 생성된 Gradle 프로젝트의 매니페스트에 INTERNET 권한이 정확히 하나 있도록 맞춘다.
        /// </summary>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
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

                    if (foundInternet)
                    {
                        // 중복 선언은 첫 번째 하나만 남기고 지운다.
                        root.RemoveChild(permission);
                        changed = true;
                        continue;
                    }

                    foundInternet = true;
                }
            }

            if (!foundInternet)
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
                Debug.Log("[PixelRoad] Normalized INTERNET permission in the Android manifest.");
            }
        }

        /// <summary>빌드 종료 시점에 되돌릴 상태가 없다. 인터페이스를 맞추기 위해 비워 둔다.</summary>
        public void OnPostprocessBuild(BuildReport report)
        {
        }
    }
}
