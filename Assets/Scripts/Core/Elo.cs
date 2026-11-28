using System;

namespace Chess2.Core
{
    /// <summary>
    /// Рейтинг Эло. Считаются партии против бота (у каждого уровня свой постоянный рейтинг)
    /// и по сети (рейтинг соперника приходит по сети). Игра вдвоём за одним устройством не считается.
    /// </summary>
    public static class Elo
    {
        public const int Start = 1000;
        public const int Floor = 100;

        /// <summary>Рейтинг уровней бота: новичок, любитель, сильный, мастер.</summary>
        public static int BotRating(BotDifficulty d)
        {
            switch (d)
            {
                case BotDifficulty.Easy: return 600;
                case BotDifficulty.Medium: return 1000;
                case BotDifficulty.Hard: return 1400;
                default: return 1800;
            }
        }

        /// <summary>Ожидаемый результат (0…1) игрока с рейтингом a против b.</summary>
        public static double Expected(int a, int b) => 1.0 / (1.0 + Math.Pow(10, (b - a) / 400.0));

        /// <summary>Первые 20 партий рейтинг меняется быстрее, чтобы быстрее найти свой уровень.</summary>
        public static int K(int ratedGames) => ratedGames < 20 ? 40 : 24;

        /// <summary>
        /// На сколько изменится рейтинг. score: 1 — победа, 0.5 — ничья, 0 — поражение.
        /// Рейтинг не опускается ниже Floor.
        /// </summary>
        public static int Change(int rating, int opponent, double score, int ratedGames)
        {
            int delta = (int)Math.Round(K(ratedGames) * (score - Expected(rating, opponent)), MidpointRounding.AwayFromZero);
            return Math.Max(delta, Floor - rating);
        }
    }
}
