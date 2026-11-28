using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace Chess2.Core.Net
{
    public enum NetRole
    {
        /// <summary>Создал игру и выбирает настройки.</summary>
        Host,

        /// <summary>Подключился к чужой игре.</summary>
        Guest
    }

    /// <summary>Настройки сетевой партии. Их выбирает хост и присылает гостю.</summary>
    public sealed class NetSetup
    {
        public int Seed;

        /// <summary>Цвет хоста в этой партии.</summary>
        public Side HostSide;

        /// <summary>Выбор хоста в меню: 0 — белые, 1 — чёрные, 2 — случайно (только для шаблона).</summary>
        public int HostColorChoice;

        /// <summary>0 — «Корона», 1 — «Хаос».</summary>
        public int Mode;

        public int TimeMinutes;
        public bool QueenMovesAsKnight = true;
        public bool EnablePierce = true;
        public bool PierceThroughOwnPieces = true;
        public bool TrojanKnight = true;

        public RuleSet BuildRules() => new RuleSet
        {
            WinCondition = Mode == 1 ? WinCondition.Annihilation : WinCondition.KingFall,
            AbilitiesForAll = Mode == 1,
            QueenMovesAsKnight = QueenMovesAsKnight,
            EnablePierce = EnablePierce,
            PierceThroughOwnPieces = PierceThroughOwnPieces,
            TrojanKnight = TrojanKnight
        };

        public NetSetup Clone() => (NetSetup)MemberwiseClone();

        internal string Encode() => string.Join(" ",
            Seed.ToString(CultureInfo.InvariantCulture),
            HostSide == Side.White ? "W" : "B",
            Mode.ToString(CultureInfo.InvariantCulture),
            TimeMinutes.ToString(CultureInfo.InvariantCulture),
            QueenMovesAsKnight ? "1" : "0",
            EnablePierce ? "1" : "0",
            PierceThroughOwnPieces ? "1" : "0",
            TrojanKnight ? "1" : "0");

        internal static NetSetup Decode(string[] a, int start) => new NetSetup
        {
            Seed = int.Parse(a[start], CultureInfo.InvariantCulture),
            HostSide = a[start + 1] == "W" ? Side.White : Side.Black,
            Mode = int.Parse(a[start + 2], CultureInfo.InvariantCulture),
            TimeMinutes = int.Parse(a[start + 3], CultureInfo.InvariantCulture),
            QueenMovesAsKnight = a[start + 4] == "1",
            EnablePierce = a[start + 5] == "1",
            PierceThroughOwnPieces = a[start + 6] == "1",
            TrojanKnight = a[start + 7] == "1"
        };
    }

    public enum NetEventKind
    {
        /// <summary>Новая партия началась (первая или реванш). Setup — её настройки.</summary>
        GameStarted,

        /// <summary>Соперник сделал ход. ClockSeconds — его время после хода.</summary>
        RemoteMove,

        RemoteResigned,

        /// <summary>У соперника кончилось время.</summary>
        RemoteTimeout,

        /// <summary>Соперник хочет реванш.</summary>
        RematchRequested,

        /// <summary>Соединение закрыто. Text — причина (может быть пустой).</summary>
        Disconnected,

        /// <summary>Сообщение в чат. Text — текст.</summary>
        Chat,

        /// <summary>Эмодзи-реакция. Value — номер эмодзи.</summary>
        Emote
    }

    public struct NetEvent
    {
        public NetEventKind Kind;
        public NetSetup Setup;
        public Move Move;
        public double ClockSeconds;
        public string Text;
        public int Value;
    }

    /// <summary>
    /// Сетевая партия один на один поверх TcpTransport.
    /// Протокол — текстовые строки: «HELLO версия», «SETUP …», «MOVE откуда куда вид время», «RESIGN»,
    /// «TIMEOUT», «REMATCH», «CHAT текст», «EMOTE номер», «PING n» / «PONG n», «BYE».
    /// Каждая сторона сама проверяет чужие ходы по правилам.
    /// Пульс: раз в PingInterval шлём PING; если от соперника долго ничего нет — связь потеряна.
    /// Все события забираются в основном потоке через Poll().
    /// </summary>
    public sealed class NetMatch : IDisposable
    {
        /// <summary>2 — чат и эмодзи; 3 — пульс соединения (PING/PONG).</summary>
        public const int ProtocolVersion = 3;

        /// <summary>Максимальная длина сообщения в чате.</summary>
        public const int MaxChatLength = 120;

        public NetRole Role { get; }
        /// <summary>Соединение: TCP (игра по IP) или Steam-лобби.</summary>
        public INetTransport Transport { get; }

        /// <summary>Настройки текущей партии (у гостя — после первого SETUP).</summary>
        public NetSetup Setup { get; private set; }

        /// <summary>
        /// Свой рейтинг — отправляется сопернику при подключении (игра выставляет его перед подключением).
        /// Старые версии без рейтинга строку RATING просто пропускают — версия протокола та же.
        /// </summary>
        public static int LocalRating { get; set; } = Elo.Start;

        /// <summary>Рейтинг соперника; null — соперник его не прислал (старая версия игры).</summary>
        public int? RemoteRating { get; private set; }

        public bool InGame => Setup != null;
        public Side LocalSide => Role == NetRole.Host ? Setup.HostSide : Setup.HostSide.Opponent();
        public bool RemoteWantsRematch { get; private set; }
        public bool LocalWantsRematch { get; private set; }

        /// <summary>Как часто слать PING (секунды).</summary>
        public double PingInterval { get; set; } = 1.5;

        /// <summary>Сколько секунд тишины — «связь нестабильна».</summary>
        public double ShakyAfter { get; set; } = 4;

        /// <summary>Сколько секунд тишины — связь потеряна, соединение закрывается.</summary>
        public double LostAfter { get; set; } = 12;

        /// <summary>Последний измеренный пинг (туда и обратно), мс. null — ещё не измерен.</summary>
        public int? PingMs { get; private set; }

        /// <summary>Сколько секунд от соперника ничего не приходило.</summary>
        public double Silence => Transport.Status == TransportStatus.Connected ? Now - _lastReceived : 0;

        /// <summary>Связь есть, но соперник давно молчит.</summary>
        public bool Shaky => Transport.Status == TransportStatus.Connected && Silence > ShakyAfter;

        private readonly NetSetup _template;
        private readonly List<NetEvent> _pending = new List<NetEvent>();
        private readonly Random _random = new Random();
        private bool _helloSent;
        private bool _closedReported;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _lastReceived;
        private double _lastPingSent = double.MinValue;
        private int _pingId;
        private double _pingSentAt;

        private double Now => _clock.Elapsed.TotalSeconds;

        private NetMatch(NetRole role, NetSetup template, INetTransport transport)
        {
            Role = role;
            _template = template;
            Transport = transport;
        }

        /// <summary>
        /// Партия поверх готового соединения (например, Steam-лобби).
        /// Хосту нужен template — настройки из меню; гостю — null.
        /// </summary>
        public static NetMatch Create(NetRole role, INetTransport transport, NetSetup template = null) =>
            new NetMatch(role, template, transport);

        /// <summary>Создать игру и ждать соперника. template — настройки, выбранные в меню.</summary>
        public static NetMatch Host(int port, NetSetup template)
        {
            var tcp = new TcpTransport();
            tcp.Host(port);
            return new NetMatch(NetRole.Host, template, tcp);
        }

        /// <summary>Подключиться к игре по адресу.</summary>
        public static NetMatch Join(string host, int port)
        {
            var tcp = new TcpTransport();
            tcp.Connect(host, port);
            return new NetMatch(NetRole.Guest, null, tcp);
        }

        /// <summary>Забрать все новые события. Вызывать каждый кадр из основного потока.</summary>
        public List<NetEvent> Poll()
        {
            var events = new List<NetEvent>(_pending);
            _pending.Clear();

            if (Transport.Status == TransportStatus.Connected && !_helloSent)
            {
                _helloSent = true;
                _lastReceived = Now; // отсчёт тишины — с момента подключения
                Transport.Send("HELLO " + ProtocolVersion);
                Transport.Send("RATING " + LocalRating.ToString(CultureInfo.InvariantCulture));
            }

            while (Transport.TryReceive(out string line))
            {
                _lastReceived = Now;
                try
                {
                    Handle(line, events);
                }
                catch (Exception e)
                {
                    Disconnect(CoreText.T("Непонятное сообщение от соперника: ") + e.Message);
                }
            }

            if (Transport.Status == TransportStatus.Connected)
            {
                double now = Now;
                if (now - _lastPingSent >= PingInterval)
                {
                    _lastPingSent = now;
                    _pingSentAt = now;
                    _pingId++;
                    Transport.Send("PING " + _pingId.ToString(CultureInfo.InvariantCulture));
                }
                if (now - _lastReceived > LostAfter)
                    Disconnect(CoreText.T("Связь с соперником потеряна"));
                events.AddRange(_pending);
                _pending.Clear();
            }

            if (Transport.Status == TransportStatus.Closed && !_closedReported)
            {
                _closedReported = true;
                events.Add(new NetEvent { Kind = NetEventKind.Disconnected, Text = Transport.Error });
            }
            return events;
        }

        public void SendMove(Move m, double clockSeconds)
        {
            Transport.Send(string.Join(" ", "MOVE",
                m.From.ToString(CultureInfo.InvariantCulture),
                m.To.ToString(CultureInfo.InvariantCulture),
                ((int)m.Kind).ToString(CultureInfo.InvariantCulture),
                ((long)(clockSeconds * 1000)).ToString(CultureInfo.InvariantCulture)));
        }

        public void SendResign() => Transport.Send("RESIGN");

        /// <summary>Сообщение в чат (обрезается до MaxChatLength). Возвращает то, что реально отправлено.</summary>
        public string SendChat(string text)
        {
            text = CleanChat(text);
            if (text.Length > 0)
                Transport.Send("CHAT " + text);
            return text;
        }

        public void SendEmote(int id) => Transport.Send("EMOTE " + id.ToString(CultureInfo.InvariantCulture));

        /// <summary>Убрать переносы и управляющие символы, обрезать длину.</summary>
        public static string CleanChat(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
                sb.Append(char.IsControl(c) ? ' ' : c);
            string clean = sb.ToString().Trim();
            return clean.Length > MaxChatLength ? clean.Substring(0, MaxChatLength) : clean;
        }

        /// <summary>У нас кончилось время — сообщаем сопернику.</summary>
        public void SendTimeout() => Transport.Send("TIMEOUT");

        /// <summary>
        /// Предложить или принять реванш. Новую партию (с переменой цветов) начинает хост,
        /// когда реванш хотят оба.
        /// </summary>
        public void RequestRematch()
        {
            if (LocalWantsRematch)
                return;
            LocalWantsRematch = true;
            if (Role == NetRole.Host && RemoteWantsRematch)
                StartNewGame(swapColors: true);
            else
                Transport.Send("REMATCH");
        }

        /// <summary>Попрощаться и закрыть соединение.</summary>
        public void Close()
        {
            if (Transport.Status == TransportStatus.Connected)
                Transport.Send("BYE");
            Transport.Close();
            _closedReported = true; // сами закрыли — событие не нужно
        }

        public void Dispose() => Close();

        private void Handle(string line, List<NetEvent> events)
        {
            string[] a = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 0)
                return;

            switch (a[0])
            {
                case "HELLO":
                    if (int.Parse(a[1], CultureInfo.InvariantCulture) != ProtocolVersion)
                    {
                        Disconnect(CoreText.T("У соперника другая версия игры"));
                        return;
                    }
                    if (Role == NetRole.Host)
                    {
                        StartNewGame(swapColors: false);
                        events.AddRange(_pending);
                        _pending.Clear();
                    }
                    break;

                case "SETUP":
                    if (Role != NetRole.Guest)
                        return;
                    Setup = NetSetup.Decode(a, 1);
                    LocalWantsRematch = RemoteWantsRematch = false;
                    events.Add(new NetEvent { Kind = NetEventKind.GameStarted, Setup = Setup });
                    break;

                case "MOVE":
                    events.Add(new NetEvent
                    {
                        Kind = NetEventKind.RemoteMove,
                        Move = new Move(
                            int.Parse(a[1], CultureInfo.InvariantCulture),
                            int.Parse(a[2], CultureInfo.InvariantCulture),
                            (MoveKind)int.Parse(a[3], CultureInfo.InvariantCulture)),
                        ClockSeconds = long.Parse(a[4], CultureInfo.InvariantCulture) / 1000.0
                    });
                    break;

                case "RESIGN":
                    events.Add(new NetEvent { Kind = NetEventKind.RemoteResigned });
                    break;

                case "TIMEOUT":
                    events.Add(new NetEvent { Kind = NetEventKind.RemoteTimeout });
                    break;

                case "REMATCH":
                    RemoteWantsRematch = true;
                    if (Role == NetRole.Host && LocalWantsRematch)
                    {
                        StartNewGame(swapColors: true);
                        events.AddRange(_pending);
                        _pending.Clear();
                    }
                    else
                        events.Add(new NetEvent { Kind = NetEventKind.RematchRequested });
                    break;

                case "CHAT":
                    string text = CleanChat(line.Length > 5 ? line.Substring(5) : "");
                    if (text.Length > 0)
                        events.Add(new NetEvent { Kind = NetEventKind.Chat, Text = text });
                    break;

                case "EMOTE":
                    events.Add(new NetEvent { Kind = NetEventKind.Emote, Value = int.Parse(a[1], CultureInfo.InvariantCulture) });
                    break;

                case "PING":
                    Transport.Send("PONG " + a[1]);
                    break;

                case "PONG":
                    if (int.Parse(a[1], CultureInfo.InvariantCulture) == _pingId)
                        PingMs = (int)Math.Round((Now - _pingSentAt) * 1000);
                    break;

                case "BYE":
                    Transport.Close();
                    break;

                case "RATING":
                    RemoteRating = Math.Max(Elo.Floor, Math.Min(4000, int.Parse(a[1], CultureInfo.InvariantCulture)));
                    break;
            }
        }

        /// <summary>Хост: новая партия — новое зерно, при реванше цвета меняются местами.</summary>
        private void StartNewGame(bool swapColors)
        {
            NetSetup s = _template.Clone();
            s.Seed = _random.Next();
            if (swapColors && Setup != null)
                s.HostSide = Setup.HostSide.Opponent();
            else
                s.HostSide = s.HostColorChoice == 2
                    ? (_random.Next(2) == 0 ? Side.White : Side.Black)
                    : (s.HostColorChoice == 0 ? Side.White : Side.Black);

            Setup = s;
            LocalWantsRematch = RemoteWantsRematch = false;
            Transport.Send("SETUP " + s.Encode());
            _pending.Add(new NetEvent { Kind = NetEventKind.GameStarted, Setup = s });
        }

        private void Disconnect(string reason)
        {
            Transport.Close();
            if (!_closedReported)
            {
                _closedReported = true;
                _pending.Add(new NetEvent { Kind = NetEventKind.Disconnected, Text = reason });
            }
        }
    }
}
