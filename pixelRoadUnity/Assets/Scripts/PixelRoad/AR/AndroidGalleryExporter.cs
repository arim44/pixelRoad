using System;
using System.IO;
using UnityEngine;

namespace PixelRoad.AR
{
    /// <summary>
    /// AR 화면 캡처를 기기 갤러리에 저장하고, 가장 최근 캡처를 앱 재시작 후에도 복원할 수 있도록
    /// 로컬에 캐시한다.
    /// Android는 스코프드 스토리지(API 29+) MediaStore를 통해 저장하므로 별도 저장소 권한이 필요 없다.
    /// 그보다 낮은 API 레벨의 기기는 지원 범위 밖이며, 실패 시 예외를 잡아 로그만 남기고 앱은 계속 동작한다.
    /// 에디터/그 외 플랫폼에서는 실기기 테스트가 불가능하므로 persistentDataPath 아래에 파일로만 남긴다.
    /// </summary>
    public static class AndroidGalleryExporter
    {
        private const string LastCaptureUriPrefsKey = "PixelRoad.LastCaptureUri";
        private const string LastCaptureCacheFileName = "last_capture.png";

        /// <summary>
        /// 갤러리에 저장한다. galleryFolder는 ARConfig.screenshotGalleryFolder에서 온다(예: "Pictures/PixelRoad").
        /// 성공하면 그 이미지의 콘텐츠 Uri 문자열을, 실패했거나 갤러리 저장을 지원하지 않는 플랫폼이면 null을 반환한다.
        /// </summary>
        public static string SaveScreenshot(byte[] pngBytes, string fileName, string galleryFolder)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return SaveToAndroidGallery(pngBytes, fileName, galleryFolder);
            }
            catch (Exception exception)
            {
                Debug.LogError("[PixelRoad] 갤러리 저장 실패: " + exception.Message);
                return null;
            }
#else
            SaveToLocalFolder(pngBytes, fileName, galleryFolder);
            return null;
#endif
        }

        /// <summary>
        /// 가장 최근 캡처를 앱 전용 캐시로 남긴다 - 다음 실행에서도 썸네일로 복원하고,
        /// 썸네일 클릭 시 어떤 사진을 열지 알기 위해 uriString도 함께 기억해 둔다.
        /// </summary>
        public static void CacheLastCapture(byte[] pngBytes, string uriString)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, LastCaptureCacheFileName);
                File.WriteAllBytes(path, pngBytes);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PixelRoad] 최근 캡처 캐시 저장 실패: " + exception.Message);
            }

            PlayerPrefs.SetString(LastCaptureUriPrefsKey, uriString ?? string.Empty);
            PlayerPrefs.Save();
        }

        /// <summary>이전에 CacheLastCapture로 남겨둔 캡처가 있으면 텍스처로 복원한다. 없으면 false.</summary>
        public static bool TryLoadCachedCapture(out Texture2D texture, out string uriString)
        {
            texture = null;
            uriString = PlayerPrefs.GetString(LastCaptureUriPrefsKey, string.Empty);

            string path = Path.Combine(Application.persistentDataPath, LastCaptureCacheFileName);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!loaded.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(loaded);
                    return false;
                }

                texture = loaded;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PixelRoad] 최근 캡처 캐시 로드 실패: " + exception.Message);
                return false;
            }
        }

        /// <summary>로컬 캐시 파일과 저장된 Uri를 지운다 - 원본 사진이 갤러리에서 삭제된 것으로 확인됐을 때 쓴다.</summary>
        public static void ClearCache()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, LastCaptureCacheFileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PixelRoad] 최근 캡처 캐시 삭제 실패: " + exception.Message);
            }

            PlayerPrefs.DeleteKey(LastCaptureUriPrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장된 콘텐츠 Uri를 시스템 사진 앱으로 연다.
        /// Intent를 AndroidJavaObject로 직접 조립하는 대신 Application.OpenURL을 쓴다 - Unity 엔진 내부의
        /// 순수 자바 코드로 ACTION_VIEW 인텐트를 실행해 주므로, Uri.parse()가 돌려주는 객체의 실제 런타임
        /// 클래스(Uri의 내부 서브클래스)와 AndroidJavaObject의 리플렉션 기반 시그니처 추론이 어긋나면서
        /// 발생하는 NoSuchMethodError나, 그걸 저수준 JNI로 우회하다 자바 예외를 놓쳐 앱이 죽는 위험이 없다.
        /// 다만 Application.OpenURL은 실패해도 예외를 던지지 않는 경우가 많아, 실패 감지는 보장되지 않는다.
        /// </summary>
        public static bool OpenInGallery(string uriString, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(uriString))
            {
                errorMessage = "저장된 사진 정보가 없습니다.";
                return false;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Application.OpenURL(uriString);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                Debug.LogWarning("[PixelRoad] 갤러리 열기 실패: " + exception.Message);
                return false;
            }
#else
            errorMessage = "에디터에서는 갤러리를 열 수 없습니다.";
            Debug.Log("[PixelRoad] " + errorMessage + " (" + uriString + ")");
            return false;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string SaveToAndroidGallery(byte[] pngBytes, string fileName, string galleryFolder)
        {
            using (AndroidJavaClass mediaColumns = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
            using (AndroidJavaObject externalContentUri = mediaColumns.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            using (AndroidJavaObject contentValues = new AndroidJavaObject("android.content.ContentValues"))
            {
                contentValues.Call("put", "_display_name", fileName);
                contentValues.Call("put", "mime_type", "image/png");
                contentValues.Call("put", "relative_path", galleryFolder);

                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject contentResolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                {
                    AndroidJavaObject imageUri = contentResolver.Call<AndroidJavaObject>("insert", externalContentUri, contentValues);
                    if (imageUri == null)
                    {
                        throw new InvalidOperationException("ContentResolver.insert가 null을 반환했습니다.");
                    }

                    string uriString = imageUri.Call<string>("toString");
                    using (AndroidJavaObject outputStream = contentResolver.Call<AndroidJavaObject>("openOutputStream", imageUri))
                    {
                        outputStream.Call("write", pngBytes);
                        outputStream.Call("flush");
                        outputStream.Call("close");
                    }

                    imageUri.Dispose();
                    Debug.Log("[PixelRoad] 스크린샷을 갤러리에 저장했습니다: " + fileName);
                    return uriString;
                }
            }
        }
#else
        private static void SaveToLocalFolder(byte[] pngBytes, string fileName, string galleryFolder)
        {
            string folder = Path.Combine(Application.persistentDataPath, galleryFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, pngBytes);
            Debug.Log("[PixelRoad] 스크린샷 저장(에디터 전용 경로): " + path);
        }
#endif
    }
}
