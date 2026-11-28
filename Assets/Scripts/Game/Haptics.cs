using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Вибрация на телефоне: короткий отклик на удар, взрыв, мат и возрождение.
/// Android — системный Vibrator (короткие импульсы с силой, Android 8+; на старых — по длительности).
/// iOS — Taptic Engine через Assets/Plugins/iOS/Haptics.mm. На ПК и в редакторе ничего не делает.
/// Выключается в опциях («Вибрация», видна только на телефоне).
/// </summary>
public static class Haptics
{
    public enum Kind { Light, Medium, Heavy, Victory }

    /// <summary>Отклик на ход: по самому сильному событию.</summary>
    public static void ForMove(List<Chess2.Core.MoveEffect> effects)
    {
        if (effects == null)
            return;
        Kind? strongest = null;
        foreach (Chess2.Core.MoveEffect e in effects)
        {
            Kind? k = e.Kind switch
            {
                Chess2.Core.EffectKind.KingFell => Kind.Victory,
                Chess2.Core.EffectKind.Exploded => Kind.Heavy,
                Chess2.Core.EffectKind.Promoted => Kind.Medium,
                Chess2.Core.EffectKind.Degraded => Kind.Light,
                Chess2.Core.EffectKind.Removed => Kind.Light,
                _ => (Kind?)null
            };
            if (k != null && (strongest == null || k > strongest))
                strongest = k;
        }
        if (strongest != null)
            Play(strongest.Value);
    }

    public static void Play(Kind kind)
    {
        if (!Platform.IsMobile || !AppSettings.Vibration)
            return;
#if UNITY_ANDROID && !UNITY_EDITOR
        switch (kind)
        {
            case Kind.Light: Android(25, 90); break;
            case Kind.Medium: Android(40, 160); break;
            case Kind.Heavy: Android(90, 255); break;
            case Kind.Victory: Android(220, 255); break;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        try
        {
            if (kind == Kind.Victory)
                AshHapticNotify(0); // «успех»: двойной отклик
            else
                AshHapticImpact((int)kind);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Вибрация недоступна: " + ex.Message);
        }
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void AshHapticImpact(int style);
    [DllImport("__Internal")] private static extern void AshHapticNotify(int type);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject sVibrator;
    private static int sSdk;
    private static bool sFailed;

    private static void Android(long ms, int amplitude)
    {
        if (sFailed)
            return;
        try
        {
            if (sVibrator == null)
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    sVibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    sSdk = version.GetStatic<int>("SDK_INT");
            }
            if (sVibrator == null || !sVibrator.Call<bool>("hasVibrator"))
            {
                sFailed = true;
                return;
            }
            if (sSdk >= 26)
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                    sVibrator.Call("vibrate", effect);
            }
            else
                sVibrator.Call("vibrate", ms);
        }
        catch (System.Exception ex)
        {
            sFailed = true;
            Debug.LogWarning("Вибрация недоступна: " + ex.Message);
            Handheld.Vibrate();
        }
    }
#endif

#if UNITY_ANDROID
    // Ссылка на Handheld.Vibrate нужна, чтобы Unity добавил в сборку разрешение VIBRATE.
    private static void KeepVibratePermission() => Handheld.Vibrate();
#endif
}
