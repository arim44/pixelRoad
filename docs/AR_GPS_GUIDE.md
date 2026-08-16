# AR 기능에서 GPS 위치 사용하기

## 현재 프로젝트에서 바로 쓰는 방법

AR 코드가 `Input.location.Start()`를 다시 호출하지 말고, `PixelRoadApp`이 이미 관리하는 위치를 공유해서 사용한다. `CurrentLocation`은 위도, 경도, 수평 정확도와 유효 상태를 제공한다.

```csharp
using PixelRoad.Location;
using PixelRoad.Runtime;
using UnityEngine;

public sealed class ArLocationConsumer : MonoBehaviour
{
    [SerializeField] private PixelRoadApp locationSource;

    private void Awake()
    {
        if (locationSource == null)
        {
            locationSource = FindFirstObjectByType<PixelRoadApp>();
        }
    }

    private void Update()
    {
        if (locationSource == null)
        {
            return;
        }

        GeoLocation location = locationSource.CurrentLocation;
        if (!location.IsValid)
        {
            return;
        }

        double latitude = location.Latitude;
        double longitude = location.Longitude;
        float accuracyMeters = location.HorizontalAccuracyMeters;

        // 방문 거리 계산, 주변 콘텐츠 조회 등에 사용한다.
    }
}
```

에디터에서는 `SimulatedLocationProvider`가 같은 인터페이스로 좌표를 공급하고, Android/iOS 빌드에서는 `UnityGpsLocationProvider`가 `Input.location.lastData`를 읽는다. 현재 설정은 목표 정확도 15m, 이동 갱신 거리 3m다.

## 별도 AR 씬을 만드는 경우

현재 프로젝트에는 AR Foundation, ARCore, ARKit과 AR Session/XR Origin이 아직 없다. 실제 카메라 AR 화면을 추가하려면 Package Manager에서 서로 같은 호환 6.x 계열로 다음 패키지를 설치하고 플랫폼 공급자를 활성화한다.

- `com.unity.xr.arfoundation`
- `com.unity.xr.management`
- Android: `com.unity.xr.arcore`
- iOS: `com.unity.xr.arkit`

AR 씬에는 최소한 `AR Session`과 `XR Origin (Mobile AR)`/AR Camera가 필요하다. 별도 씬 전환 중에도 GPS를 계속 공유하려면 `PixelRoadApp` 또는 별도 위치 서비스 오브젝트를 `DontDestroyOnLoad`로 유지해야 한다. 씬마다 여러 컴포넌트가 `Input.location.Start()`/`Stop()`을 각각 호출하지 않는다.

## 권한과 수명주기

Android Manifest에는 현재 `ACCESS_COARSE_LOCATION`과 `ACCESS_FINE_LOCATION`이 선언되어 있다. 전경 AR만 필요하므로 백그라운드 위치 권한은 추가하지 않는다. Android 12 이상에서는 두 권한을 함께 요청하고, 사용자가 대략적 위치만 허용한 경우 방문 인증이 부정확할 수 있음을 안내한다.

iOS Player Settings에는 배포 전에 다음 설명을 채워야 한다.

- Location Usage Description: `주변 랜드마크 방문을 확인하고 AR 콘텐츠를 표시하기 위해 위치를 사용합니다.`
- Camera Usage Description: AR 카메라가 필요한 이유

앱이 일시정지될 때 위치 서비스를 멈추고 복귀할 때 권한과 시스템 위치 설정을 다시 확인해야 한다. Unity 위치 서비스는 시작 직후 초기화 시간이 필요하므로 `LocationServiceStatus.Running`이 된 뒤에만 `lastData`를 읽는다.

공식 참고 문서:

- [Unity LocationService.Start](https://docs.unity3d.com/ScriptReference/LocationService.Start.html)
- [Unity LocationService.lastData](https://docs.unity3d.com/ScriptReference/LocationService-lastData.html)
- [Android 위치 권한](https://developer.android.com/develop/sensors-and-location/location/permissions/runtime)
- [Unity AR Foundation](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.xr.arfoundation.html)

## GPS 방문 판정과 지리 기반 AR의 차이

현재 GPS는 랜드마크까지의 거리 계산과 방문 인증에 적합하다. 특정 위·경도에 3D 오브젝트를 실제 공간에 정밀하게 고정하려면 GPS 좌표를 Unity `Transform`에 바로 넣으면 안 된다. GPS 오차와 AR의 로컬 좌표계가 다르기 때문이다.

정밀한 지리 앵커가 필요하면 Android에서는 ARCore Extensions Geospatial API/VPS, iOS에서는 ARKit `ARGeoAnchor`를 별도 검토한다. 방향 안내만 필요하면 위치 서비스와 함께 나침반의 `trueHeading`을 사용한다.

