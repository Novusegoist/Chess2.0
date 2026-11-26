using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Chess2.Core.Net
{
    public enum TransportStatus
    {
        Idle,
        Listening,
        Connecting,
        Connected,
        Closed
    }

    /// <summary>
    /// Простое TCP-соединение «строка = сообщение» для игры по IP.
    /// Чтение идёт в фоновом потоке, входящие строки складываются в очередь —
    /// забирать их нужно из основного потока через TryReceive (Unity не любит работу из чужих потоков).
    /// </summary>
    public sealed class TcpTransport : IDisposable
    {
        public const int DefaultPort = 7777;

        private readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        private readonly object _sendLock = new object();
        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _stream;
        private volatile TransportStatus _status = TransportStatus.Idle;
        private volatile string _error;

        public TransportStatus Status => _status;

        /// <summary>Причина закрытия соединения (если была ошибка).</summary>
        public string Error => _error;

        /// <summary>Порт, который слушает хост (полезно, если запрошен порт 0 — «любой свободный»).</summary>
        public int ListenPort { get; private set; }

        /// <summary>Ждать подключения соперника на порту.</summary>
        public void Host(int port)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start(1);
                ListenPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _status = TransportStatus.Listening;
                _listener.BeginAcceptTcpClient(OnAccept, null);
            }
            catch (Exception e)
            {
                Fail("Не удалось открыть порт " + port + ": " + e.Message);
            }
        }

        /// <summary>Подключиться к хосту.</summary>
        public void Connect(string host, int port)
        {
            try
            {
                _status = TransportStatus.Connecting;
                _client = new TcpClient(AddressFamily.InterNetwork);
                _client.BeginConnect(host, port, OnConnect, null);
            }
            catch (Exception e)
            {
                Fail("Не удалось подключиться: " + e.Message);
            }
        }

        /// <summary>Отправить одну строку-сообщение.</summary>
        public bool Send(string line)
        {
            if (_status != TransportStatus.Connected)
                return false;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(line.Replace("\n", " ") + "\n");
                lock (_sendLock)
                    _stream.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception e)
            {
                Fail("Соединение потеряно: " + e.Message);
                return false;
            }
        }

        public bool TryReceive(out string line) => _incoming.TryDequeue(out line);

        public void Close()
        {
            if (_status != TransportStatus.Closed)
                _status = TransportStatus.Closed;
            try { _listener?.Stop(); } catch { /* уже закрыт */ }
            try { _client?.Close(); } catch { /* уже закрыт */ }
        }

        public void Dispose() => Close();

        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                _client = _listener.EndAcceptTcpClient(ar);
                _listener.Stop(); // играем один на один — больше никого не ждём
                Attach();
            }
            catch (ObjectDisposedException)
            {
                // Хост отменил ожидание.
            }
            catch (Exception e)
            {
                Fail("Ошибка при подключении соперника: " + e.Message);
            }
        }

        private void OnConnect(IAsyncResult ar)
        {
            try
            {
                _client.EndConnect(ar);
                Attach();
            }
            catch (ObjectDisposedException)
            {
                // Подключение отменили.
            }
            catch (Exception e)
            {
                Fail("Не удалось подключиться: " + e.Message);
            }
        }

        private void Attach()
        {
            if (_status == TransportStatus.Closed)
            {
                _client?.Close();
                return;
            }
            _client.NoDelay = true;
            _stream = _client.GetStream();
            _status = TransportStatus.Connected;

            var reader = new Thread(ReadLoop) { IsBackground = true, Name = "Chess2 net reader" };
            reader.Start();
        }

        private void ReadLoop()
        {
            try
            {
                using (var reader = new StreamReader(_stream, new UTF8Encoding(false)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        _incoming.Enqueue(line);
                }
                Fail(null); // соперник закрыл соединение
            }
            catch (Exception e)
            {
                if (_status != TransportStatus.Closed)
                    Fail("Соединение потеряно: " + e.Message);
            }
        }

        private void Fail(string error)
        {
            if (_status == TransportStatus.Closed)
                return;
            _error = error;
            _status = TransportStatus.Closed;
            try { _listener?.Stop(); } catch { /* уже закрыт */ }
            try { _client?.Close(); } catch { /* уже закрыт */ }
        }
    }
}
