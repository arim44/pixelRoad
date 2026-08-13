using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRoad.UI
{
    /// <summary>
    /// landmarks.json의 thumbnail 필드(그리고 category)를 Resources 스프라이트로 해석한다.
    ///
    /// 조회 순서(fallback):
    ///   1. Resources/{folder}/{icon}      - JSON thumbnail 필드
    ///   2. Resources/{folder}/{category}  - JSON category 필드
    ///   3. Resources/{folder}/{default}   - map_config.defaultSpotIconName
    ///   4. null                           - 호출측이 코드 생성 도형 마커로 대체
    ///
    /// 결과는 실패(null)까지 캐시해 같은 키로 Resources.Load를 반복하지 않는다.
    /// </summary>
    public sealed class SpotIconLibrary
    {
        private readonly Dictionary<string, Sprite> cache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private readonly string folder;
        private readonly string defaultIconName;
        private bool missingIconLogged;

        public SpotIconLibrary(string resourceFolder, string defaultIconName)
        {
            folder = NormalizeFolder(resourceFolder);
            this.defaultIconName = string.IsNullOrWhiteSpace(defaultIconName)
                ? string.Empty
                : defaultIconName.Trim();
        }

        /// <summary>아이콘 키 → 카테고리 키 → 기본 아이콘 순으로 스프라이트를 찾는다. 모두 없으면 null.</summary>
        public Sprite Resolve(string iconKey, string categoryKey)
        {
            Sprite sprite = Load(iconKey);
            if (sprite == null)
            {
                sprite = Load(categoryKey);
            }

            if (sprite == null)
            {
                sprite = Load(defaultIconName);
            }

            if (sprite == null && !missingIconLogged)
            {
                missingIconLogged = true;
                Debug.Log(
                    "[PixelRoad] 스팟 아이콘을 찾지 못해 기본 도형 마커를 사용합니다. " +
                    "PNG를 Resources/" + folder + " 아래에 JSON thumbnail 값과 같은 이름으로 넣으면 자동으로 적용됩니다. " +
                    "(찾은 키: " + iconKey + " / " + categoryKey + " / " + defaultIconName + ")");
            }

            return sprite;
        }

        /// <summary>단일 키로 스프라이트를 조회한다. 없으면 null.</summary>
        public Sprite Load(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            string trimmed = key.Trim();
            if (cache.TryGetValue(trimmed, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = LoadFromResources(trimmed);
            cache[trimmed] = sprite;
            return sprite;
        }

        private Sprite LoadFromResources(string key)
        {
            string path = folder + key;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            // 텍스처 타입이 Sprite로 임포트되지 않은 PNG도 살려 준다.
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static string NormalizeFolder(string resourceFolder)
        {
            if (string.IsNullOrWhiteSpace(resourceFolder))
            {
                return string.Empty;
            }

            string trimmed = resourceFolder.Trim().Replace('\\', '/').TrimStart('/');
            return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : trimmed + "/";
        }
    }
}
