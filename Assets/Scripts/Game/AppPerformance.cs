using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Применяет настройки графики и звука и бережёт компьютер:
/// - экран: полный экран / окно и разрешение;
/// - кадры: лимит из настроек (15 в свёрнутом окне), VSync;
/// - яркость: полноэкранный слой поверх всего интерфейса;
/// - громкость: общая громкость игры;
/// - игра работает и в свёрнутом окне (медленно), чтобы сетевая партия не вставала;
/// - камера только заливает фон: 3D-сцена, свет и постобработка выключены — экран закрыт интерфейсом.
/// </summary>
public class AppPerformance : MonoBehaviour
{
    public const int BackgroundFps = 15;

    private static AppPerformance sInstance;
    private Image mBrightness;
    private bool mFocused = true;
    private int mIgnoreResizeFrames;

    void Awake()
    {
        sInstance = this;
        Application.runInBackground = true;
        SimplifyRendering();
        CreateBrightnessLayer();
        ApplyAll();
        ApplyCursor();
    }

    /// <summary>Курсор-стрелка в цветах кнопок (Resources/UI/cursor). Кончик — в левом верхнем углу.</summary>
    private static void ApplyCursor()
    {
        var cursor = Resources.Load<Texture2D>("UI/cursor");
        if (cursor != null)
            Cursor.SetCursor(cursor, new Vector2(1, 1), CursorMode.Auto);
    }

    void OnApplicationFocus(bool focused)
    {
        mFocused = focused;
        ApplyFrameRate();
    }

    /// <summary>Применить всё (при запуске).</summary>
    public static void ApplyAll()
    {
        ApplyScreen();
        ApplyFrameRate();
        ApplyBrightness();
        ApplyVolume();
    }

    /// <summary>Настройки экрана поменялись не из окна опций (например, F11) — опциям стоит обновиться.</summary>
    public static event System.Action ScreenChanged;

    /// <summary>
    /// Полный экран / окно. У полного экрана своё разрешение (по умолчанию как у монитора),
    /// у окна — свой размер (тот, до которого его растянули мышкой).
    /// </summary>
    public static void ApplyScreen()
    {
        if (AppSettings.Fullscreen)
        {
            if (!TryParseResolution(AppSettings.Resolution, out int w, out int h))
            {
                w = Screen.currentResolution.width;
                h = Screen.currentResolution.height;
            }
            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Vector2Int size = WindowSize();
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }
        if (sInstance != null)
            sInstance.mIgnoreResizeFrames = 10; // не путать смену режима с тем, что окно потянули мышкой
    }

    /// <summary>Переключить окно ⇄ полный экран (F11).</summary>
    public static void ToggleFullscreen()
    {
        AppSettings.Fullscreen = !AppSettings.Fullscreen;
        AppSettings.Save();
        ApplyScreen();
        ScreenChanged?.Invoke();
    }

    /// <summary>Размер окна: сохранённый или 1280×720, но не больше 90% монитора.</summary>
    public static Vector2Int WindowSize()
    {
        int maxW = Mathf.Max(640, (int)(Screen.currentResolution.width * 0.9f));
        int maxH = Mathf.Max(480, (int)(Screen.currentResolution.height * 0.9f));
        if (!TryParseResolution(AppSettings.WindowSize, out int w, out int h))
        {
            w = 1280;
            h = 720;
        }
        return new Vector2Int(Mathf.Clamp(w, 640, maxW), Mathf.Clamp(h, 480, maxH));
    }

    void Update()
    {
        if (F11Pressed())
            ToggleFullscreen();
        RememberWindowSize();
    }

    /// <summary>Окно потянули мышкой — запоминаем новый размер.</summary>
    private void RememberWindowSize()
    {
        if (mIgnoreResizeFrames > 0)
        {
            mIgnoreResizeFrames--;
            return;
        }
        if (AppSettings.Fullscreen || Screen.fullScreenMode != FullScreenMode.Windowed || Application.isEditor)
            return;

        string size = $"{Screen.width}x{Screen.height}";
        if (size != AppSettings.WindowSize)
            AppSettings.WindowSize = size; // на диск — при выходе
    }

    void OnApplicationQuit() => AppSettings.Save();

    private static bool F11Pressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.f11Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F11);
#endif
    }

    public static void ApplyFrameRate()
    {
        bool focused = sInstance == null || sInstance.mFocused;
        // VSync сам ограничивает кадры частотой монитора; без него работает targetFrameRate.
        QualitySettings.vSyncCount = AppSettings.VSync && focused ? 1 : 0;
        int limit = AppSettings.FpsLimit;
        Application.targetFrameRate = !focused ? BackgroundFps : limit > 0 ? limit : -1;
    }

    public static void ApplyBrightness()
    {
        if (sInstance == null || sInstance.mBrightness == null)
            return;
        float b = AppSettings.Brightness;
        // Шейдер умножает картинку на 2 × цвет: серый 0.5 — без изменений.
        float g = b * 0.5f;
        sInstance.mBrightness.color = new Color(g, g, g, 1f);
        sInstance.mBrightness.enabled = Mathf.Abs(b - 1f) > 0.001f;
    }

    public static void ApplyVolume()
    {
        AudioListener.volume = AppSettings.MasterVolume;
    }

    public static bool TryParseResolution(string text, out int width, out int height)
    {
        width = height = 0;
        if (string.IsNullOrEmpty(text))
            return false;
        string[] parts = text.Split('x');
        return parts.Length == 2 && int.TryParse(parts[0], out width) && int.TryParse(parts[1], out height)
               && width > 0 && height > 0;
    }

    /// <summary>Самый верхний слой: «яркость» поверх всего интерфейса, клики пропускает.</summary>
    private void CreateBrightnessLayer()
    {
        Shader shader = Resources.Load<Shader>("Shaders/UIBrightness");
        if (shader == null)
        {
            Debug.LogWarning("Нет шейдера яркости Resources/Shaders/UIBrightness — настройка яркости не работает");
            return;
        }

        var go = new GameObject("Brightness Canvas", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var rt = UiKit.CreateUi("Brightness", go.transform, typeof(Image));
        UiKit.Stretch(rt);
        mBrightness = rt.GetComponent<Image>();
        mBrightness.material = new Material(shader);
        mBrightness.raycastTarget = false;
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
