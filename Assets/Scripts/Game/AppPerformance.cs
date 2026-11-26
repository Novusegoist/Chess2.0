using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Бережём компьютер. Игра — плоский интерфейс, ей не нужны сотни кадров в секунду и 3D-рендер:
/// - кадры ограничены (60 в активном окне, 15 в свёрнутом);
/// - игра продолжает работать в фоне медленно, а не засыпает — иначе в сетевой партии
///   встали бы часы и ходы соперника;
/// - камера только заливает фон: 3D-сцена, свет и постобработка выключены — весь экран закрыт интерфейсом.
/// </summary>
public class AppPerformance : MonoBehaviour
{
    public const int ActiveFps = 60;
    public const int BackgroundFps = 15;

    void Awake()
    {
        QualitySettings.vSyncCount = 0; // иначе targetFrameRate игнорируется
        Application.targetFrameRate = ActiveFps;
        Application.runInBackground = true;
        SimplifyRendering();
    }

    void OnApplicationFocus(bool focused)
    {
        Application.targetFrameRate = focused ? ActiveFps : BackgroundFps;
    }

    /// <summary>Камера ничего не рисует, кроме фона; свет и эффекты постобработки отключаются.</summary>
    private static void SimplifyRendering()
    {
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            cam.cullingMask = 0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiKit.Background;
            var urp = cam.GetComponent<UniversalAdditionalCameraData>();
            if (urp != null)
            {
                urp.renderPostProcessing = false;
                urp.antialiasing = AntialiasingMode.None;
                urp.renderShadows = false;
            }
        }

        foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            volume.enabled = false;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            light.enabled = false;
    }
}
