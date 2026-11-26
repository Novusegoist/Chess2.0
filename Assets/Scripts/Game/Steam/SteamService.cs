using System;
using System.Collections.Generic;
using System.Text;
using Chess2.Core.Net;
using Steamworks;
using UnityEngine;

/// <summary>
/// Работа со Steam: запуск Steamworks, лобби «только для друзей» на двоих, приглашения через оверлей.
/// Сообщения партии ходят через чат лобби — их передаёт Steam, порты открывать не нужно.
///
/// Если Steam не запущен, Ready = false, а игра работает как обычно (по IP и с ботом).
/// AppID берётся из steam_appid.txt рядом с игрой (сейчас тестовый 480 — «Spacewar»).
/// </summary>
public class SteamService : MonoBehaviour
{
    public const string LobbyGameKey = "chess2";

    public static SteamService Instance { get; private set; }

    /// <summary>Steam запущен и Steamworks работает.</summary>
    public bool Ready { get; private set; }

    /// <summary>Почему Steam недоступен (для подсказки в меню).</summary>
    public string InitError { get; private set; }

    /// <summary>Друг пригласил нас, и мы приняли приглашение в Steam (номер лобби).</summary>
    public event Action<CSteamID> InviteAccepted;

    private Callback<GameLobbyJoinRequested_t> mJoinRequested;
    private Callback<LobbyChatUpdate_t> mChatUpdate;
    private Callback<LobbyChatMsg_t> mChatMsg;
    private CallResult<LobbyCreated_t> mLobbyCreated;
    private CallResult<LobbyEnter_t> mLobbyEntered;
    private SteamLobbyTransport mActive;
    private CSteamID mPendingInvite = CSteamID.Nil;
    private readonly byte[] mBuffer = new byte[4096];

    void Awake()
    {
        Instance = this;
        try
        {
            if (!Packsize.Test() || !DllCheck.Test())
                InitError = "Файлы Steamworks не подходят к этой сборке";
            else if (!SteamAPI.Init())
                InitError = "Steam не запущен (или игра запущена не через Steam)";
            else
                Ready = true;
        }
        catch (DllNotFoundException)
        {
            InitError = "Не найдена библиотека steam_api64.dll";
        }
        catch (Exception e)
        {
            InitError = "Steam недоступен: " + e.Message;
        }

        if (!Ready)
        {
            Debug.Log("Steam: " + InitError);
            return;
        }

        mJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        mChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
        mChatMsg = Callback<LobbyChatMsg_t>.Create(OnChatMessage);
        mLobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
        mLobbyEntered = CallResult<LobbyEnter_t>.Create(OnLobbyEntered);

        // Игру запустили из приглашения Steam: «+connect_lobby <номер>».
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong id))
                mPendingInvite = new CSteamID(id);
    }

    void Update()
    {
        if (Ready)
            SteamAPI.RunCallbacks();
    }

    void OnDestroy()
    {
        mActive?.Close();
        if (Ready)
        {
            SteamAPI.Shutdown();
            Ready = false;
        }
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Приглашение, с которым запустили игру (одноразово).</summary>
    public bool TryTakePendingInvite(out CSteamID lobby)
    {
        lobby = mPendingInvite;
        mPendingInvite = CSteamID.Nil;
        return lobby.IsValid();
    }

    public string MyName => Ready ? SteamFriends.GetPersonaName() : "";

    public string FriendName(CSteamID id) => Ready && id.IsValid() ? SteamFriends.GetFriendPersonaName(id) : "";

    #region Лобби

    /// <summary>Создать лобби и открыть окно приглашения друзей.</summary>
    public SteamLobbyTransport HostLobby()
    {
        var t = Attach(new SteamLobbyTransport(this, isHost: true));
        t.SetStatus(TransportStatus.Listening);
        mLobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2));
        return t;
    }

    /// <summary>Войти в лобби друга (принятое приглашение).</summary>
    public SteamLobbyTransport JoinLobby(CSteamID lobby)
    {
        var t = Attach(new SteamLobbyTransport(this, isHost: false));
        t.SetStatus(TransportStatus.Connecting);
        mLobbyEntered.Set(SteamMatchmaking.JoinLobby(lobby));
        return t;
    }

    /// <summary>Снова открыть окно приглашения (если его закрыли).</summary>
    public void OpenInviteDialog()
    {
        if (Ready && mActive != null && mActive.Lobby.IsValid())
            SteamFriends.ActivateGameOverlayInviteDialog(mActive.Lobby);
    }

    private SteamLobbyTransport Attach(SteamLobbyTransport t)
    {
        mActive?.Close();
        mActive = t;
        return t;
    }

    internal void Detach(SteamLobbyTransport t)
    {
        if (mActive == t)
        {
            mActive = null;
            if (Ready)
                SteamFriends.ClearRichPresence();
        }
    }

    private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
    {
        SteamLobbyTransport t = mActive;
        if (t == null || !t.IsHost)
            return;
        if (ioFailure || result.m_eResult != EResult.k_EResultOK)
        {
            t.Fail("Steam не смог создать лобби (" + result.m_eResult + ")");
            return;
        }

        t.Lobby = new CSteamID(result.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(t.Lobby, "game", LobbyGameKey);
        SteamMatchmaking.SetLobbyData(t.Lobby, "protocol", NetMatch.ProtocolVersion.ToString());
        // «Присоединиться к игре» в списке друзей Steam.
        SteamFriends.SetRichPresence("connect", "+connect_lobby " + t.Lobby.m_SteamID);
        SteamFriends.SetRichPresence("status", "Ждёт соперника · Пепел и корона");
        OpenInviteDialog();
    }

    private void OnLobbyEntered(LobbyEnter_t result, bool ioFailure)
    {
        SteamLobbyTransport t = mActive;
        if (t == null || t.IsHost)
            return;
        if (ioFailure || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            t.Fail("Не удалось войти в лобби друга — возможно, оно уже закрыто");
            return;
        }

        var lobby = new CSteamID(result.m_ulSteamIDLobby);
        if (SteamMatchmaking.GetLobbyData(lobby, "game") != LobbyGameKey)
        {
            SteamMatchmaking.LeaveLobby(lobby);
            t.Fail("Это лобби не от «Пепла и короны»");
            return;
        }

        t.Lobby = lobby;
        t.Opponent = SteamMatchmaking.GetLobbyOwner(lobby);
        if (SteamMatchmaking.GetNumLobbyMembers(lobby) < 2)
        {
            t.Fail("Друг уже вышел из лобби");
            return;
        }
        SteamFriends.SetRichPresence("status", "Играет в «Пепел и корону»");
        t.SetStatus(TransportStatus.Connected);
    }

    private void OnChatUpdate(LobbyChatUpdate_t update)
    {
        SteamLobbyTransport t = mActive;
        if (t == null || update.m_ulSteamIDLobby != t.Lobby.m_SteamID)
            return;
        var user = new CSteamID(update.m_ulSteamIDUserChanged);
        if (user == SteamUser.GetSteamID())
            return;

        uint change = update.m_rgfChatMemberStateChange;
        if ((change & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0 && t.IsHost)
        {
            t.Opponent = user;
            SteamFriends.SetRichPresence("status", "Играет в «Пепел и корону»");
            t.SetStatus(TransportStatus.Connected);
        }

        const uint gone = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft
                                 | EChatMemberStateChange.k_EChatMemberStateChangeDisconnected
                                 | EChatMemberStateChange.k_EChatMemberStateChangeKicked
                                 | EChatMemberStateChange.k_EChatMemberStateChangeBanned);
        if ((change & gone) != 0 && user == t.Opponent)
            t.Fail("Соперник вышел из игры");
    }

    private void OnChatMessage(LobbyChatMsg_t msg)
    {
        SteamLobbyTransport t = mActive;
        if (t == null || msg.m_ulSteamIDLobby != t.Lobby.m_SteamID)
            return;

        int length = SteamMatchmaking.GetLobbyChatEntry(t.Lobby, (int)msg.m_iChatID, out CSteamID user,
            mBuffer, mBuffer.Length, out EChatEntryType type);
        if (user == SteamUser.GetSteamID() || type != EChatEntryType.k_EChatEntryTypeChatMsg || length <= 0)
            return; // свои сообщения Steam тоже присылает обратно

        // Хост мог ещё не получить «соперник вошёл», а сообщение уже пришло.
        if (t.IsHost && t.Status == TransportStatus.Listening)
        {
            t.Opponent = user;
            t.SetStatus(TransportStatus.Connected);
        }
        t.Enqueue(Encoding.UTF8.GetString(mBuffer, 0, length).TrimEnd('\0'));
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t request)
    {
        if (InviteAccepted != null)
            InviteAccepted(request.m_steamIDLobby);
        else
            mPendingInvite = request.m_steamIDLobby;
    }

    #endregion
}

/// <summary>Соединение через чат Steam-лобби на двоих.</summary>
public class SteamLobbyTransport : INetTransport
{
    private readonly SteamService mService;
    private readonly Queue<string> mInbox = new Queue<string>();

    public bool IsHost { get; }
    public CSteamID Lobby { get; internal set; } = CSteamID.Nil;
    public CSteamID Opponent { get; internal set; } = CSteamID.Nil;
    public TransportStatus Status { get; private set; } = TransportStatus.Idle;
    public string Error { get; private set; }

    internal SteamLobbyTransport(SteamService service, bool isHost)
    {
        mService = service;
        IsHost = isHost;
    }

    public bool Send(string line)
    {
        if (Status != TransportStatus.Connected || !Lobby.IsValid())
            return false;
        byte[] data = Encoding.UTF8.GetBytes(line.Replace("\n", " "));
        return SteamMatchmaking.SendLobbyChatMsg(Lobby, data, data.Length);
    }

    public bool TryReceive(out string line)
    {
        if (mInbox.Count > 0)
        {
            line = mInbox.Dequeue();
            return true;
        }
        line = null;
        return false;
    }

    public void Close()
    {
        if (Status == TransportStatus.Closed)
            return;
        Status = TransportStatus.Closed;
        if (Lobby.IsValid())
            SteamMatchmaking.LeaveLobby(Lobby);
        mService.Detach(this);
    }

    public void Dispose() => Close();

    internal void Enqueue(string line) => mInbox.Enqueue(line);

    internal void SetStatus(TransportStatus status)
    {
        if (Status != TransportStatus.Closed)
            Status = status;
    }

    internal void Fail(string error)
    {
        if (Status == TransportStatus.Closed)
            return;
        Error = error;
        Close();
    }
}
