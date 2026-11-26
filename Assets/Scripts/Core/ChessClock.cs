using System;

namespace Chess2.Core
{
    /// <summary>
    /// Шахматные часы: у каждой стороны свой запас времени, тикает время того, чей ход.
    /// Часы стартуют после первого хода белых — чтобы партия не начиналась с потери времени.
    /// Без Unity: время передаётся снаружи (Tick), поэтому часы легко тестировать и синхронизировать по сети.
    /// </summary>
    public sealed class ChessClock
    {
        public double InitialSeconds { get; }
        public double WhiteSeconds { get; private set; }
        public double BlackSeconds { get; private set; }

        /// <summary>Часы идут (после первого хода и до конца партии).</summary>
        public bool Running { get; private set; }

        /// <summary>Партия без контроля времени.</summary>
        public bool Enabled => InitialSeconds > 0;

        public ChessClock(int minutes)
        {
            InitialSeconds = Math.Max(0, minutes) * 60.0;
            WhiteSeconds = BlackSeconds = InitialSeconds;
        }

        public double Remaining(Side side) => side == Side.White ? WhiteSeconds : BlackSeconds;

        /// <summary>Ход сделан — часы запускаются (если ещё не шли).</summary>
        public void OnMoveMade()
        {
            if (Enabled)
                Running = true;
        }

        /// <summary>Остановить часы насовсем (конец партии).</summary>
        public void Stop() => Running = false;

        /// <summary>
        /// Списать прошедшее время со стороны, которая сейчас думает.
        /// Возвращает true, если у неё кончилось время (флаг упал).
        /// </summary>
        public bool Tick(Side toMove, double elapsedSeconds)
        {
            if (!Enabled || !Running || elapsedSeconds <= 0)
                return false;

            if (toMove == Side.White)
                WhiteSeconds = Math.Max(0, WhiteSeconds - elapsedSeconds);
            else
                BlackSeconds = Math.Max(0, BlackSeconds - elapsedSeconds);

            if (Remaining(toMove) > 0)
                return false;
            Running = false;
            return true;
        }

        /// <summary>«10:00», а в последние 10 секунд — с десятыми: «0:07.3».</summary>
        public static string Format(double seconds)
        {
            if (seconds < 0)
                seconds = 0;
            if (seconds < 10)
            {
                int tenths = (int)Math.Floor(seconds * 10);
                return $"0:{tenths / 10:00}.{tenths % 10}";
            }
            int total = (int)Math.Ceiling(seconds);
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
