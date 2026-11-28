using System.Collections.Generic;
using Chess2.Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Отображение доски: клетки, подсветка ходов, последний ход, разворот доски.
/// Правил здесь нет — они в Chess2.Core. Всё создаётся из кода, префабы не нужны.
/// Координаты внутри доски: (0,0) — левый нижний угол, клетка 100x100.
/// </summary>
public class Board : MonoBehaviour
{
    public const float CellSize = 100f;

    [Header("Цвета клеток")]
    public Color mLightColor = new Color32(230, 220, 187, 255);
    public Color mDarkColor = new Color32(176, 136, 98, 255);

    [Header("Подсветка")]
    public Color mMoveColor = new Color32(20, 20, 20, 90);
    public Color mCaptureColor = new Color32(210, 40, 40, 200);
    public Color mPierceColor = new Color32(150, 60, 230, 220);
    [Tooltip("Двойной прыжок коня")]
    public Color mDoubleColor = new Color32(0, 165, 175, 220);
    [Tooltip("Посадка в коня и высадка из него")]
    public Color mTrojanColor = new Color32(60, 175, 70, 230);
    public Color mSelectedTint = new Color32(255, 230, 120, 255);
    public Color mLastMoveTint = new Color32(240, 200, 90, 255);

    public RectTransform Rect { get; private set; }
    public bool Flipped { get; private set; }

    private readonly Cell[] mAllCells = new Cell[64];
    private readonly Color[] mBaseColors = new Color[64];
    private readonly List<int> mHighlighted = new List<int>();
    private int mSelected = Sq.None;
    private Move? mLastMove;
    private Sprite mDotSprite;
    private Sprite mRingSprite;
    private readonly Text[] mFileLabels = new Text[8]; // a..h вдоль нижнего края
    private readonly Text[] mRankLabels = new Text[8]; // 1..8 вдоль левого края

    public void Create(GameManager gameManager)
    {
        Rect = (RectTransform)transform;
        mDotSprite = MakeCircleSprite(64, 0f);
        mRingSprite = MakeCircleSprite(128, 0.82f);

        for (int sq = 0; sq < 64; sq++)
        {
            var cellGo = new GameObject("Cell " + Sq.Name(sq), typeof(RectTransform), typeof(Image), typeof(Cell));
            cellGo.transform.SetParent(transform, false);
            var rt = (RectTransform)cellGo.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.sizeDelta = new Vector2(CellSize, CellSize);

            var markerGo = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            markerGo.transform.SetParent(cellGo.transform, false);
            var marker = markerGo.GetComponent<Image>();
            marker.raycastTarget = false;

            var cell = cellGo.GetComponent<Cell>();
            cell.Setup(sq, gameManager, marker);
            mAllCells[sq] = cell;
        }

        // Координаты по краю доски, как на настоящей: буквы внизу, цифры слева (поверх клеток).
        for (int i = 0; i < 8; i++)
        {
            mFileLabels[i] = CreateLabel("File " + i, TextAnchor.LowerRight);
            mRankLabels[i] = CreateLabel("Rank " + i, TextAnchor.UpperLeft);
        }

        ApplyTheme();
        BoardTheme.Changed += ApplyTheme;
    }

    void OnDestroy() => BoardTheme.Changed -= ApplyTheme;

    /// <summary>Перекрасить клетки и подписи в тему из настроек (меняется в опциях, в том числе посреди партии).</summary>
    public void ApplyTheme()
    {
        BoardTheme theme = BoardTheme.Current;
        mLightColor = theme.Light;
        mDarkColor = theme.Dark;
        for (int sq = 0; sq < 64; sq++)
            mBaseColors[sq] = (Sq.X(sq) + Sq.Y(sq)) % 2 != 0 ? mLightColor : mDarkColor;
        Layout();
        RefreshColors();
    }

    private Text CreateLabel(string name, TextAnchor align)
    {
        var t = UiKit.CreateText(name, transform, "", 20, Color.white, align, shadow: false);
        t.fontStyle = FontStyle.Bold;
        var rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CellSize - 8, CellSize - 6);
        return t;
    }

    /// <summary>Подписи по краю: буквы и цифры с учётом разворота доски, цветом «наоборот» к клетке.</summary>
    private void LayoutLabels()
    {
        if (mFileLabels[0] == null)
            return;
        for (int i = 0; i < 8; i++)
        {
            // i — столбец/строка на экране (0 — слева/снизу).
            int file = Flipped ? 7 - i : i;
            int rank = Flipped ? 7 - i : i;

            Text f = mFileLabels[i];
            f.text = ((char)('a' + file)).ToString();
            ((RectTransform)f.transform).anchoredPosition = new Vector2(i * CellSize + CellSize / 2, CellSize / 2);
            f.color = LabelColor(Sq.Of(file, Flipped ? 7 : 0));

            Text r = mRankLabels[i];
            r.text = (rank + 1).ToString();
            ((RectTransform)r.transform).anchoredPosition = new Vector2(CellSize / 2, i * CellSize + CellSize / 2);
            r.color = LabelColor(Sq.Of(Flipped ? 7 : 0, rank));
        }
    }

    /// <summary>На светлой клетке — тёмная подпись, на тёмной — светлая.</summary>
    private Color LabelColor(int sq)
    {
        bool light = (Sq.X(sq) + Sq.Y(sq)) % 2 != 0;
        Color c = light ? mDarkColor : mLightColor;
        c.a = 0.95f;
        return c;
    }

    /// <summary>Развернуть доску (если человек играет чёрными).</summary>
    public void SetFlipped(bool flipped)
    {
        Flipped = flipped;
        Layout();
    }

    /// <summary>Центр клетки в координатах доски (и слоя фигур — у них одинаковый размер).</summary>
    public Vector2 GetCellLocalPosition(int sq)
    {
        int x = Sq.X(sq), y = Sq.Y(sq);
        if (Flipped)
        {
            x = 7 - x;
            y = 7 - y;
        }
        return new Vector2(x * CellSize + CellSize / 2, y * CellSize + CellSize / 2);
    }

    /// <summary>Какая клетка под точкой экрана (или Sq.None).</summary>
    public int ScreenToSquare(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screenPoint, eventCamera, out Vector2 local))
            return Sq.None;

        local -= Rect.rect.min;
        int x = Mathf.FloorToInt(local.x / CellSize);
        int y = Mathf.FloorToInt(local.y / CellSize);
        if (!Sq.InBounds(x, y))
            return Sq.None;
        if (Flipped)
        {
            x = 7 - x;
            y = 7 - y;
        }
        return Sq.Of(x, y);
    }

    public void ShowMoves(int selected, List<Move> moves)
    {
        ClearMoves();
        mSelected = selected;

        foreach (Move m in moves)
        {
            if (m.Kind == MoveKind.Explode)
                continue; // взрыв — отдельной кнопкой, не клеткой

            Image marker = mAllCells[m.To].mMarker;
            var rt = (RectTransform)marker.transform;
            marker.enabled = true;

            // Точка — ход на пустую клетку, кольцо — действие с фигурой на клетке.
            bool dot;
            switch (m.Kind)
            {
                case MoveKind.DoubleJump: dot = true; marker.color = mDoubleColor; break;
                case MoveKind.DoubleCapture: dot = false; marker.color = mDoubleColor; break;
                case MoveKind.Disembark: dot = true; marker.color = mTrojanColor; break;
                case MoveKind.Embark: dot = false; marker.color = mTrojanColor; break;
                case MoveKind.Pierce: dot = false; marker.color = mPierceColor; break;
                case MoveKind.Capture: dot = false; marker.color = mCaptureColor; break;
                default: dot = true; marker.color = mMoveColor; break;
            }
            marker.sprite = dot ? mDotSprite : mRingSprite;
            float size = dot ? 0.32f : 0.96f;
            rt.sizeDelta = new Vector2(CellSize * size, CellSize * size);
            mHighlighted.Add(m.To);
        }
        RefreshColors();
    }

    public void ClearMoves()
    {
        foreach (int sq in mHighlighted)
            mAllCells[sq].mMarker.enabled = false;
        mHighlighted.Clear();
        mSelected = Sq.None;
        RefreshColors();
    }

    /// <summary>Подсветить, откуда и куда был последний ход.</summary>
    public void SetLastMove(Move? move)
    {
        mLastMove = move;
        RefreshColors();
    }

    private void Layout()
    {
        LayoutLabels();
        for (int sq = 0; sq < 64; sq++)
            if (mAllCells[sq] != null)
                mAllCells[sq].mRectTransform.anchoredPosition = GetCellLocalPosition(sq);
    }

    private void RefreshColors()
    {
        for (int sq = 0; sq < 64; sq++)
        {
            if (mAllCells[sq] == null)
                continue;

            Color c = mBaseColors[sq];
            if (mLastMove.HasValue && (mLastMove.Value.From == sq || mLastMove.Value.To == sq))
                c = Color.Lerp(c, mLastMoveTint, 0.45f);
            if (sq == mSelected)
                c = Color.Lerp(c, mSelectedTint, 0.6f);

            mAllCells[sq].mImage.color = c;
        }
    }

    /// <summary>Белый круг (inner = 0) или кольцо (inner — доля внутреннего радиуса) со сглаженными краями.</summary>
    private static Sprite MakeCircleSprite(int size, float inner)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = size / 2f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d);
                if (inner > 0)
                    a *= Mathf.Clamp01(d - inner * r);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
