using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Картинка одной фигуры. Ничего не знает о правилах: передаёт клики и перетаскивание
/// в GameManager и проигрывает анимации. Позиция — в координатах доски (anchoredPosition).
/// </summary>
public class PieceView : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int PieceId { get; private set; }
    public int Square { get; set; }

    private GameManager mGameManager;
    private RectTransform mRectTransform;
    private Image mImage;
    private Coroutine mMoveRoutine;
    private Coroutine mPulseRoutine;
    private bool mDragging;
    private FireFx mFire;

    public void Setup(int pieceId, GameManager gameManager)
    {
        PieceId = pieceId;
        mGameManager = gameManager;
        mRectTransform = (RectTransform)transform;
        mImage = GetComponent<Image>();
        mImage.preserveAspect = true;   // спрайты вытянутые — не сплющиваем
        mImage.color = Color.white;     // никакой перекраски: у белых и чёрных свои картинки
    }

    public void SetSprite(Sprite sprite) => mImage.sprite = sprite;

    /// <summary>
    /// Огонь за фигурой (у неё есть способность). frames = null — потушить.
    /// layer — слой под фигурами, чтобы огонь был позади.
    /// </summary>
    public void SetFire(Sprite[] frames, RectTransform layer)
    {
        if (frames == null || frames.Length == 0)
        {
            if (mFire != null)
            {
                Destroy(mFire.gameObject);
                mFire = null;
            }
            return;
        }
        if (mFire != null)
            return;

        var go = new GameObject("Fire #" + PieceId, typeof(RectTransform), typeof(Image), typeof(FireFx));
        go.transform.SetParent(layer, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        float width = mRectTransform.sizeDelta.x * 0.95f;
        Rect r = frames[0].rect;
        rt.sizeDelta = new Vector2(width, width * r.height / r.width);
        mFire = go.GetComponent<FireFx>();
        mFire.Init(frames);
        LateUpdate();
    }

    void LateUpdate()
    {
        // Огонь стоит у основания фигуры и ходит за ней (в том числе при перетаскивании).
        if (mFire != null)
            mFire.Rect.anchoredPosition = mRectTransform.anchoredPosition
                                          + new Vector2(0, -mRectTransform.sizeDelta.y * 0.47f);
    }

    void OnDestroy()
    {
        if (mFire != null)
            Destroy(mFire.gameObject);
    }

    /// <summary>Фигура уходит с доски: сжимается и тает, потом удаляется.</summary>
    public void Vanish()
    {
        mImage.raycastTarget = false;
        if (mMoveRoutine != null)
            StopCoroutine(mMoveRoutine);
        if (mPulseRoutine != null)
            StopCoroutine(mPulseRoutine);
        StartCoroutine(VanishRoutine());
    }

    private IEnumerator VanishRoutine()
    {
        const float duration = 0.45f;
        Color start = mImage.color;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, k);
            mImage.color = new Color(start.r, start.g, start.b, 1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>Пассажир коня: уменьшен и не ловит клики (клик идёт в коня под ним).</summary>
    public void SetRider(bool rider, float fullSize)
    {
        float size = rider ? fullSize * 0.45f : fullSize;
        mRectTransform.sizeDelta = new Vector2(size, size);
        mImage.raycastTarget = !rider;
    }

    /// <summary>
    /// Тёмная обводка только для белых фигур — чтобы не терялись на светлых клетках.
    /// Чёрным обводка не нужна: светлое «свечение» вокруг них выглядит размыто.
    /// </summary>
    public void SetOutline(bool white)
    {
        var outline = GetComponent<Outline>();
        if (!white)
        {
            if (outline != null)
                Destroy(outline);
            return;
        }
        if (outline == null)
            outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.12f, 0.1f, 0.85f);
        outline.effectDistance = new Vector2(1.6f, -1.6f);
    }

    public void SnapTo(Vector2 position)
    {
        if (mMoveRoutine != null)
            StopCoroutine(mMoveRoutine);
        mRectTransform.anchoredPosition = position;
    }

    public void AnimateTo(Vector2 position, float duration = 0.15f)
    {
        if (mMoveRoutine != null)
            StopCoroutine(mMoveRoutine);
        mMoveRoutine = StartCoroutine(MoveRoutine(position, duration));
        AppPerformance.Wake(); // ход (в том числе бота или соперника по сети) — плавно
    }

    /// <summary>Короткая «вспышка» — фигура ослабла или возродилась.</summary>
    public void Pulse(Color flash)
    {
        if (mPulseRoutine != null)
            StopCoroutine(mPulseRoutine);
        mPulseRoutine = StartCoroutine(PulseRoutine(flash));
    }

    private IEnumerator MoveRoutine(Vector2 target, float duration)
    {
        Vector2 start = mRectTransform.anchoredPosition;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float k = 1f - (1f - t / duration) * (1f - t / duration);
            mRectTransform.anchoredPosition = Vector2.Lerp(start, target, k);
            yield return null;
        }
        mRectTransform.anchoredPosition = target;
        mMoveRoutine = null;
    }

    private IEnumerator PulseRoutine(Color flash)
    {
        const float duration = 0.35f;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / duration * Mathf.PI);
            transform.localScale = Vector3.one * (1f + 0.25f * k);
            mImage.color = Color.Lerp(Color.white, flash, k);
            yield return null;
        }
        transform.localScale = Vector3.one;
        mImage.color = Color.white;
        mPulseRoutine = null;
    }

    #region Ввод

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!mDragging)
            mGameManager.OnSquareClicked(Square);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!mGameManager.CanPickUp(Square))
        {
            eventData.pointerDrag = null; // чужую фигуру не таскаем
            return;
        }
        mDragging = true;
        mGameManager.Select(Square);
        transform.SetAsLastSibling(); // поверх остальных фигур
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!mDragging)
            return;
        var parent = (RectTransform)mRectTransform.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
            mRectTransform.anchoredPosition = local - parent.rect.min;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!mDragging)
            return;
        mDragging = false;
        mGameManager.OnPieceDropped(Square, eventData.position, eventData.pressEventCamera);
    }

    #endregion
}
