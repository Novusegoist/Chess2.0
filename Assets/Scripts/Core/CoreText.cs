using System;

namespace Chess2.Core
{
    /// <summary>
    /// Перевод текстов ядра, которые видит игрок (ошибки сети). Ядро не знает про язык игры:
    /// интерфейс подставляет сюда свой переводчик при запуске. По умолчанию текст не меняется.
    /// </summary>
    public static class CoreText
    {
        public static Func<string, string> T = s => s;
    }
}
