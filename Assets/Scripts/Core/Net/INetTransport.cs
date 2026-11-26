using System;

namespace Chess2.Core.Net
{
    /// <summary>
    /// Соединение двух игроков: «строка = сообщение», надёжная доставка по порядку.
    /// Реализации: TcpTransport (игра по IP) и Steam-лобби (в Unity-части игры).
    /// Все методы вызываются из основного потока; Status и Error можно читать в любой момент.
    /// </summary>
    public interface INetTransport : IDisposable
    {
        TransportStatus Status { get; }

        /// <summary>Причина закрытия соединения (если была ошибка).</summary>
        string Error { get; }

        /// <summary>Отправить одну строку. false — соединения нет.</summary>
        bool Send(string line);

        /// <summary>Забрать очередную пришедшую строку.</summary>
        bool TryReceive(out string line);

        void Close();
    }
}
