using System;
using UnityEditor;
using UnityEngine;

namespace PixelRoad.Editor
{
    /// <summary>
    /// Figma에서 내보낸 아이콘 PNG를 Sprite로 자동 임포트한다.
    /// Assets/Resources/PixelRoad/Icons/ 아래에 들어오는 텍스처에만 적용되며,
    /// UI에서 바로 쓸 수 있도록 스프라이트 설정(단일 스프라이트, 알파 투명, 밉맵 해제,
    /// 클램프 래핑, 무압축)을 일괄 지정해 아티스트가 인스펙터에서 매번 손보지 않아도 되게 한다.
    /// </summary>
    internal sealed class PixelRoadIconImporter : AssetPostprocessor
    {
        private const string IconFolderPath = "Assets/Resources/PixelRoad/Icons/";

        /// <summary>아이콘 폴더의 텍스처에만 UI 스프라이트 임포트 설정을 적용한다.</summary>
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconFolderPath, StringComparison.Ordinal))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 128f;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
