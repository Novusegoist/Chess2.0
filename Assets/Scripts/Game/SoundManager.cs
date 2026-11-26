using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Звуки и музыка.
/// - Звуки: Resources/Audio/Sfx/имя.ogg. Варианты одного звука — имя_1, имя_2…:
///   Play("move") выбирает случайный из move_1, move_2, move_3.
/// - Музыка: все клипы из Resources/Audio/Music, играют по кругу.
/// - Громкость и включение берутся из AppSettings (общая громкость — AudioListener, см. AppPerformance).
/// </summary>
public class SoundManager : MonoBehaviour
{
    private static SoundManager sInstance;

    private readonly Dictionary<string, List<AudioClip>> mClips = new Dictionary<string, List<AudioClip>>();
    private AudioSource mSfx;
    private AudioSource mMusic;
    private AudioClip[] mPlaylist;
    private int mTrack;
    private float mNextSettingsCheck;

    void Awake()
    {
        sInstance = this;

        // Без «ушей» в сцене ничего не слышно.
        if (FindAnyObjectByType<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

        mSfx = gameObject.AddComponent<AudioSource>();
        mSfx.playOnAwake = false;
        mMusic = gameObject.AddComponent<AudioSource>();
        mMusic.playOnAwake = false;
        mMusic.loop = false; // следующий трек включаем сами

        foreach (AudioClip clip in Resources.LoadAll<AudioClip>("Audio/Sfx"))
        {
            string key = BaseName(clip.name);
            if (!mClips.TryGetValue(key, out var list))
                mClips[key] = list = new List<AudioClip>();
            list.Add(clip);
        }

        mPlaylist = Resources.LoadAll<AudioClip>("Audio/Music");
        System.Array.Sort(mPlaylist, (a, b) => string.CompareOrdinal(a.name, b.name));
        mTrack = mPlaylist.Length > 0 ? Random.Range(0, mPlaylist.Length) : 0;
        ApplyVolumes();
    }

    void Update()
    {
        // Настройки громкости могли поменяться в опциях — проверяем несколько раз в секунду.
        if (Time.unscaledTime >= mNextSettingsCheck)
        {
            mNextSettingsCheck = Time.unscaledTime + 0.2f;
            ApplyVolumes();
        }

        // Плейлист: трек закончился — следующий.
        if (AppSettings.Music && mPlaylist.Length > 0 && !mMusic.isPlaying
            && mMusic.clip != null && mMusic.time == 0f && mMusic.timeSamples == 0)
            PlayNextTrack();
    }

    /// <summary>Проиграть звук события. pitchJitter — случайный разброс высоты, чтобы повторы не звучали механически.</summary>
    public static void Play(string name, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (sInstance == null || !AppSettings.Sound)
            return;
        if (!sInstance.mClips.TryGetValue(name, out var list) || list.Count == 0)
            return;
        AudioClip clip = list[Random.Range(0, list.Count)];
        sInstance.mSfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        sInstance.mSfx.PlayOneShot(clip, volume);
    }

    /// <summary>Проиграть звук с задержкой (например, «шах» сразу после звука хода).</summary>
    public static void PlayDelayed(string name, float delay, float volume = 1f)
    {
        if (sInstance != null)
            sInstance.StartCoroutine(sInstance.DelayedRoutine(name, delay, volume));
    }

    private IEnumerator DelayedRoutine(string name, float delay, float volume)
    {
        yield return new WaitForSecondsRealtime(delay);
        Play(name, volume);
    }

    private void ApplyVolumes()
    {
        mSfx.volume = AppSettings.Sound ? AppSettings.SoundVolume : 0f;
        mMusic.volume = AppSettings.MusicVolume;

        if (!AppSettings.Music || mPlaylist.Length == 0)
        {
            if (mMusic.isPlaying)
                mMusic.Pause();
            return;
        }

        if (mMusic.clip == null)
            PlayNextTrack();
        else if (!mMusic.isPlaying && mMusic.time > 0f)
            mMusic.UnPause();
    }

    private void PlayNextTrack()
    {
        mMusic.clip = mPlaylist[mTrack % mPlaylist.Length];
        mTrack++;
        mMusic.time = 0f;
        mMusic.Play();
    }

    /// <summary>«move_2» → «move».</summary>
    private static string BaseName(string name)
    {
        int i = name.LastIndexOf('_');
        return i > 0 && int.TryParse(name.Substring(i + 1), out _) ? name.Substring(0, i) : name;
    }
}
