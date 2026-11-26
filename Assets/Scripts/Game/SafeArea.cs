using UnityEngine;

/// <summary>
/// Держит интерфейс внутри «безопасной зоны» экрана — не под вырезом камеры и скруглёнными углами телефона.
/// Вешается на растянутый на весь холст RectTransform. Фон (Bleed) при этом растягивается на весь экран.
/// На ПК безопасная зона — весь экран, и ничего не меняется.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    /// <summary>Дочерний фон, который должен заходить и под вырез (необязательно).</summary>
    public RectTransform Bleed;

    private Rect mLastSafe;
    private Vector2Int mLastSize;

    void OnEnable() => Apply(force: true);

    void Update() => Apply(force: false);

    private void Apply(bool force)
    {
        Rect safe = Screen.safeArea;
        var size = new Vector2Int(Screen.width, Screen.height);
        if (!force && safe == mLastSafe && size == mLastSize)
            return;
        mLastSafe = safe;
        mLastSize = size;
        if (size.x <= 0 || size.y <= 0)
            return;

        Vector2 min = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
        Vector2 max = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
        var rt = (RectTransform)transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        if (Bleed != null)
        {
            // Весь экран [0..1] в координатах безопасной зоны.
            Vector2 span = max - min;
            Bleed.anchorMin = new Vector2(-min.x / span.x, -min.y / span.y);
            Bleed.anchorMax = new Vector2((1 - min.x) / span.x, (1 - min.y) / span.y);
            Bleed.offsetMin = Bleed.offsetMax = Vector2.zero;
        }
    }
}
