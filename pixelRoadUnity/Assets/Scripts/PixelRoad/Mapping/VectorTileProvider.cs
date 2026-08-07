using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PixelRoad.Data;
using UnityEngine;

namespace PixelRoad.Mapping
{
    public readonly struct VectorTileCacheMetadata
    {
        public readonly long ExpiresUnixSeconds;
        public readonly string ETag;
        public readonly string LastModified;

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

        public VectorTileProvider(MapConfig config)
            : this(config, Application.identifier)
        {
        }

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

        public bool TryBuildTileUrl(TileKey key, out string url)
        {
            return TryBuildTileUrl(key.Zoom, key.X, key.Y, out url, out _);
        }

        public bool TryBuildTileUrl(TileKey key, out string url, out string error)
        {
            return TryBuildTileUrl(key.Zoom, key.X, key.Y, out url, out error);
        }

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

        private static bool ContainsExactlyOnce(string source, string value)
        {
            int firstIndex = source.IndexOf(value, StringComparison.Ordinal);
            return firstIndex >= 0
                && source.IndexOf(value, firstIndex + value.Length, StringComparison.Ordinal) < 0;
        }

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

        private static string NormalizeOptionalHeaderValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static long SaturatingAdd(long left, long right)
        {
            return left > long.MaxValue - right ? long.MaxValue : left + right;
        }
    }
}
