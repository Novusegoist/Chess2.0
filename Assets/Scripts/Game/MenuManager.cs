using System;
using System.Collections;
using System.Collections.Generic;
using Chess2.Core;
using Chess2.Core.Net;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Меню игры: главный экран, настройка партии с ботом, вдвоём и по сети (IP), опции, пауза, конец партии.
/// Всё строится из кода по картинкам из Resources/UI.
///
/// При запуске любой сцены без GameManager создаётся объект «Chess2» с GameManager и MenuManager
/// (см. Bootstrap) — сцену настраивать не нужно.
/// </summary>
[RequireComponent(typeof(GameManager))]
public class MenuManager : MonoBehaviour
{
    private enum Screen { None, Main, BotSetup, Players, LocalSetup, HostSetup, Join, Waiting, Options, Pause, GameOver }

    private const float BigButtonHeight = 84f;

    private GameManager mGame;
    private Canvas mCanvas;
    private readonly Dictionary<Screen, GameObject> mScreens = new Dictionary<Screen, GameObject>();
    private Screen mCurrent = Screen.None;
    private Screen mOptionsReturn = Screen.Main;
    private GameObject mHud;
    private Text mGameOverText;
    private Text mGameOverInfo;
    private MenuButton mAgainButton;
    private MenuButton mRestartButton;
    private MenuButton mResignButton;

    // Сеть: подключение, пока партия ещё не началась (дальше соединением владеет GameManager).
    private NetMatch mPendingNet;
    private Text mWaitTitle;
    private Text mWaitText;
    private Text mWaitStatus;
    private InputField mAddressField;
    private Text mJoinError;
    private readonly List<MenuButton> mHostColorButtons = new List<MenuButton>();
    private readonly List<MenuButton> mHostTimeButtons = new List<MenuButton>();
    private readonly List<MenuButton> mHostModeButtons = new List<MenuButton>();
    private Text mHostModeHint;

    // Группы «выбери один».
    private readonly List<MenuButton> mDifficultyButtons = new List<MenuButton>();
    private readonly List<MenuButton> mColorButtons = new List<MenuButton>();
    private readonly List<MenuButton> mBotModeButtons = new List<MenuButton>();
    private readonly List<MenuButton> mLocalModeButtons = new List<MenuButton>();
    private readonly List<MenuButton> mBotTimeButtons = new List<MenuButton>();
    private readonly List<MenuButton> mLocalTimeButtons = new List<MenuButton>();
    private Text mBotModeHint;
    private Text mLocalModeHint;

    // Опции.
    private readonly OptionsScreen mOptions = new OptionsScreen();
    private readonly List<MenuButton> mSoundButtons = new List<MenuButton>();
    private readonly List<MenuButton> mMusicButtons = new List<MenuButton>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var game = FindAnyObjectByType<GameManager>();
        if (game == null)
            game = new GameObject("Chess2").AddComponent<GameManager>();
        if (game.GetComponent<AppPerformance>() == null)
            game.gameObject.AddComponent<AppPerformance>();
        if (game.GetComponent<SoundManager>() == null)
            game.gameObject.AddComponent<SoundManager>();
        if (game.GetComponent<MenuManager>() == null)
            game.gameObject.AddComponent<MenuManager>();
    }

    void Awake()
    {
        mGame = GetComponent<GameManager>();
        mGame.GameEnded += OnGameEnded;
        mGame.RematchOffered += OnRematchOffered;
        mGame.NetGameStarted += OnNetGameStarted;
        mGame.NetClosed += OnNetClosed;
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
        {
            mGame.GameEnded -= OnGameEnded;
            mGame.RematchOffered -= OnRematchOffered;
            mGame.NetGameStarted -= OnNetGameStarted;
            mGame.NetClosed -= OnNetClosed;
        }
        mPendingNet?.Close();
    }

    void Update()
    {
        PollPendingNet();

        if (!EscapePressed())
            return;

        switch (mCurrent)
        {
            case Screen.None: OpenPause(); break;
            case Screen.Pause: ClosePause(); break;
            case Screen.Options: CloseOptions(); break;
            case Screen.BotSetup:
            case Screen.Players: Show(Screen.Main); break;
            case Screen.LocalSetup:
            case Screen.HostSetup:
            case Screen.Join: Show(Screen.Players); break;
            case Screen.Waiting: CancelNet(); break;
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

        if (screen == Screen.BotSetup || screen == Screen.LocalSetup || screen == Screen.HostSetup)
            RefreshSetup();
        if (screen == Screen.Pause)
        {
            // В сетевой партии «заново» нельзя — только сдаться или реванш после конца.
            mRestartButton.gameObject.SetActive(mGame.Net == null);
            mResignButton.gameObject.SetActive(mGame.State != null && !mGame.State.IsOver);
        }
        if (screen == Screen.Options)
            mOptions.Refresh();
        if (screen == Screen.Main)
            RefreshAudioIcons(); // звук могли переключить в опциях
    }

    private void StartBotGame()
    {
        int color = AppSettings.PlayerColor;
        if (color == 2)
            color = UnityEngine.Random.Range(0, 2);
        Side botSide = color == 0 ? Side.Black : Side.White;

        AppSettings.Save();
        mGame.StartGame(true, botSide, AppSettings.Difficulty, AppSettings.BuildRules(), AppSettings.TimeMinutes);
        Show(Screen.None);
    }

    private void StartLocalGame()
    {
        AppSettings.Save();
        mGame.StartGame(false, Side.Black, AppSettings.Difficulty, AppSettings.BuildRules(), AppSettings.TimeMinutes);
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
        bool net = mGame.Net != null && mGame.Net.Transport.Status == TransportStatus.Connected;
        mAgainButton.SetText(mGame.Net != null ? "Реванш" : "Ещё раз");
        mAgainButton.gameObject.SetActive(mGame.Net == null || net);
        mGameOverInfo.text = mGame.Net != null && mGame.Net.RemoteWantsRematch ? "Соперник предлагает реванш!" : "";
        StartCoroutine(ShowGameOverLater());
    }

    private void PlayAgain()
    {
        if (mGame.Net != null)
        {
            mGame.RequestRematch();
            mGameOverInfo.text = "Ждём ответа соперника…";
            return;
        }
        mGame.Restart();
        Show(Screen.None);
    }

    private void OnRematchOffered()
    {
        if (mGameOverInfo != null)
            mGameOverInfo.text = "Соперник предлагает реванш!";
    }

    private void OnNetGameStarted()
    {
        if (mCurrent == Screen.GameOver || mCurrent == Screen.Waiting)
            Show(Screen.None);
    }

    private void OnNetClosed(string reason)
    {
        if (mGameOverInfo == null)
            return;
        mGameOverInfo.text = reason;
        mAgainButton.gameObject.SetActive(false);
    }

    #endregion

    #region Сеть

    private void StartHosting()
    {
        AppSettings.Save();
        var template = new NetSetup
        {
            HostColorChoice = AppSettings.PlayerColor,
            Mode = AppSettings.Mode,
            TimeMinutes = AppSettings.TimeMinutes,
            QueenMovesAsKnight = AppSettings.QueenMovesAsKnight,
            EnablePierce = AppSettings.EnablePierce,
            PierceThroughOwnPieces = AppSettings.PierceThroughOwnPieces,
            TrojanKnight = AppSettings.TrojanKnight
        };
        mPendingNet?.Close();
        mPendingNet = NetMatch.Host(TcpTransport.DefaultPort, template);

        var addresses = NetInfo.LocalAddresses();
        mWaitTitle.text = "Ждём соперника…";
        mWaitText.text = "Скажи сопернику один из адресов и порт " + TcpTransport.DefaultPort + ":\n\n"
                         + (addresses.Count > 0 ? string.Join("\n", addresses) : "(не удалось узнать адреса)")
                         + "\n\nНа этом же компьютере — 127.0.0.1. Через интернет нужен Radmin VPN\nили открытый порт на роутере.";
        mWaitStatus.text = "";
        Show(Screen.Waiting);
    }

    private void StartJoining()
    {
        mJoinError.text = "";
        if (!NetInfo.TryParseEndpoint(mAddressField.text, out string host, out int port))
        {
            mJoinError.text = "Введи адрес, например 192.168.1.5 или 26.12.34.56:7777";
            return;
        }
        AppSettings.LastAddress = mAddressField.text.Trim();
        AppSettings.Save();

        mPendingNet?.Close();
        mPendingNet = NetMatch.Join(host, port);
        mWaitTitle.text = "Подключаемся…";
        mWaitText.text = $"Адрес: {host}, порт {port}";
        mWaitStatus.text = "";
        Show(Screen.Waiting);
    }

    private void PollPendingNet()
    {
        if (mPendingNet == null)
            return;

        if (mPendingNet.Transport.Status == TransportStatus.Connected && mWaitStatus != null)
            mWaitStatus.text = "Соединение есть, договариваемся о партии…";

        foreach (NetEvent e in mPendingNet.Poll())
        {
            if (e.Kind == NetEventKind.GameStarted)
            {
                NetMatch net = mPendingNet;
                mPendingNet = null; // дальше соединением владеет GameManager
                mGame.StartNetGame(net);
                Show(Screen.None);
                return;
            }
            if (e.Kind == NetEventKind.Disconnected)
            {
                mWaitStatus.text = string.IsNullOrEmpty(e.Text) ? "Соединение закрыто" : e.Text;
                mPendingNet.Close();
                mPendingNet = null;
                return;
            }
        }
    }

    private void CancelNet()
    {
        mPendingNet?.Close();
        mPendingNet = null;
        Show(Screen.Players);
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
        mScreens[Screen.Players] = BuildPlayers();
        mScreens[Screen.LocalSetup] = BuildLocalSetup();
        mScreens[Screen.HostSetup] = BuildHostSetup();
        mScreens[Screen.Join] = BuildJoin();
        mScreens[Screen.Waiting] = BuildWaiting();
        mScreens[Screen.Options] = mOptions.Build(mCanvas.transform, CloseOptions,
            (parent, text, size, offset) => Title(parent, text, size, offset), BottomBar);
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
        UiKit.CreateImageButton(col, "play_players", BigButtonHeight, () => Show(Screen.Players));
        UiKit.CreateImageButton(col, "options", BigButtonHeight, () => OpenOptions(Screen.Main));
        UiKit.CreateImageButton(col, "exit", BigButtonHeight, Quit);

        SoundToggles(root);

        // Авторство сторонних материалов (лицензия CC BY-SA требует указать его в игре).
        var credits = UiKit.CreateText("Credits", root, "Эмодзи: OpenMoji — openmoji.org, лицензия CC BY-SA 4.0",
            18, new Color(0.6f, 0.65f, 0.6f, 1f), TextAnchor.LowerLeft, shadow: false);
        var crt = (RectTransform)credits.transform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0, 0);
        crt.anchoredPosition = new Vector2(16, 10);
        crt.sizeDelta = new Vector2(700, 26);
        return root.gameObject;
    }

    private GameObject BuildBotSetup()
    {
        var root = ScreenRoot("BotSetup", UiKit.Background);
        Title(root, "Игра с ботом", 64, new Vector2(0, -50));

        var col = UiKit.CreateColumn("Content", root, 8);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

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

        Label(col, "Время на партию");
        TimeRow(col, mBotTimeButtons);

        Label(col, "Режим");
        mBotModeHint = ModeRow(col, mBotModeButtons);

        BottomBar(root, () => Show(Screen.Main), "continue", StartBotGame);
        return root.gameObject;
    }

    /// <summary>«Игра с игроками»: где играем — за одним компьютером или по сети.</summary>
    private GameObject BuildPlayers()
    {
        var root = ScreenRoot("Players", UiKit.Background);
        Title(root, "Игра с игроками", 64, new Vector2(0, -70));

        var col = UiKit.CreateColumn("Content", root, 22);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 10));
        var size = new Vector2(560, BigButtonHeight);
        UiKit.CreatePlateButton(col, "За одним компьютером", size, () => Show(Screen.LocalSetup), 34);
        UiKit.CreatePlateButton(col, "Создать игру по сети", size, () => Show(Screen.HostSetup), 34);
        UiKit.CreatePlateButton(col, "Подключиться по IP", size, () =>
        {
            mAddressField.text = AppSettings.LastAddress;
            mJoinError.text = "";
            Show(Screen.Join);
        }, 34);

        var info = UiKit.CreateText("Info", col,
            "По сети — в локальной сети, через Radmin VPN или по интернету.\nИгра с друзьями через Steam появится позже.",
            24, new Color(0.75f, 0.8f, 0.75f, 1f));
        ((RectTransform)info.transform).sizeDelta = new Vector2(1000, 80);

        BottomBar(root, () => Show(Screen.Main), null, null);
        return root.gameObject;
    }

    /// <summary>Хост выбирает цвет, время и режим, потом ждёт соперника.</summary>
    private GameObject BuildHostSetup()
    {
        var root = ScreenRoot("HostSetup", UiKit.Background);
        Title(root, "Создать игру по сети", 64, new Vector2(0, -60));

        var col = UiKit.CreateColumn("Content", root, 10);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

        Label(col, "Твой цвет");
        var row = UiKit.CreateRow("Color", col, 14);
        string[] colors = { "Белые", "Чёрные", "Случайно" };
        for (int i = 0; i < colors.Length; i++)
        {
            int c = i;
            mHostColorButtons.Add(UiKit.CreatePlateButton(row, colors[i], new Vector2(230, 66), () =>
            {
                AppSettings.PlayerColor = c;
                RefreshSetup();
            }));
        }

        Label(col, "Время на партию");
        TimeRow(col, mHostTimeButtons);

        Label(col, "Режим");
        mHostModeHint = ModeRow(col, mHostModeButtons);

        BottomBar(root, () => Show(Screen.Players), "continue", StartHosting);
        return root.gameObject;
    }

    /// <summary>Гость вводит адрес хоста.</summary>
    private GameObject BuildJoin()
    {
        var root = ScreenRoot("Join", UiKit.Background);
        Title(root, "Подключиться по IP", 64, new Vector2(0, -70));

        var col = UiKit.CreateColumn("Content", root, 16);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

        Label(col, "Адрес соперника");
        mAddressField = UiKit.CreateInputField(col, new Vector2(620, 72), "например 192.168.1.5");

        var hint = UiKit.CreateText("Hint", col,
            "Адрес покажет соперник на экране «Создать игру по сети».\nПорт по умолчанию " + TcpTransport.DefaultPort
            + ", другой можно указать через двоеточие: 26.12.34.56:7777",
            22, new Color(0.75f, 0.8f, 0.75f, 1f));
        ((RectTransform)hint.transform).sizeDelta = new Vector2(1000, 70);

        mJoinError = UiKit.CreateText("Error", col, "", 24, new Color32(255, 120, 110, 255));
        ((RectTransform)mJoinError.transform).sizeDelta = new Vector2(1000, 34);

        BottomBar(root, () => Show(Screen.Players), "continue", StartJoining);
        return root.gameObject;
    }

    /// <summary>Ожидание соперника (хост) или подключения (гость).</summary>
    private GameObject BuildWaiting()
    {
        var root = ScreenRoot("Waiting", UiKit.Background);
        mWaitTitle = Title(root, "", 64, new Vector2(0, -90));

        var col = UiKit.CreateColumn("Content", root, 24);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 0));

        mWaitText = UiKit.CreateText("Info", col, "", 30, UiKit.TextLight);
        ((RectTransform)mWaitText.transform).sizeDelta = new Vector2(1100, 330);

        mWaitStatus = UiKit.CreateText("Status", col, "", 26, new Color32(255, 200, 120, 255));
        ((RectTransform)mWaitStatus.transform).sizeDelta = new Vector2(1100, 40);

        BottomBar(root, CancelNet, null, null);
        return root.gameObject;
    }

    private GameObject BuildLocalSetup()
    {
        var root = ScreenRoot("LocalSetup", UiKit.Background);
        Title(root, "За одним компьютером", 64, new Vector2(0, -70));

        var col = UiKit.CreateColumn("Content", root, 18);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, 20));

        var info = UiKit.CreateText("Info", col, "Вдвоём за одним компьютером, ходите по очереди.", 28, UiKit.TextLight);
        ((RectTransform)info.transform).sizeDelta = new Vector2(1000, 50);

        Label(col, "Время на партию");
        TimeRow(col, mLocalTimeButtons);

        Label(col, "Режим");
        mLocalModeHint = ModeRow(col, mLocalModeButtons);

        BottomBar(root, () => Show(Screen.Players), "continue", StartLocalGame);
        return root.gameObject;
    }

    private GameObject BuildPause()
    {
        var root = ScreenRoot("Pause", new Color(0.05f, 0.06f, 0.05f, 0.88f));
        Title(root, "Пауза", 72, new Vector2(0, -110));

        var col = UiKit.CreateColumn("Buttons", root, 22);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -40));
        UiKit.CreateImageButton(col, "continue", BigButtonHeight, ClosePause);
        mRestartButton = UiKit.CreatePlateButton(col, "Заново", new Vector2(361, BigButtonHeight), () =>
        {
            mGame.Restart();
            Show(Screen.None);
        }, 38);
        mResignButton = UiKit.CreatePlateButton(col, "Сдаться", new Vector2(361, BigButtonHeight), () =>
        {
            Show(Screen.None);
            mGame.Resign();
        }, 38);
        UiKit.CreateImageButton(col, "options", BigButtonHeight, () => OpenOptions(Screen.Pause));
        UiKit.CreateImageButton(col, "menu", BigButtonHeight, ToMainMenu);
        return root.gameObject;
    }

    private GameObject BuildGameOver()
    {
        var root = ScreenRoot("GameOver", new Color(0.05f, 0.06f, 0.05f, 0.8f));
        mGameOverText = Title(root, "", 56, new Vector2(0, -200));
        mGameOverInfo = UiKit.CreateText("Info", root, "", 28, new Color32(255, 200, 120, 255));
        Place(mGameOverInfo.transform, new Vector2(0.5f, 1), new Vector2(0, -290));

        var col = UiKit.CreateColumn("Buttons", root, 22);
        Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -40));
        mAgainButton = UiKit.CreatePlateButton(col, "Ещё раз", new Vector2(361, BigButtonHeight), PlayAgain, 38);
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
        RefreshModeRow(mHostModeButtons, mHostModeHint);
        for (int i = 0; i < mHostColorButtons.Count; i++)
            mHostColorButtons[i].SetSelected(i == AppSettings.PlayerColor);

        int time = System.Array.IndexOf(AppSettings.TimeOptions, AppSettings.TimeMinutes);
        if (time < 0)
            time = 0;
        for (int i = 0; i < mBotTimeButtons.Count; i++)
            mBotTimeButtons[i].SetSelected(i == time);
        for (int i = 0; i < mLocalTimeButtons.Count; i++)
            mLocalTimeButtons[i].SetSelected(i == time);
        for (int i = 0; i < mHostTimeButtons.Count; i++)
            mHostTimeButtons[i].SetSelected(i == time);
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

    private void TimeRow(Transform parent, List<MenuButton> into)
    {
        var row = UiKit.CreateRow("Time", parent, 14);
        foreach (int minutes in AppSettings.TimeOptions)
        {
            int m = minutes;
            string label = m == 0 ? "Без времени" : m + " мин";
            into.Add(UiKit.CreatePlateButton(row, label, new Vector2(m == 0 ? 250 : 170, 60), () =>
            {
                AppSettings.TimeMinutes = m;
                RefreshSetup();
            }));
        }
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

        if (mainSprite == null)
            return; // экран только со стрелкой «назад»

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
