using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран «Опции» с вкладками «Графика», «Звук» и «Игра».
/// Всё применяется сразу и сохраняется в AppSettings.
/// </summary>
public class OptionsScreen
{
    private enum Tab { Graphics, Sound, Game }

    private readonly List<MenuButton> mTabButtons = new List<MenuButton>();
    private readonly Dictionary<Tab, GameObject> mPanels = new Dictionary<Tab, GameObject>();
    private Tab mTab = Tab.Graphics;
    private readonly List<Action> mRefreshers = new List<Action>();
    private List<Vector2Int> mResolutions;

    public GameObject Root { get; private set; }

    /// <summary>Игрок сменил язык (меню решает, пересобрать ли интерфейс сразу).</summary>
    public event Action LanguageChanged;

    private Text mLanguageNote;

    /// <summary>Показать под строкой языка «сменится, когда выйдешь в главное меню».</summary>
    public void ShowLanguagePending(bool pending)
    {
        if (mLanguageNote != null)
            mLanguageNote.gameObject.SetActive(pending);
    }

    /// <summary>Собрать экран. close — «назад» и «сохранить».</summary>
    public GameObject Build(Transform canvas, Action close, Action<Transform, string, int, Vector2> title,
        Action<Transform, Action, string, Action> bottomBar)
    {
        var root = UiKit.CreateScreen("Options", canvas, UiKit.Background, "bg_menu");
        Root = root.gameObject;

        title(root, L.T("Опции"), 64, new Vector2(0, -50));

        // Вкладки.
        var tabs = UiKit.CreateRow("Tabs", root, 16);
        tabs.anchorMin = tabs.anchorMax = new Vector2(0.5f, 1);
        tabs.pivot = new Vector2(0.5f, 1);
        tabs.anchoredPosition = new Vector2(0, -140);
        AddTab(tabs, L.T("Графика"), Tab.Graphics);
        AddTab(tabs, L.T("Звук"), Tab.Sound);
        AddTab(tabs, L.T("Игра"), Tab.Game);

        mPanels[Tab.Graphics] = BuildGraphics(root);
        mPanels[Tab.Sound] = BuildSound(root);
        mPanels[Tab.Game] = BuildGame(root);

        bottomBar(root, close, "save", close);

        // F11 нажали, пока опции открыты, — показать новый режим.
        AppPerformance.ScreenChanged += () =>
        {
            if (Root != null && Root.activeInHierarchy)
                Refresh();
        };
        return Root;
    }

    /// <summary>Обновить значения на экране (при каждом открытии).</summary>
    public void Refresh()
    {
        foreach (Action r in mRefreshers)
            r();
        for (int i = 0; i < mTabButtons.Count; i++)
            mTabButtons[i].SetSelected(i == (int)mTab);
        foreach (var kv in mPanels)
            kv.Value.SetActive(kv.Key == mTab);
    }

    private void AddTab(Transform row, string label, Tab tab)
    {
        mTabButtons.Add(UiKit.CreatePlateButton(row, label, new Vector2(250, 64), () =>
        {
            mTab = tab;
            Refresh();
        }, 32));
    }

    private static RectTransform Panel(Transform root, string name)
    {
        var col = UiKit.CreateColumn(name, root, 8);
        col.anchorMin = col.anchorMax = new Vector2(0.5f, 1);
        col.pivot = new Vector2(0.5f, 1);
        col.anchoredPosition = new Vector2(0, -230);
        return col;
    }

    #region Графика

    private GameObject BuildGraphics(Transform root)
    {
        var col = Panel(root, "Graphics");

        // На телефоне игра всегда во весь экран: режим экрана, разрешение и VSync не нужны.
        if (Platform.IsDesktop)
            BuildScreenRows(col);

        var fps = UiKit.CreateSelector(UiKit.CreateSettingRow(col, L.T("Кадров в секунду")), d =>
        {
            int[] opts = AppSettings.FpsOptions;
            int i = Array.IndexOf(opts, AppSettings.FpsLimit);
            i = Mathf.Clamp((i < 0 ? 1 : i) + d, 0, opts.Length - 1);
            AppSettings.FpsLimit = opts[i];
            AppPerformance.ApplyFrameRate();
            Refresh();
        });
        mRefreshers.Add(() => fps.text = AppSettings.VSync && Platform.IsDesktop
            ? L.T("как у монитора (VSync)")
            : AppSettings.FpsLimit > 0 ? AppSettings.FpsLimit.ToString() : L.T("без ограничения"));

        if (Platform.IsDesktop)
        {
            var vsync = UiKit.CreateSelector(UiKit.CreateSettingRow(col, L.T("Вертикальная синхронизация")), d =>
            {
                AppSettings.VSync = !AppSettings.VSync;
                AppPerformance.ApplyFrameRate();
                Refresh();
            });
            mRefreshers.Add(() => vsync.text = AppSettings.VSync ? L.T("Вкл") : L.T("Выкл"));
        }

        Text brightnessValue = null;
        var brightness = UiKit.CreateSlider(UiKit.CreateSettingRow(col, L.T("Яркость")), 0.5f, 1.5f, v =>
        {
            AppSettings.Brightness = v;
            AppPerformance.ApplyBrightness();
            brightnessValue.text = Percent(v);
        }, out brightnessValue);
        mRefreshers.Add(() =>
        {
            brightness.SetValueWithoutNotify(AppSettings.Brightness);
            brightnessValue.text = Percent(AppSettings.Brightness);
        });

        if (Platform.IsDesktop)
            Note(col, L.T("F11 — окно ⇄ полный экран. Окно можно тянуть мышкой за край.\nРежим экрана и разрешение работают только в собранной игре, не в редакторе."));
        else
            Note(col, L.T("Меньше кадров в секунду — телефон меньше греется и дольше работает от батареи."));
        return col.gameObject;
    }

    /// <summary>Строки «Экран» и «Разрешение / Размер окна» — только на ПК.</summary>
    private void BuildScreenRows(Transform col)
    {
        var screen = UiKit.CreateSelector(UiKit.CreateSettingRow(col, L.T("Экран")), d =>
        {
            AppSettings.Fullscreen = !AppSettings.Fullscreen;
            AppPerformance.ApplyScreen();
            Refresh();
        });
        mRefreshers.Add(() => screen.text = AppSettings.Fullscreen ? L.T("Полный экран") : L.T("Окно"));

        // В полном экране — разрешение, в окне — размер окна (его же можно тянуть мышкой).
        RectTransform resRow = UiKit.CreateSettingRow(col, L.T("Разрешение"));
        Text resLabel = resRow.parent.GetComponentInChildren<Text>();
        var res = UiKit.CreateSelector(resRow, d =>
        {
            var list = Resolutions();
            int i = Mathf.Clamp(CurrentResolutionIndex(list) + d, 0, list.Count - 1);
            string value = $"{list[i].x}x{list[i].y}";
            if (AppSettings.Fullscreen)
                AppSettings.Resolution = value;
            else
                AppSettings.WindowSize = value;
            AppPerformance.ApplyScreen();
            Refresh();
        });
        mRefreshers.Add(() =>
        {
            resLabel.text = AppSettings.Fullscreen ? L.T("Разрешение") : L.T("Размер окна");
            var list = Resolutions();
            Vector2Int r = list[CurrentResolutionIndex(list)];
            res.text = $"{r.x} × {r.y}";
        });
    }

    /// <summary>
    /// Разрешения монитора (без повторов по частоте), от меньшего к большему.
    /// Для окна — только те, что влезают в экран, плюс текущий размер окна.
    /// </summary>
    private List<Vector2Int> Resolutions()
    {
        var set = new HashSet<Vector2Int>();
        int screenW = Screen.currentResolution.width, screenH = Screen.currentResolution.height;
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.width < 800 || r.height < 600)
                continue;
            if (!AppSettings.Fullscreen && (r.width > screenW * 0.9f || r.height > screenH * 0.9f))
                continue;
            set.Add(new Vector2Int(r.width, r.height));
        }
        if (AppSettings.Fullscreen)
            set.Add(new Vector2Int(screenW, screenH));
        else
            set.Add(AppPerformance.WindowSize());

        mResolutions = new List<Vector2Int>(set);
        mResolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return mResolutions;
    }

    private static int CurrentResolutionIndex(List<Vector2Int> list)
    {
        Vector2Int current;
        if (!AppSettings.Fullscreen)
            current = AppPerformance.WindowSize();
        else if (AppPerformance.TryParseResolution(AppSettings.Resolution, out int w, out int h))
            current = new Vector2Int(w, h);
        else
            current = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        int i = list.IndexOf(current);
        return i >= 0 ? i : list.Count - 1;
    }

    #endregion

    #region Звук

    private GameObject BuildSound(Transform root)
    {
        var col = Panel(root, "Sound");

        AddVolume(col, L.T("Общая громкость"), () => AppSettings.MasterVolume, v =>
        {
            AppSettings.MasterVolume = v;
            AppPerformance.ApplyVolume();
        });
        AddVolume(col, L.T("Звуки"), () => AppSettings.SoundVolume, v => AppSettings.SoundVolume = v);
        AddVolume(col, L.T("Музыка"), () => AppSettings.MusicVolume, v => AppSettings.MusicVolume = v);

        AddToggle(col, L.T("Звуки"), () => AppSettings.Sound, v => AppSettings.Sound = v, L.T("Вкл"), L.T("Выкл"));
        AddToggle(col, L.T("Музыка"), () => AppSettings.Music, v => AppSettings.Music = v, L.T("Вкл"), L.T("Выкл"));

        // Авторство сторонних материалов (лицензия CC BY-SA эмодзи требует указать его в игре).
        Note(col, L.T("Звуки — Kenney (kenney.nl). Музыка — joth, omfgdude, cynicmusic (OpenGameArt). Всё под CC0.\n")
                  + L.T("Эмодзи в чате — OpenMoji (openmoji.org), лицензия CC BY-SA 4.0."));
        return col.gameObject;
    }

    private void AddVolume(Transform col, string label, Func<float> get, Action<float> set)
    {
        Text value = null;
        var slider = UiKit.CreateSlider(UiKit.CreateSettingRow(col, label), 0f, 1f, v =>
        {
            set(v);
            value.text = Percent(v);
        }, out value);
        mRefreshers.Add(() =>
        {
            slider.SetValueWithoutNotify(get());
            value.text = Percent(get());
        });
    }

    #endregion

    #region Игра

    private GameObject BuildGame(Transform root)
    {
        var col = Panel(root, "Game");

        // Язык: как в системе → русский → English → …
        var lang = UiKit.CreateSelector(UiKit.CreateSettingRow(col, L.T("Язык") + " / Language"), d =>
        {
            int v = AppSettings.Language + d;
            AppSettings.Language = v > 1 ? -1 : v < -1 ? 1 : v;
            AppSettings.Save();
            Refresh();
            LanguageChanged?.Invoke();
        });
        mRefreshers.Add(() => lang.text = AppSettings.Language == 0 ? "Русский"
            : AppSettings.Language == 1 ? "English"
            : L.T("Как в системе"));
        mLanguageNote = UiKit.CreateText("LanguageNote", col, L.T("Язык сменится, когда выйдешь в главное меню."),
            22, new Color32(255, 200, 120, 255));
        UiKit.FitText(mLanguageNote, 1000, 32);
        mLanguageNote.gameObject.SetActive(false);

        AddToggle(col, L.T("Подсказки ходов"), () => AppSettings.ShowMoveHints, v => AppSettings.ShowMoveHints = v, L.T("Вкл"), L.T("Выкл"));
        AddToggle(col, L.T("Последний ход"), () => AppSettings.ShowLastMove, v => AppSettings.ShowLastMove = v, L.T("Подсвечивать"), L.T("Не подсвечивать"));
        AddToggle(col, L.T("Эффекты"), () => AppSettings.Effects, v => AppSettings.Effects = v, L.T("Вкл"), L.T("Выкл"));
        AddToggle(col, L.T("Подсказки под доской"), () => AppSettings.ShowHintText, v => AppSettings.ShowHintText = v, L.T("Вкл"), L.T("Выкл"));

        Note(col, L.T("Способности загоревшихся фигур (для новых партий):"));
        AddToggle(col, L.T("Троянский конь"), () => AppSettings.TrojanKnight, v => AppSettings.TrojanKnight = v, L.T("Да"), L.T("Нет"));
        AddToggle(col, L.T("Ферзь ходит и как конь"), () => AppSettings.QueenMovesAsKnight, v => AppSettings.QueenMovesAsKnight = v, L.T("Да"), L.T("Нет"));
        AddToggle(col, L.T("Пробивание слоном и ладьёй"), () => AppSettings.EnablePierce, v => AppSettings.EnablePierce = v, L.T("Да"), L.T("Нет"));
        AddToggle(col, L.T("Пробивать сквозь свои"), () => AppSettings.PierceThroughOwnPieces, v => AppSettings.PierceThroughOwnPieces = v, L.T("Да"), L.T("Нет"));
        return col.gameObject;
    }

    #endregion

    private void AddToggle(Transform col, string label, Func<bool> get, Action<bool> set, string on, string off)
    {
        var value = UiKit.CreateSelector(UiKit.CreateSettingRow(col, label), d =>
        {
            set(!get());
            Refresh();
        });
        mRefreshers.Add(() => value.text = get() ? on : off);
    }

    private static void Note(Transform col, string text)
    {
        var t = UiKit.CreateText("Note", col, text, 22, new Color(0.72f, 0.77f, 0.72f, 1f));
        int lines = text.Split('\n').Length;
        UiKit.FitText(t, 1000, 14 + 28 * lines);
    }

    private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
}
