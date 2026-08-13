using System;
using System.IO;
using UnityEngine;

namespace PixelRoad.AR
{
    /// <summary>
    /// AR 화면 캡처를 기기 갤러리에 저장한다.
    /// Android는 스코프드 스토리지(API 29+) MediaStore를 통해 저장하므로 별도 저장소 권한이 필요 없다.
    /// 그보다 낮은 API 레벨의 기기는 지원 범위 밖이며, 실패 시 예외를 잡아 로그만 남기고 앱은 계속 동작한다.
    /// 에디터/그 외 플랫폼에서는 실기기 테스트가 불가능하므로 persistentDataPath 아래에 파일로만 남긴다.
    /// </summary>
    public static class AndroidGalleryExporter
    {
        /// <summary>galleryFolder는 ARConfig.screenshotGalleryFolder에서 온다(예: "Pictures/PixelRoad").</summary>
        public static void SaveScreenshot(byte[] pngBytes, string fileName, string galleryFolder)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                SaveToAndroidGallery(pngBytes, fileName, galleryFolder);
                Debug.Log("[PixelRoad] 스크린샷을 갤러리에 저장했습니다: " + fileName);
            }
            catch (Exception exception)
            {
                Debug.LogError("[PixelRoad] 갤러리 저장 실패: " + exception.Message);
            }
#else
            SaveToLocalFolder(pngBytes, fileName, galleryFolder);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void SaveToAndroidGallery(byte[] pngBytes, string fileName, string galleryFolder)
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

                    using (imageUri)
                    using (AndroidJavaObject outputStream = contentResolver.Call<AndroidJavaObject>("openOutputStream", imageUri))
                    {
                        outputStream.Call("write", pngBytes);
                        outputStream.Call("flush");
                        outputStream.Call("close");
                    }
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
