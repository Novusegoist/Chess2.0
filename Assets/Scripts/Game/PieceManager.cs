using System.Collections.Generic;
using Chess2.Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Держит картинки фигур в соответствии с GameState.
/// Каждой фигуре ядра (по Id) соответствует один PieceView. Лежит поверх доски, того же размера.
/// </summary>
public class PieceManager : MonoBehaviour
{
    [Tooltip("Папка внутри Resources со спрайтами w_king, b_pawn, w_ghost и т.д.")]
    public string mSpriteFolder = "Pieces";

    [Tooltip("Размер фигуры в клетке 100x100")]
    public float mPieceSize = 96f;

    public Color mDegradeFlash = new Color32(255, 90, 90, 255);
    public Color mPromoteFlash = new Color32(120, 255, 140, 255);
    public Color mEmpowerFlash = new Color32(230, 60, 230, 255);

    [Tooltip("Папка внутри Resources с кадрами огня")]
    public string mFireFolder = "Fx/Fire";

    private readonly Dictionary<int, PieceView> mViews = new Dictionary<int, PieceView>();
    private Sprite[] mFireFrames;
    private RectTransform mFireLayer;

    /// <summary>Кадры огня (загружаются один раз).</summary>
    private Sprite[] FireFrames
    {
        get
        {
            if (mFireFrames != null)
                return mFireFrames;

            var sprites = new List<Sprite>(Resources.LoadAll<Sprite>(mFireFolder));
            if (sprites.Count == 0)
            {
                // Кадры импортированы как обычные текстуры — делаем спрайты сами.
                foreach (Texture2D tex in Resources.LoadAll<Texture2D>(mFireFolder))
                {
                    var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f));
                    s.name = tex.name;
                    sprites.Add(s);
                }
            }
            sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (sprites.Count == 0)
                Debug.LogError("Нет кадров огня в Resources/" + mFireFolder);
            mFireFrames = sprites.ToArray();
            return mFireFrames;
        }
    }

    /// <summary>Слой под фигурами, где горит огонь.</summary>
    private RectTransform FireLayer
    {
        get
        {
            if (mFireLayer == null)
            {
                mFireLayer = UiKit.CreateUi("Fire Layer", transform);
                UiKit.Stretch(mFireLayer);
            }
            mFireLayer.SetSiblingIndex(0);
            return mFireLayer;
        }
    }
    private readonly Dictionary<string, Sprite> mSprites = new Dictionary<string, Sprite>();
    private GameManager mGameManager;
    private Board mBoard;

    public void Setup(Board board, GameManager gameManager)
    {
        mBoard = board;
        mGameManager = gameManager;
    }

    /// <summary>Убрать все фигуры (новая партия).</summary>
    public void Clear()
    {
        foreach (PieceView view in mViews.Values)
            Destroy(view.gameObject);
        mViews.Clear();
    }

    /// <summary>
    /// Привести картинки к состоянию доски. effects — что случилось за ход (для анимаций),
    /// может быть null (например, при старте партии).
    /// </summary>
    public void Sync(GameState state, List<MoveEffect> effects)
    {
        var alive = new HashSet<int>();
        var moved = new HashSet<int>();
        if (effects != null)
            foreach (MoveEffect e in effects)
                if (e.Kind == EffectKind.Moved)
                    moved.Add(e.PieceId);

        for (int sq = 0; sq < 64; sq++)
        {
            Piece p = state.Cells[sq];
            if (p.IsEmpty)
                continue;

            alive.Add(p.Id);
            bool isNew = !mViews.TryGetValue(p.Id, out PieceView view);
            if (isNew)
            {
                view = CreateView(p.Id);
                mViews.Add(p.Id, view);
            }

            view.Square = sq;
            view.SetSprite(GetSprite(p.Side, p.Type));
            if (isNew)
                view.SetOutline(p.Side == Side.White);

            view.SetRider(false, mPieceSize);
            // Огонь — только у загоревшихся фигур, у которых есть способность в нынешнем виде
            // (ослабленная до пешки фигура гаснет, а возродившись — снова горит).
            bool burning = p.Empowered && Chess2.Core.Rules.HasAbility(state, p);
            view.SetFire(burning ? FireFrames : null, FireLayer);
            Vector2 target = mBoard.GetCellLocalPosition(sq);
            if (isNew || !moved.Contains(p.Id))
                view.SnapTo(target);
            else
                view.AnimateTo(target);
        }

        // Пассажиры троянских коней: маленькая фигурка в верхнем углу клетки коня.
        for (int sq = 0; sq < 64; sq++)
        {
            Piece c = state.Cargo[sq];
            if (c.IsEmpty)
                continue;

            alive.Add(c.Id);
            bool isNew = !mViews.TryGetValue(c.Id, out PieceView view);
            if (isNew)
            {
                view = CreateView(c.Id);
                mViews.Add(c.Id, view);
            }

            view.Square = sq;
            view.SetSprite(GetSprite(c.Side, c.Type));
            view.SetRider(true, mPieceSize);
            view.SetFire(null, null);
            view.transform.SetAsLastSibling();

            Vector2 target = mBoard.GetCellLocalPosition(sq) + new Vector2(Board.CellSize * 0.28f, Board.CellSize * 0.26f);
            int knightId = state.Cells[sq].Id;
            if (isNew)
                view.SnapTo(target);
            else
                view.AnimateTo(target, moved.Contains(knightId) ? 0.15f : 0.2f);
        }

        // Ушедшие с доски (срубленные призраки, павший король, взорванный конь) тают.
        var dead = new List<int>();
        foreach (var kv in mViews)
            if (!alive.Contains(kv.Key))
                dead.Add(kv.Key);
        foreach (int id in dead)
        {
            if (effects == null)
                Destroy(mViews[id].gameObject);
            else
                mViews[id].Vanish();
            mViews.Remove(id);
        }

        if (effects != null)
        {
            foreach (MoveEffect e in effects)
            {
                if (!mViews.TryGetValue(e.PieceId, out PieceView view))
                    continue;
                if (e.Kind == EffectKind.Degraded)
                    view.Pulse(mDegradeFlash);
                else if (e.Kind == EffectKind.Empowered)
                    view.Pulse(mEmpowerFlash);
                else if (e.Kind == EffectKind.Promoted || e.Kind == EffectKind.Disembarked || e.Kind == EffectKind.Ejected)
                    view.Pulse(mPromoteFlash);
            }
        }
    }

    /// <summary>Вернуть фигуру на её клетку (неудачное перетаскивание).</summary>
    public void ReturnToSquare(int sq)
    {
        foreach (PieceView view in mViews.Values)
            if (view.Square == sq)
                view.AnimateTo(mBoard.GetCellLocalPosition(sq), 0.1f);
    }

    private PieceView CreateView(int id)
    {
        var go = new GameObject("Piece #" + id, typeof(RectTransform), typeof(Image), typeof(PieceView));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.sizeDelta = new Vector2(mPieceSize, mPieceSize);

        var view = go.GetComponent<PieceView>();
        view.Setup(id, mGameManager);
        return view;
    }

    private Sprite GetSprite(Side side, PieceType type)
    {
        string name = (side == Side.White ? "w_" : "b_") + type.ToString().ToLowerInvariant();
        if (mSprites.TryGetValue(name, out Sprite cached))
            return cached;

        string path = mSpriteFolder + "/" + name;
        Sprite sprite = Resources.Load<Sprite>(path);

        // Если картинка импортирована как обычная текстура, а не Sprite — делаем спрайт сами.
        if (sprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            else
                Debug.LogError($"Нет спрайта фигуры Resources/{path}.png");
        }

        mSprites[name] = sprite;
        return sprite;
    }
}
