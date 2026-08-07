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
    /// A non-development build without the approved live-map symbol is an offline
    /// review flavor. Unity may infer INTERNET from unrelated packages, so remove it
    /// from that generated flavor only. Development and approved live builds retain it.
    /// </summary>
    public sealed class PixelRoadAndroidManifestPostprocessor :
        IPreprocessBuildWithReport,
        IPostGenerateGradleAndroidProject,
        IPostprocessBuildWithReport
    {
        private const string LiveMapSymbol = "PIXELROAD_LIVE_VECTOR_MAP";
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private const string InternetPermission = "android.permission.INTERNET";
        private static bool developmentBuildInProgress;

        public int callbackOrder
        {
            get { return 1000; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            developmentBuildInProgress = report != null
                && (report.summary.options & BuildOptions.Development) != 0;
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            bool includeInternet = developmentBuildInProgress
                || EditorUserBuildSettings.development
                || HasAndroidDefine(LiveMapSymbol);

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

        public void OnPostprocessBuild(BuildReport report)
        {
            developmentBuildInProgress = false;
        }

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
