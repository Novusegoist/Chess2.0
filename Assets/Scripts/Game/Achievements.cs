using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Достижения: список, сохранение (PlayerPrefs — одинаково на ПК, Android и iOS)
/// и всплывающее уведомление «Достижение открыто». Проверки событий — в GameManager.
/// Позже сюда же можно подключить достижения Steam, Google Play и Game Center.
/// </summary>
public static class Achievements
{
    public sealed class Def
    {
        public string Id;
        public string Title;
        public string Description;
    }

    public static readonly IReadOnlyList<Def> All = new[]
    {
        new Def { Id = "first_win", Title = "Первая победа", Description = "Выиграй партию у бота или по сети" },
        new Def { Id = "beat_easy", Title = "Разминка", Description = "Победи бота-новичка" },
        new Def { Id = "beat_medium", Title = "Любитель повержен", Description = "Победи бота-любителя" },
        new Def { Id = "beat_hard", Title = "Сильнее сильного", Description = "Победи сильного бота" },
        new Def { Id = "beat_expert", Title = "Мастер", Description = "Победи бота-мастера" },
        new Def { Id = "king_fell", Title = "Король пал", Description = "Поставь мат чужому королю" },
        new Def { Id = "kingless_win", Title = "Без короля", Description = "Выиграй партию, потеряв своего короля" },
        new Def { Id = "trojan_boom", Title = "Троянский взрыв", Description = "Взорви троянского коня" },
        new Def { Id = "big_boom", Title = "Большой бум", Description = "Ослабь взрывом сразу три чужие фигуры" },
        new Def { Id = "pierce", Title = "Насквозь", Description = "Ударь сквозь фигуру ладьёй или слоном" },
        new Def { Id = "double_jump", Title = "Двойной прыжок", Description = "Прыгни конём дважды за ход" },
        new Def { Id = "revive", Title = "Возрождение", Description = "Верни ослабленную фигуру, проведя пешку до конца доски" },
        new Def { Id = "ghost_hunter", Title = "Охотник за призраками", Description = "Убери с доски 10 чужих призраков" },
        new Def { Id = "streak3", Title = "Серия", Description = "Выиграй три партии подряд" },
        new Def { Id = "chaos_win", Title = "Повелитель хаоса", Description = "Выиграй партию в режиме «Хаос»" },
        new Def { Id = "net_win", Title = "Сетевой боец", Description = "Выиграй партию по сети" },
        new Def { Id = "all_lessons", Title = "Ученик", Description = "Пройди все уроки обучения" },
    };

    private const string Prefix = "chess2.ach.";

    public static bool IsUnlocked(string id) => PlayerPrefs.GetInt(Prefix + id, 0) != 0;

    public static int UnlockedCount
    {
        get
        {
            int n = 0;
            foreach (Def d in All)
                if (IsUnlocked(d.Id))
                    n++;
            return n;
        }
    }

    /// <summary>Открыть достижение (повторно — ничего не происходит) и показать уведомление.</summary>
    public static void Unlock(string id)
    {
        if (IsUnlocked(id))
            return;
        Def def = Find(id);
        if (def == null)
            return;
        PlayerPrefs.SetInt(Prefix + id, 1);
        PlayerPrefs.Save();
        AchievementToast.Show(def);
    }

    /// <summary>Накопительный счётчик (например, убранные призраки).</summary>
    public static int AddProgress(string key, int amount = 1)
    {
        int value = PlayerPrefs.GetInt(Prefix + "count." + key, 0) + amount;
        PlayerPrefs.SetInt(Prefix + "count." + key, value);
        return value;
    }

    public static void ResetAll()
    {
        foreach (Def d in All)
            PlayerPrefs.DeleteKey(Prefix + d.Id);
        PlayerPrefs.DeleteKey(Prefix + "count.ghosts");
        PlayerPrefs.Save();
    }

    private static Def Find(string id)
    {
        foreach (Def d in All)
            if (d.Id == id)
                return d;
        return null;
    }
}

/// <summary>Плашка «Достижение открыто» сверху экрана поверх всего, на несколько секунд.</summary>
public class AchievementToast : MonoBehaviour
{
    private static AchievementToast sInstance;
    private readonly Queue<Achievements.Def> mQueue = new Queue<Achievements.Def>();
    private RectTransform mPlate;
    private Text mTitle;
    private Text mDescription;
    private bool mShowing;

    public static void Show(Achievements.Def def)
    {
        if (sInstance == null)
            Create();
        sInstance.mQueue.Enqueue(def);
        if (!sInstance.mShowing)
            sInstance.StartCoroutine(sInstance.Run());
    }

    private static void Create()
    {
        var go = new GameObject("Achievement Toast");
        DontDestroyOnLoad(go);
        sInstance = go.AddComponent<AchievementToast>();

        Canvas canvas = UiKit.CreateCanvas("Toast Canvas", go.transform, 100);
        canvas.GetComponent<GraphicRaycaster>().enabled = false; // не мешает кликам
        RectTransform safe = UiKit.CreateSafeRoot(canvas.transform);

        var plate = UiKit.CreateUi("Plate", safe, typeof(Image));
        plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0.5f, 1);
        plate.sizeDelta = new Vector2(620, 110);
        var img = plate.GetComponent<Image>();
        img.sprite = UiKit.LoadSprite("UI/plate");
        if (img.sprite != null && img.sprite.border != Vector4.zero)
            img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        sInstance.mPlate = plate;

        var head = UiKit.CreateText("Head", plate, "Достижение открыто!", 22, new Color32(31, 79, 99, 255), shadow: false);
        var hrt = (RectTransform)head.transform;
        hrt.anchoredPosition = new Vector2(0, 30);
        hrt.sizeDelta = new Vector2(560, 30);

        sInstance.mTitle = UiKit.CreateText("Title", plate, "", 32, new Color32(0, 120, 200, 255), shadow: false);
        sInstance.mTitle.fontStyle = FontStyle.Bold;
        var trt = (RectTransform)sInstance.mTitle.transform;
        trt.anchoredPosition = new Vector2(0, -2);
        trt.sizeDelta = new Vector2(560, 40);

        sInstance.mDescription = UiKit.CreateText("Desc", plate, "", 20, new Color32(31, 79, 99, 255), shadow: false);
        var drt = (RectTransform)sInstance.mDescription.transform;
        drt.anchoredPosition = new Vector2(0, -34);
        drt.sizeDelta = new Vector2(580, 28);

        plate.gameObject.SetActive(false);
    }

    private IEnumerator Run()
    {
        mShowing = true;
        while (mQueue.Count > 0)
        {
            Achievements.Def def = mQueue.Dequeue();
            mTitle.text = def.Title;
            mDescription.text = def.Description;
            mPlate.gameObject.SetActive(true);
            SoundManager.Play("empower", 0.9f, 0f);

            // Выезжает сверху, держится, уезжает обратно.
            const float hidden = 130f, shown = -20f;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                mPlate.anchoredPosition = new Vector2(0, Mathf.Lerp(hidden, shown, Mathf.SmoothStep(0, 1, t)));
                yield return null;
            }
            mPlate.anchoredPosition = new Vector2(0, shown);
            yield return new WaitForSecondsRealtime(3f);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                mPlate.anchoredPosition = new Vector2(0, Mathf.Lerp(shown, hidden, Mathf.SmoothStep(0, 1, t)));
                yield return null;
            }
            mPlate.gameObject.SetActive(false);
        }
        mShowing = false;
    }
}
