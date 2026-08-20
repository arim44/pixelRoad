using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRoad.UI
{
    /// <summary>
    /// landmarks.json의 category·thumbnail 필드를 Resources 스프라이트로 해석한다.
    /// 지도와 도감이 서로 다른 필드를 따르므로 두 조회 경로를 분리해 둔다.
    ///
    /// 지도·AR 마커 - <see cref="ResolveIconKey"/>로 키를 한 번 정하고 <see cref="Load"/>로 읽는다.
    ///   1. Resources/{folder}/{category}       - JSON category 필드 (예: 역사)
    ///   2. Resources/{folder}/{category 별칭}  - 한글 category의 영문 파일명 (예: history)
    ///   3. Resources/{folder}/{default}        - map_config.defaultSpotIconName
    ///   4. null                                - 호출측이 코드 생성 도형 마커로 대체
    ///
    /// 도감 이미지 - <see cref="ResolveThumbnail"/>
    ///   1. Resources/{folder}/{thumbnail}      - JSON thumbnail 필드
    ///   2. null                                - 호출측이 <see cref="Placeholder"/>로 대체
    ///
    /// 결과는 실패(null)까지 캐시해 같은 키로 Resources.Load를 반복하지 않는다.
    /// </summary>
    public sealed class SpotIconLibrary
    {
        /// <summary>
        /// 한글 category로 된 PNG를 두지 않아도 되도록 준비한 영문 파일명 별칭.
        /// `역사.png`와 `history.png` 중 있는 쪽을 쓴다.
        /// </summary>
        private static readonly Dictionary<string, string> CategoryFileAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "역사", "history" },
                { "문화", "culture" },
                { "교통", "transport" },
                { "공공", "public" },
                { "테스트", "test" },
            };

        private readonly Dictionary<string, Sprite> cache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private readonly string folder;
        private readonly string defaultIconName;
        private readonly string placeholderIconName;
        private Sprite placeholder;
        private bool placeholderLoaded;
        private bool missingIconLogged;
        private bool missingPlaceholderLogged;

        /// <summary>조회 기준이 될 Resources 폴더와 기본·대체 아이콘 이름을 고정한다.</summary>
        public SpotIconLibrary(string resourceFolder, string defaultIconName, string placeholderIconName)
        {
            folder = NormalizeFolder(resourceFolder);
            this.defaultIconName = Normalize(defaultIconName);
            this.placeholderIconName = Normalize(placeholderIconName);
        }

        /// <summary>
        /// 썸네일이 없거나 아직 해금하지 않은 랜드마크에 쓰는 대체 이미지.
        /// 한 번 읽어 두고 계속 돌려쓴다. 파일이 없으면 null.
        /// </summary>
        public Sprite Placeholder
        {
            get
            {
                if (!placeholderLoaded)
                {
                    placeholderLoaded = true;
                    placeholder = Load(placeholderIconName);
                    if (placeholder == null && !missingPlaceholderLogged)
                    {
                        missingPlaceholderLogged = true;
                        Debug.Log(
                            "[PixelRoad] 도감 대체 이미지를 찾지 못했습니다. " +
                            "Resources/" + folder + placeholderIconName + ".png 를 넣으면 " +
                            "썸네일이 없거나 잠긴 랜드마크에 자동으로 쓰입니다.");
                    }
                }

                return placeholder;
            }
        }

        /// <summary>
        /// 마커에 쓸 아이콘 키를 정한다. category만 따르고 thumbnail은 보지 않는다.
        /// 실제로 PNG가 있는 키를 돌려주므로 호출측은 <see cref="Load"/> 한 번이면 된다.
        ///   1. category 이름의 PNG가 있으면 category
        ///   2. 없으면 영문 별칭(예: 역사 → history) PNG가 있는지 보고 있으면 별칭
        ///   3. 둘 다 없으면 기본 아이콘 이름
        /// 랜드마크를 읽을 때 한 번만 부르고 결과를 <c>SpotDefinition.IconKey</c>에 담아 두므로
        /// 지도와 AR이 같은 category에 대해 반드시 같은 그림을 쓴다.
        /// </summary>
        public string ResolveIconKey(string categoryKey)
        {
            if (Load(categoryKey) != null)
            {
                return categoryKey.Trim();
            }

            string alias = AliasFor(categoryKey);
            if (Load(alias) != null)
            {
                return alias;
            }

            if (Load(defaultIconName) == null && !missingIconLogged)
            {
                missingIconLogged = true;
                Debug.Log(
                    "[PixelRoad] 카테고리 아이콘을 찾지 못해 기본 도형 마커를 사용합니다. " +
                    "PNG를 Resources/" + folder + " 아래에 JSON category 값과 같은 이름으로 넣으면 자동으로 적용됩니다. " +
                    "(찾은 키: " + categoryKey + " / " + alias + " / " + defaultIconName + ")");
            }

            return defaultIconName;
        }

        /// <summary>
        /// 도감에 쓰는 랜드마크 이미지. thumbnail만 따른다.
        /// 파일이 없으면 <see cref="Placeholder"/>를 돌려주고, 그것마저 없으면 null.
        /// </summary>
        public Sprite ResolveThumbnail(string thumbnailKey)
        {
            Sprite sprite = Load(thumbnailKey);
            return sprite != null ? sprite : Placeholder;
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

        /// <summary>한글 category에 대응하는 영문 파일명을 돌려준다. 별칭이 없으면 빈 문자열.</summary>
        private static string AliasFor(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
            {
                return string.Empty;
            }

            return CategoryFileAliases.TryGetValue(categoryKey.Trim(), out string alias)
                ? alias
                : string.Empty;
        }

        /// <summary>Resources에서 실제로 읽어 온다. Sprite로 임포트되지 않은 PNG는 Texture2D로 받아 스프라이트를 만든다.</summary>
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

        /// <summary>설정 문자열의 앞뒤 공백을 정리한다. 비어 있으면 빈 문자열.</summary>
        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        /// <summary>설정에 적힌 폴더 문자열을 `a/b/` 형태로 다듬어 키를 이어 붙일 수 있게 한다.</summary>
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
