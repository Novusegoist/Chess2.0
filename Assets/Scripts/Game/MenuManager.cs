using System;
using System.Collections;
using System.Collections.Generic;
using Chess2.Core;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Меню игры: главный экран, настройка партии с ботом и вдвоём, опции, пауза, конец партии.
/// Всё строится из кода по картинкам из Resources/UI.
///
/// При запуске любой сцены без GameManager создаётся объект «Chess2» с GameManager и MenuManager
/// (см. Bootstrap) — сцену настраивать не нужно.
/// </summary>
[RequireComponent(typeof(GameManager))]
public class MenuManager : MonoBehaviour
{
    private enum Screen { None, Main, BotSetup, LocalSetup, Options, Pause, GameOver }

    private const float BigButtonHeight = 84f;

    private GameManager mGame;
    private Canvas mCanvas;
    private readonly Dictionary<Screen, GameObject> mScreens = new Dictionary<Screen, GameObject>();
    private Screen mCurrent = Screen.None;
    private Screen mOptionsReturn = Screen.Main;
    private GameObject mHud;
    private Text mGameOverText;

    // Группы «выбери один».
    private readonly List<MenuButton> mDifficultyButtons = new List<MenuButton>();
    private readonly List<MenuButton> mColorButtons = new List<MenuButton>();
    private readonly List<MenuButton> mBotModeButtons = new List<MenuButton>();
    private readonly List<MenuButton> mLocalModeButtons = new List<MenuButton>();
    private Text mBotModeHint;
    private Text mLocalModeHint;

    // Опции.
    private MenuButton mQueenKnightButton;
    private MenuButton mPierceButton;
    private MenuButton mPierceOwnButton;
    private MenuButton mTrojanButton;
    private readonly List<MenuButton> mSoundButtons = new List<MenuButton>();
    private readonly List<MenuButton> mMusicButtons = new List<MenuButton>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var game = FindAnyObjectByType<GameManager>();
        if (game == null)
            game = new GameObject("Chess2").AddComponent<GameManager>();
        if (game.GetComponent<MenuManager>() == null)
            game.gameObject.AddComponent<MenuManager>();
    }

    void Awake()
    {
        mGame = GetComponent<GameManager>();
        mGame.GameEnded += OnGameEnded;
    }

    void Start()
    {
        UiKit.EnsureEventSystem();
        BuildUi();
        Show(Screen.Main);
    }

    void OnDestroy()
    {
        if (mGame != null)
            mGame.GameEnded -= OnGameEnded;
    }

    void Update()
    {
        if (!EscapePressed())
            return;

        switch (mCurrent)
        {
            case Screen.None: OpenPause(); break;
            case Screen.Pause: ClosePause(); break;
            case Screen.Options: CloseOptions(); break;
            case Screen.BotSetup:
            case Screen.LocalSetup: Show(Screen.Main); break;
        }
    }

    #region Переходы

    private void Show(Screen screen)
    {
        mCurrent = screen;
        foreach (var kv in mScreens)
            kv.Value.SetActive(kv.Key == screen);

        bool inGame = mGame.State != null;
        mHud.SetActive(inGame && screen == Screen.None);
        mGame.Paused = inGame && screen != Screen.None && screen != Screen.GameOver;

        if (screen == Screen.BotSetup || screen == Screen.LocalSetup)
            RefreshSetup();
        if (screen == Screen.Options)
            RefreshOptions();
    }

    private void StartBotGame()
    {
        int color = AppSettings.PlayerColor;
        if (color == 2)
            color = UnityEngine.Random.Range(0, 2);
        Side botSide = color == 0 ? Side.Black : Side.White;

        AppSettings.Save();
        mGame.StartGame(true, botSide, AppSettings.Difficulty, AppSettings.BuildRules());
        Show(Screen.None);
    }

    private void StartLocalGame()
    {
        AppSettings.Save();
        mGame.StartGame(false, Side.Black, AppSettings.Difficulty, AppSettings.BuildRules());
        Show(Screen.None);
    }

    private void OpenPause()
    {
        if (mGame.State != null)
            Show(Screen.Pause);
    }

    private void ClosePause() => Show(Screen.None);

    private void OpenOptions(Screen returnTo)
    {
        mOptionsReturn = returnTo;
        Show(Screen.Options);
    }

    private void CloseOptions()
    {
        AppSettings.Save();
        Show(mOptionsReturn);
    }

    private void ToMainMenu()
    {
        mGame.StopGame();
        Show(Screen.Main);
    }

    private void OnGameEnded(GameState state)
    {
        mGameOverText.text = mGame.ResultText(state);
        StartCoroutine(ShowGameOverLater());
    }

    private IEnumerator ShowGameOverLater()
    {
        // Даём досмотреть последний ход.
        yield return new WaitForSeconds(1.2f);
        if (mGame.State != null && mGame.State.IsOver && mCurrent == Screen.None)
            Show(Screen.GameOver);
    }

    private static void Quit()
    {
        AppSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Сборка экранов

    private void BuildUi()
    {
        mCanvas = UiKit.CreateCanvas("Chess2 Menu Canvas", transform, 10);

        BuildHud();
        mScreens[Screen.Main] = BuildMain();
        mScreens[Screen.BotSetup] = BuildBotSetup();
        mScreens[Screen.LocalSetup] = BuildLocalSetup();
        mScreens[Screen.Options] = BuildOptions();
        mScreens[Screen.Pause] = BuildPause();
        mScreens[Screen.GameOver] = BuildGameOver();
    }

    /// <summary>Кнопка «Меню» поверх доски во время партии.</summary>
    private void BuildHud()
    {
        var rt = UiKit.CreateUi("HUD", mCanvas.transform);
        UiKit.Stretch(rt);
        mHud = rt.gameObject;

        var btn = UiKit.CreateImageButton(rt, "menu", 56, OpenPause);
        var brt = (RectTransform)btn.transform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0, 1);
        brt.anchoredPosition = new Vector2(24, -24);
    }

    private GameObject BuildMain()
    {
        var root = ScreenRoot("Main", UiKit.Background);

        // Декор: большие призраки по бокам.
        Decor(root, "Pieces/w_ghost", new Vector2(-560, -40), 420, 8f, 0.18f);
        Decor(root, "Pieces/b_ghost", new Vector2(560, -40), 420, -8f, 0.45f);

        Title(root, "ШАХМАТЫ 2.0", 96, new Vector2(0, -110));
        var sub = UiKit.CreateText("Subtitle", root, "фигуры не умирают — они слабеют", 30, new Color(0.8f, 0.85f, 0.8f, 1f));
        Place(sub.transform, new Vector2(0.5f, 1), new Vector2(0, -200));

        var col = UiKit.CreateColumn("Buttons", root, 22);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -70));
        UiKit.CreateImageButton(col, "play_bot", BigButtonHeight, () => Show(Screen.BotSetup));
        UiKit.CreateImageButton(col, "play_players", BigButtonHeight, () => Show(Screen.LocalSetup));
        UiKit.CreateImageButton(col, "options", BigButtonHeight, () => OpenOptions(Screen.Main));
        UiKit.CreateImageButton(col, "exit", BigButtonHeight, Quit);

        SoundToggles(root);
        return root.gameObject;
    }

    private GameObject BuildBotSetup()
    {
        var root = ScreenRoot("BotSetup", UiKit.Background);
        Title(root, "Игра с ботом", 64, new Vector2(0, -70));

        var col = UiKit.CreateColumn("Content", root, 14);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 10));

        Label(col, "Сложность");
        var row = UiKit.CreateRow("Difficulty", col, 14);
        string[] diff = { "Новичок", "Любитель", "Сильный", "Мастер" };
        for (int i = 0; i < diff.Length; i++)
        {
            var level = (BotDifficulty)i;
            mDifficultyButtons.Add(UiKit.CreatePlateButton(row, diff[i], new Vector2(230, 66), () =>
            {
                AppSettings.Difficulty = level;
                RefreshSetup();
            }));
        }

        Label(col, "Твой цвет");
        row = UiKit.CreateRow("Color", col, 14);
        string[] colors = { "Белые", "Чёрные", "Случайно" };
        for (int i = 0; i < colors.Length; i++)
        {
            int c = i;
            mColorButtons.Add(UiKit.CreatePlateButton(row, colors[i], new Vector2(230, 66), () =>
            {
                AppSettings.PlayerColor = c;
                RefreshSetup();
            }));
        }

        Label(col, "Режим");
        mBotModeHint = ModeRow(col, mBotModeButtons);

        BottomBar(root, () => Show(Screen.Main), "continue", StartBotGame);
        return root.gameObject;
    }

    private GameObject BuildLocalSetup()
    {
        var root = ScreenRoot("LocalSetup", UiKit.Background);
        Title(root, "Игра с игроками", 64, new Vector2(0, -70));

        var col = UiKit.CreateColumn("Content", root, 18);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

        var info = UiKit.CreateText("Info", col,
            "Пока — вдвоём за одним компьютером, ходите по очереди.\nОнлайн и игра с друзьями в Steam появятся позже.",
            28, UiKit.TextLight);
        ((RectTransform)info.transform).sizeDelta = new Vector2(1000, 90);

        Label(col, "Режим");
        mLocalModeHint = ModeRow(col, mLocalModeButtons);

        BottomBar(root, () => Show(Screen.Main), "continue", StartLocalGame);
        return root.gameObject;
    }

    private GameObject BuildOptions()
    {
        var root = ScreenRoot("Options", UiKit.Background);
        Title(root, "Опции", 64, new Vector2(0, -70));

        var col = UiKit.CreateColumn("Content", root, 16);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

        Label(col, "Способности загоревшихся фигур (для новых партий)");
        var size = new Vector2(680, 66);
        mTrojanButton = UiKit.CreatePlateButton(col, "", size, () =>
        {
            AppSettings.TrojanKnight = !AppSettings.TrojanKnight;
            RefreshOptions();
        }, 30);
        mQueenKnightButton = UiKit.CreatePlateButton(col, "", size, () =>
        {
            AppSettings.QueenMovesAsKnight = !AppSettings.QueenMovesAsKnight;
            RefreshOptions();
        }, 30);
        mPierceButton = UiKit.CreatePlateButton(col, "", size, () =>
        {
            AppSettings.EnablePierce = !AppSettings.EnablePierce;
            RefreshOptions();
        }, 30);
        mPierceOwnButton = UiKit.CreatePlateButton(col, "", size, () =>
        {
            AppSettings.PierceThroughOwnPieces = !AppSettings.PierceThroughOwnPieces;
            RefreshOptions();
        }, 30);

        Label(col, "Звук и музыка");
        var row = UiKit.CreateRow("Audio", col, 30);
        mSoundButtons.Add(UiKit.CreateImageButton(row, "sound", 90, ToggleSound));
        mMusicButtons.Add(UiKit.CreateImageButton(row, "music", 90, ToggleMusic));

        BottomBar(root, CloseOptions, "save", CloseOptions);
        return root.gameObject;
    }

    private GameObject BuildPause()
    {
        var root = ScreenRoot("Pause", new Color(0.05f, 0.06f, 0.05f, 0.88f));
        Title(root, "Пауза", 72, new Vector2(0, -110));

        var col = UiKit.CreateColumn("Buttons", root, 22);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -40));
        UiKit.CreateImageButton(col, "continue", BigButtonHeight, ClosePause);
        UiKit.CreatePlateButton(col, "Заново", new Vector2(361, BigButtonHeight), () =>
        {
            mGame.Restart();
            Show(Screen.None);
        }, 38);
        UiKit.CreateImageButton(col, "options", BigButtonHeight, () => OpenOptions(Screen.Pause));
        UiKit.CreateImageButton(col, "menu", BigButtonHeight, ToMainMenu);
        return root.gameObject;
    }

    private GameObject BuildGameOver()
    {
        var root = ScreenRoot("GameOver", new Color(0.05f, 0.06f, 0.05f, 0.8f));
        mGameOverText = Title(root, "", 56, new Vector2(0, -220));

        var col = UiKit.CreateColumn("Buttons", root, 22);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -40));
        UiKit.CreatePlateButton(col, "Ещё раз", new Vector2(361, BigButtonHeight), () =>
        {
            mGame.Restart();
            Show(Screen.None);
        }, 38);
        UiKit.CreateImageButton(col, "menu", BigButtonHeight, ToMainMenu);
        // Можно закрыть окно и посмотреть на доску.
        UiKit.CreatePlateButton(col, "Посмотреть доску", new Vector2(361, 64), () => Show(Screen.None), 26);
        return root.gameObject;
    }

    #endregion

    #region Обновление состояния кнопок

    private void RefreshSetup()
    {
        for (int i = 0; i < mDifficultyButtons.Count; i++)
            mDifficultyButtons[i].SetSelected(i == (int)AppSettings.Difficulty);
        for (int i = 0; i < mColorButtons.Count; i++)
            mColorButtons[i].SetSelected(i == AppSettings.PlayerColor);

        RefreshModeRow(mBotModeButtons, mBotModeHint);
        RefreshModeRow(mLocalModeButtons, mLocalModeHint);
    }

    private static void RefreshModeRow(List<MenuButton> buttons, Text hint)
    {
        int mode = AppSettings.Mode;
        for (int i = 0; i < buttons.Count; i++)
            buttons[i].SetSelected(i == mode);
        if (hint != null)
            hint.text = mode == 0
                ? "Шахматы с деградацией. После мата король уходит, а 1–3 фигуры его армии загораются и получают силу.\nАрмия без короля побеждает матом второму королю и проигрывает, когда фигур не осталось."
                : "Все способности у всех фигур сразу. Король слабеет как все, шаха нет.\nПобеда — когда у соперника не осталось ни одной фигуры.";
    }

    private void RefreshOptions()
    {
        mQueenKnightButton.SetText("Ферзь ходит и как конь: " + YesNo(AppSettings.QueenMovesAsKnight));
        mPierceButton.SetText("Пробивание слоном и ладьёй: " + YesNo(AppSettings.EnablePierce));
        mPierceOwnButton.SetText("Пробивать сквозь свои фигуры: " + YesNo(AppSettings.PierceThroughOwnPieces));
        mTrojanButton.SetText("Троянский конь: " + YesNo(AppSettings.TrojanKnight));
        RefreshAudioIcons();
    }

    private void ToggleSound()
    {
        AppSettings.Sound = !AppSettings.Sound;
        RefreshAudioIcons();
    }

    private void ToggleMusic()
    {
        AppSettings.Music = !AppSettings.Music;
        RefreshAudioIcons();
    }

    private void RefreshAudioIcons()
    {
        foreach (var b in mSoundButtons)
            b.SetSprite(UiKit.LoadSprite(AppSettings.Sound ? "UI/sound" : "UI/sound_off"));
        foreach (var b in mMusicButtons)
            b.SetSprite(UiKit.LoadSprite(AppSettings.Music ? "UI/music" : "UI/music_off"));
    }

    private static string YesNo(bool v) => v ? "да" : "нет";

    #endregion

    #region Мелкие помощники

    private RectTransform ScreenRoot(string name, Color background)
    {
        var img = UiKit.CreatePanel(name, mCanvas.transform, background);
        img.raycastTarget = true; // перекрывает доску — клики не проходят насквозь
        return (RectTransform)img.transform;
    }

    private static Text Title(Transform parent, string text, int size, Vector2 offsetFromTop)
    {
        var t = UiKit.CreateText("Title", parent, text, size, UiKit.TextLight);
        t.fontStyle = FontStyle.Bold;
        var outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = UiKit.TextDark;
        outline.effectDistance = new Vector2(3, -3);
        Place(t.transform, new Vector2(0.5f, 1), offsetFromTop);
        return t;
    }

    private static void Label(Transform parent, string text)
    {
        var t = UiKit.CreateText("Label " + text, parent, text, 30, new Color(0.85f, 0.9f, 0.85f, 1f));
        ((RectTransform)t.transform).sizeDelta = new Vector2(600, 44);
    }

    private Text ModeRow(Transform parent, List<MenuButton> into)
    {
        var row = UiKit.CreateRow("Mode", parent, 14);
        into.Add(UiKit.CreatePlateButton(row, "Шахматы 2.0", new Vector2(300, 66), () =>
        {
            AppSettings.Mode = 0;
            RefreshSetup();
        }));
        into.Add(UiKit.CreatePlateButton(row, "Хаос", new Vector2(300, 66), () =>
        {
            AppSettings.Mode = 1;
            RefreshSetup();
        }));

        var hint = UiKit.CreateText("ModeHint", parent, "", 22, new Color(0.75f, 0.8f, 0.75f, 1f));
        ((RectTransform)hint.transform).sizeDelta = new Vector2(1100, 60);
        return hint;
    }

    /// <summary>Внизу: стрелка «назад» слева и главная кнопка справа.</summary>
    private static void BottomBar(Transform parent, Action back, string mainSprite, Action main)
    {
        var backBtn = UiKit.CreateImageButton(parent, "arrow", 84, back);
        var brt = (RectTransform)backBtn.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0, 0);
        brt.pivot = new Vector2(0.5f, 0); // разворот вокруг центра, иначе стрелка уедет за край
        brt.anchoredPosition = new Vector2(60 + brt.sizeDelta.x / 2, 50);
        brt.localRotation = Quaternion.Euler(0, 180, 0); // стрелка из набора смотрит вправо — разворачиваем

        var mainBtn = UiKit.CreateImageButton(parent, mainSprite, 84, main);
        var mrt = (RectTransform)mainBtn.transform;
        mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(1, 0);
        mrt.anchoredPosition = new Vector2(-60, 50);
    }

    private void SoundToggles(Transform parent)
    {
        var row = UiKit.CreateRow("AudioToggles", parent, 16);
        Place(row, new Vector2(1, 1), new Vector2(-110, -60));
        mSoundButtons.Add(UiKit.CreateImageButton(row, "sound", 72, ToggleSound));
        mMusicButtons.Add(UiKit.CreateImageButton(row, "music", 72, ToggleMusic));
        RefreshAudioIcons();
    }

    private static void Decor(Transform parent, string sprite, Vector2 offset, float height, float angle, float alpha)
    {
        var rt = UiKit.CreateUi("Decor", parent, typeof(Image));
        var img = rt.GetComponent<Image>();
        img.sprite = UiKit.LoadSprite(sprite);
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = new Color(1, 1, 1, alpha);
        rt.sizeDelta = new Vector2(height, height);
        rt.anchoredPosition = offset;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private static void Place(Transform t, Vector2 anchor, Vector2 offset)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y == 1 ? 1 : anchor.y == 0 ? 0 : 0.5f);
        if (anchor.x == 1) rt.pivot = new Vector2(1, rt.pivot.y);
        rt.anchoredPosition = offset;
    }

    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    #endregion
}
