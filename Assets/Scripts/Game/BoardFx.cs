using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Эффекты поверх доски: искры от удара, взрыв коня (вспышка и тряска), падающая корона павшего короля,
/// вспышка возрождения пешки. Всё рисуется интерфейсом (Image), поэтому одинаково на ПК и телефонах.
/// Выключаются в опциях («Эффекты»). Координаты — как у фигур: от левого нижнего угла доски.
/// </summary>
public class BoardFx : MonoBehaviour
{
    private static readonly Color Spark = new Color32(255, 225, 150, 255);
    private static readonly Color Hit = new Color32(255, 120, 90, 255);
    private static readonly Color Fire = new Color32(255, 150, 40, 255);
    private static readonly Color Gold = new Color32(255, 215, 90, 255);
    private static readonly Color Revive = new Color32(140, 255, 160, 255);
    private static readonly Color Ghost = new Color32(200, 215, 230, 255);
    private static readonly Color Magic = new Color32(235, 90, 235, 255);

    private static Sprite sDot, sRing;

    private RectTransform mLayer;
    private RectTransform[] mShakeTargets;
    private Vector2[] mShakeBase;
    private float mShakeUntil, mShakePower;
    private Coroutine mShake;

    /// <summary>Слой эффектов поверх фигур; shakeTargets — что трясти при взрыве (доска и фигуры).</summary>
    public static BoardFx Create(RectTransform pieces, params RectTransform[] shakeTargets)
    {
        RectTransform layer = UiKit.Stretch(UiKit.CreateUi("Fx Layer", pieces));
        // Свой Canvas: частицы двигаются каждый кадр, и без него пересобирался бы весь слой фигур.
        layer.gameObject.AddComponent<Canvas>();
        var fx = layer.gameObject.AddComponent<BoardFx>();
        fx.mLayer = layer;
        fx.mShakeTargets = shakeTargets;
        return fx;
    }

    /// <summary>Сыграть эффекты хода. pos(sq) — центр клетки в координатах доски.</summary>
    public void Play(List<Chess2.Core.MoveEffect> effects, System.Func<int, Vector2> pos,
        System.Func<Chess2.Core.Side, Sprite> kingSprite)
    {
        if (!AppSettings.Effects || effects == null)
            return;
        mLayer.SetAsLastSibling(); // фигуры, созданные позже, не должны закрывать эффекты

        foreach (Chess2.Core.MoveEffect e in effects)
        {
            switch (e.Kind)
            {
                case Chess2.Core.EffectKind.Degraded:
                    Burst(pos(e.To), Hit, 12, 260f, 0.12f);
                    Burst(pos(e.To), Spark, 8, 180f, 0.12f);
                    break;
                case Chess2.Core.EffectKind.Removed:
                    Burst(pos(e.To), Ghost, 10, 120f, 0.1f, gravity: -120f);
                    break;
                case Chess2.Core.EffectKind.Exploded:
                    Explosion(pos(e.From));
                    break;
                case Chess2.Core.EffectKind.KingFell:
                    StartCoroutine(FallingCrown(pos(e.From), kingSprite(e.Side)));
                    break;
                case Chess2.Core.EffectKind.Promoted:
                    StartCoroutine(Ring(pos(e.To), Revive, 0.15f, 0.55f, 2.2f));
                    Burst(pos(e.To), Gold, 14, 140f, 0.2f, gravity: -260f);
                    break;
                case Chess2.Core.EffectKind.Empowered:
                    Burst(pos(e.From), Magic, 10, 150f, 0.15f, gravity: -200f);
                    break;
            }
        }
    }

    /// <summary>Убрать всё, что ещё летит (новая партия, отмена хода).</summary>
    public void Clear()
    {
        StopAllCoroutines();
        for (int i = mLayer.childCount - 1; i >= 0; i--)
            Destroy(mLayer.GetChild(i).gameObject);
        RestoreShake();
    }

    void OnDisable() => Clear();

    private void Explosion(Vector2 at)
    {
        StartCoroutine(Ring(at, Color.white, 0f, 0.25f, 3.5f, disk: true, startAlpha: 0.9f));
        StartCoroutine(Ring(at, Fire, 0.03f, 0.45f, 3f));
        Burst(at, Fire, 26, 420f, 0.02f);
        Burst(at, Spark, 16, 300f, 0.02f);
        Burst(at, new Color(0.25f, 0.22f, 0.2f, 1f), 10, 90f, 0.08f, gravity: -60f, life: 1f, size: 26f);
        Shake(0.35f, 14f);
    }

    #region Частицы

    private void Burst(Vector2 at, Color color, int count, float speed, float delay,
        float gravity = 700f, float life = 0.55f, float size = 9f)
    {
        StartCoroutine(BurstRoutine(at, color, count, speed, delay, gravity, life, size));
    }

    private IEnumerator BurstRoutine(Vector2 at, Color color, int count, float speed, float delay,
        float gravity, float life, float size)
    {
        if (delay > 0)
            yield return new WaitForSecondsRealtime(delay);

        var parts = new Image[count];
        var vel = new Vector2[count];
        var p = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            parts[i] = Spawn(Dot, color, size * Random.Range(0.6f, 1.4f));
            float a = Random.value * Mathf.PI * 2f;
            vel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.4f, 1f);
            p[i] = at;
        }

        for (float t = 0; t < life; t += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            float k = t / life;
            for (int i = 0; i < count; i++)
            {
                vel[i].y -= gravity * dt;
                vel[i] *= 1f - 2f * dt; // сопротивление воздуха
                p[i] += vel[i] * dt;
                parts[i].rectTransform.anchoredPosition = p[i];
                Color c = color;
                c.a = 1f - k * k;
                parts[i].color = c;
            }
            yield return null;
        }

        foreach (Image img in parts)
            Destroy(img.gameObject);
    }

    /// <summary>Расходящееся кольцо (или вспышка-диск).</summary>
    private IEnumerator Ring(Vector2 at, Color color, float delay, float duration, float cells,
        bool disk = false, float startAlpha = 1f)
    {
        if (delay > 0)
            yield return new WaitForSecondsRealtime(delay);

        Image img = Spawn(disk ? Dot : RingSprite, color, Board.CellSize);
        img.rectTransform.anchoredPosition = at;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            float ease = 1f - (1f - k) * (1f - k);
            img.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, cells, ease);
            Color c = color;
            c.a = startAlpha * (1f - k);
            img.color = c;
            yield return null;
        }
        Destroy(img.gameObject);
    }

    /// <summary>Король подпрыгивает, кувыркается и падает вниз с доски, а над клеткой рассыпается золото.</summary>
    private IEnumerator FallingCrown(Vector2 at, Sprite king)
    {
        Burst(at, Gold, 16, 220f, 0f, gravity: 500f, life: 0.8f);
        StartCoroutine(Ring(at, Gold, 0f, 0.5f, 2.5f));
        if (king == null)
            yield break;

        Image img = Spawn(king, Color.white, Board.CellSize * 0.95f);
        img.preserveAspect = true;
        RectTransform rt = img.rectTransform;
        Vector2 vel = new Vector2(Random.value < 0.5f ? -110f : 110f, 420f);
        float spin = -Mathf.Sign(vel.x) * 260f;
        Vector2 p = at;
        const float life = 1.4f;
        for (float t = 0; t < life; t += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            vel.y -= 1100f * dt;
            p += vel * dt;
            rt.anchoredPosition = p;
            rt.localEulerAngles = new Vector3(0, 0, spin * t);
            float k = t / life;
            img.color = new Color(1f, 1f, 1f, 1f - k * k * k);
            yield return null;
        }
        Destroy(img.gameObject);
    }

    private Image Spawn(Sprite sprite, Color color, float size)
    {
        var go = new GameObject("Fx", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(mLayer, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    #endregion

    #region Тряска

    private void Shake(float duration, float power)
    {
        if (mShakeTargets == null || mShakeTargets.Length == 0)
            return;
        mShakeUntil = Time.unscaledTime + duration;
        mShakePower = power;
        if (mShake == null)
            mShake = StartCoroutine(ShakeRoutine(duration));
    }

    private IEnumerator ShakeRoutine(float duration)
    {
        mShakeBase = new Vector2[mShakeTargets.Length];
        for (int i = 0; i < mShakeTargets.Length; i++)
            mShakeBase[i] = mShakeTargets[i].anchoredPosition;

        while (Time.unscaledTime < mShakeUntil)
        {
            float left = (mShakeUntil - Time.unscaledTime) / duration;
            Vector2 offset = Random.insideUnitCircle * mShakePower * Mathf.Clamp01(left);
            for (int i = 0; i < mShakeTargets.Length; i++)
                mShakeTargets[i].anchoredPosition = mShakeBase[i] + offset;
            yield return null;
        }
        RestoreShake();
    }

    private void RestoreShake()
    {
        if (mShakeBase != null)
            for (int i = 0; i < mShakeTargets.Length; i++)
                if (mShakeTargets[i] != null)
                    mShakeTargets[i].anchoredPosition = mShakeBase[i];
        mShakeBase = null;
        mShake = null;
    }

    #endregion

    #region Спрайты (рисуются один раз в памяти)

    private static Sprite Dot => sDot ??= MakeSprite((d) => Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));

    private static Sprite RingSprite => sRing ??= MakeSprite((d) => Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.18f));

    /// <summary>Круглый спрайт 64×64: alpha(d), где d — расстояние от центра (0…1).</summary>
    private static Sprite MakeSprite(System.Func<float, float> alpha)
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(alpha(d))));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    #endregion
}
