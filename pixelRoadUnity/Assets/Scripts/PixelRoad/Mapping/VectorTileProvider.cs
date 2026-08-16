using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PixelRoad.Data;
using UnityEngine;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// 응답 헤더에서 뽑아낸 캐시 판단 정보. 만료 시각과 재검증에 쓸 값들을 함께 들고 있다.
    /// </summary>
    public readonly struct VectorTileCacheMetadata
    {
        public readonly long ExpiresUnixSeconds;
        public readonly string ETag;
        public readonly string LastModified;

        /// <summary>만료 시각과 재검증용 헤더 값을 묶는다.</summary>
        public VectorTileCacheMetadata(long expiresUnixSeconds, string etag, string lastModified)
        {
            ExpiresUnixSeconds = expiresUnixSeconds;
            ETag = etag;
            LastModified = lastModified;
        }
    }

    /// <summary>
    /// Immutable vector-tile provider configuration and HTTP policy helpers.
    /// This type does not issue network requests.
    /// </summary>
    public sealed class VectorTileProvider
    {
        public const long DefaultCacheLifetimeSeconds = 7L * 24L * 60L * 60L;
        public const string RequestedWithHeaderName = "X-Requested-With";

        private const int MaximumSupportedSlippyZoom = 30;
        private const string FallbackApplicationIdentifier = "PixelRoad";

        public string ProviderId { get; }
        public string UrlTemplate { get; }
        public int MinimumZoom { get; }
        public int MaximumZoom { get; }
        public string RequestedWithHeaderValue { get; }
        public string ValidationError { get; }
        public bool IsValid => string.IsNullOrEmpty(ValidationError);

        /// <summary>실행 중인 앱 식별자를 그대로 써서 제공자를 만든다.</summary>
        public VectorTileProvider(MapConfig config)
            : this(config, Application.identifier)
        {
        }

        /// <summary>
        /// 설정을 읽어 들이고 곧바로 검증한다. 잘못된 설정은 예외 대신 ValidationError로 남겨
        /// 호출 측이 지도를 끄는 판단을 할 수 있게 한다.
        /// </summary>
        public VectorTileProvider(MapConfig config, string applicationIdentifier)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            ProviderId = (config.vectorTileProviderId ?? string.Empty).Trim();
            UrlTemplate = (config.vectorTileUrlTemplate ?? string.Empty).Trim();
            MinimumZoom = config.vectorTileMinZoom;
            MaximumZoom = config.vectorTileMaxZoom;
            RequestedWithHeaderValue = BuildRequestedWithHeaderValue(applicationIdentifier);
            ValidationError = ValidateConfiguration(
                ProviderId,
                UrlTemplate,
                MinimumZoom,
                MaximumZoom);
        }

        /// <summary>타일 키로 요청 URL을 만든다. 실패 사유가 필요 없을 때 쓴다.</summary>
        public bool TryBuildTileUrl(TileKey key, out string url)
        {
            return TryBuildTileUrl(key.Zoom, key.X, key.Y, out url, out _);
        }

        /// <summary>타일 키로 요청 URL을 만들고 실패하면 사유도 함께 돌려준다.</summary>
        public bool TryBuildTileUrl(TileKey key, out string url, out string error)
        {
            return TryBuildTileUrl(key.Zoom, key.X, key.Y, out url, out error);
        }

        /// <summary>
        /// 좌표를 템플릿에 채워 URL을 만든다. 범위를 벗어난 좌표나 HTTPS가 아닌 주소는
        /// 요청을 보내기 전에 여기서 걸러진다.
        /// </summary>
        public bool TryBuildTileUrl(int zoom, int x, int y, out string url, out string error)
        {
            url = null;
            error = null;

            if (!IsValid)
            {
                error = ValidationError;
                return false;
            }

            if (zoom < MinimumZoom || zoom > MaximumZoom)
            {
                error = "Tile zoom is outside the configured provider range.";
                return false;
            }

            // MaximumSupportedSlippyZoom keeps this shift within a positive Int32.
            int tileCount = 1 << zoom;
            if (x < 0 || x >= tileCount || y < 0 || y >= tileCount)
            {
                error = "Tile coordinates are outside the Slippy Map range for this zoom.";
                return false;
            }

            string candidate = UrlTemplate
                .Replace("{z}", zoom.ToString(CultureInfo.InvariantCulture))
                .Replace("{x}", x.ToString(CultureInfo.InvariantCulture))
                .Replace("{y}", y.ToString(CultureInfo.InvariantCulture));

            if (!TryValidateHttpsUrl(candidate, out error))
            {
                return false;
            }

            url = candidate;
            return true;
        }

        /// <summary>
        /// 앱 식별자를 헤더에 넣어도 안전한 문자만 남기도록 다듬는다.
        /// 타일 제공자가 요구하는 사용 주체 표기를 지키기 위한 값이다.
        /// </summary>
        public static string BuildRequestedWithHeaderValue(string applicationIdentifier)
        {
            string source = string.IsNullOrWhiteSpace(applicationIdentifier)
                ? FallbackApplicationIdentifier
                : applicationIdentifier.Trim();

            StringBuilder sanitized = new StringBuilder(source.Length);
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                bool isLetterOrDigit = character >= 'a' && character <= 'z'
                    || character >= 'A' && character <= 'Z'
                    || character >= '0' && character <= '9';
                if (isLetterOrDigit || character == '.' || character == '_' || character == '-')
                {
                    sanitized.Append(character);
                }
                else
                {
                    sanitized.Append('-');
                }
            }

            string value = sanitized.ToString().Trim('-');
            return string.IsNullOrEmpty(value) ? FallbackApplicationIdentifier : value;
        }

        /// <summary>
        /// 응답 헤더를 해석해 캐시 만료 시각과 재검증 값을 뽑는다.
        /// Age 헤더가 있으면 중간 캐시에서 이미 흘러간 시간만큼 수명을 줄인다.
        /// </summary>
        public static VectorTileCacheMetadata ParseResponseCacheHeaders(
            IDictionary<string, string> responseHeaders,
            long nowUnixSeconds)
        {
            TryGetHeaderValue(responseHeaders, "Cache-Control", out string cacheControl);
            TryGetHeaderValue(responseHeaders, "Expires", out string expires);
            TryGetHeaderValue(responseHeaders, "Age", out string age);
            TryGetHeaderValue(responseHeaders, "ETag", out string etag);
            TryGetHeaderValue(responseHeaders, "Last-Modified", out string lastModified);

            long expiry = CalculateExpiryUnixSeconds(nowUnixSeconds, cacheControl, expires);
            if (TryParseCacheControl(cacheControl, out long maxAgeSeconds, out bool requiresRevalidation)
                && !requiresRevalidation
                && long.TryParse(
                    age,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long currentAgeSeconds)
                && currentAgeSeconds > 0L)
            {
                long remainingSeconds = Math.Max(0L, maxAgeSeconds - currentAgeSeconds);
                expiry = SaturatingAdd(nowUnixSeconds, remainingSeconds);
            }

            return new VectorTileCacheMetadata(
                expiry,
                NormalizeOptionalHeaderValue(etag),
                NormalizeOptionalHeaderValue(lastModified));
        }

        /// <summary>
        /// Cache-Control과 Expires를 순서대로 따져 만료 시각을 정한다.
        /// 둘 다 없으면 기본 수명을 적용해 캐시가 아예 안 쓰이는 상황을 막는다.
        /// </summary>
        public static long CalculateExpiryUnixSeconds(
            long nowUnixSeconds,
            string cacheControl,
            string expires)
        {
            if (TryParseCacheControl(cacheControl, out long maxAgeSeconds, out bool requiresRevalidation))
            {
                return requiresRevalidation
                    ? nowUnixSeconds
                    : SaturatingAdd(nowUnixSeconds, maxAgeSeconds);
            }

            if (requiresRevalidation)
            {
                return nowUnixSeconds;
            }

            if (string.Equals(expires?.Trim(), "0", StringComparison.Ordinal))
            {
                return nowUnixSeconds;
            }

            string normalizedExpires = string.IsNullOrWhiteSpace(expires) ? null : expires.Trim();
            if (string.Equals(normalizedExpires, "0", StringComparison.Ordinal))
            {
                return nowUnixSeconds;
            }

            if (normalizedExpires != null
                && DateTimeOffset.TryParse(
                    normalizedExpires,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces
                        | DateTimeStyles.AssumeUniversal
                        | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset expiresAt))
            {
                return expiresAt.ToUnixTimeSeconds();
            }

            return SaturatingAdd(nowUnixSeconds, DefaultCacheLifetimeSeconds);
        }

        /// <summary>헤더 이름을 대소문자 구분 없이 찾는다. 서버마다 표기가 달라 필요한 처리다.</summary>
        public static bool TryGetHeaderValue(
            IDictionary<string, string> headers,
            string headerName,
            out string value)
        {
            value = null;
            if (headers == null || string.IsNullOrEmpty(headerName))
            {
                return false;
            }

            if (headers.TryGetValue(headerName, out value))
            {
                return true;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase))
                {
                    value = header.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// 요청 대상이 자격 정보 없는 절대 HTTPS 주소인지 확인한다.
        /// 치환되지 않은 자리표시자가 남아 있으면 실패로 본다.
        /// </summary>
        public static bool TryValidateHttpsUrl(string url, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                error = "Vector tile URL is empty.";
                return false;
            }

            if (url.IndexOf('{') >= 0 || url.IndexOf('}') >= 0)
            {
                error = "Vector tile URL contains an unresolved placeholder.";
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(uri.Host))
            {
                error = "Vector tile URL must be an absolute HTTPS URL.";
                return false;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                error = "Vector tile URL must not contain user credentials.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 제공자 설정이 쓸 만한지 검사하고 문제가 있으면 사유 문자열을, 없으면 null을 돌려준다.
        /// </summary>
        private static string ValidateConfiguration(
            string providerId,
            string urlTemplate,
            int minimumZoom,
            int maximumZoom)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return "Vector tile provider id is empty.";
            }

            if (minimumZoom < 0
                || maximumZoom < minimumZoom
                || maximumZoom > MaximumSupportedSlippyZoom)
            {
                return "Vector tile zoom range must satisfy 0 <= min <= max <= 30.";
            }

            if (string.IsNullOrWhiteSpace(urlTemplate))
            {
                return "Vector tile URL template is empty.";
            }

            if (!ContainsExactlyOnce(urlTemplate, "{z}")
                || !ContainsExactlyOnce(urlTemplate, "{x}")
                || !ContainsExactlyOnce(urlTemplate, "{y}"))
            {
                return "Vector tile URL template must contain {z}, {x}, and {y} exactly once.";
            }

            string validationUrl = urlTemplate
                .Replace("{z}", "0")
                .Replace("{x}", "0")
                .Replace("{y}", "0");
            return TryValidateHttpsUrl(validationUrl, out string error) ? null : error;
        }

        /// <summary>템플릿에 자리표시자가 정확히 한 번만 들어 있는지 확인한다.</summary>
        private static bool ContainsExactlyOnce(string source, string value)
        {
            int firstIndex = source.IndexOf(value, StringComparison.Ordinal);
            return firstIndex >= 0
                && source.IndexOf(value, firstIndex + value.Length, StringComparison.Ordinal) < 0;
        }

        /// <summary>
        /// Cache-Control 지시자를 훑어 max-age 값과 재검증 필요 여부를 가려낸다.
        /// </summary>
        private static bool TryParseCacheControl(
            string cacheControl,
            out long maxAgeSeconds,
            out bool requiresRevalidation)
        {
            maxAgeSeconds = 0L;
            requiresRevalidation = false;
            bool foundMaxAge = false;

            if (string.IsNullOrWhiteSpace(cacheControl))
            {
                return false;
            }

            string[] directives = cacheControl.Split(',');
            for (int index = 0; index < directives.Length; index++)
            {
                string directive = directives[index].Trim();
                int equalsIndex = directive.IndexOf('=');
                string directiveName = equalsIndex < 0
                    ? directive
                    : directive.Substring(0, equalsIndex).Trim();
                if (string.Equals(directiveName, "no-cache", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(directiveName, "no-store", StringComparison.OrdinalIgnoreCase))
                {
                    requiresRevalidation = true;
                    continue;
                }

                if (equalsIndex <= 0
                    || !string.Equals(directiveName, "max-age", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = directive.Substring(equalsIndex + 1).Trim().Trim('"');
                if (long.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long parsedSeconds)
                    && parsedSeconds >= 0L)
                {
                    maxAgeSeconds = parsedSeconds;
                    foundMaxAge = true;
                }
            }

            return foundMaxAge;
        }

        /// <summary>빈 헤더 값을 null로 통일해 이후 비교를 단순하게 만든다.</summary>
        private static string NormalizeOptionalHeaderValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>긴 수명 값을 더할 때 오버플로로 만료 시각이 과거가 되는 것을 막는다.</summary>
        private static long SaturatingAdd(long left, long right)
        {
            return left > long.MaxValue - right ? long.MaxValue : left + right;
        }
    }
}
