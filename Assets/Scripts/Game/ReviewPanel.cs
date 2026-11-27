using UnityEngine;
using UnityEngine.UI;

/// <summary>Панель разбора партии слева от доски: какой ход, его оценка, совет бота и итоги по сторонам.</summary>
public class ReviewPanel : MonoBehaviour
{
    public const float Width = 360f;

    private Text mTitle;
    private Text mMove;
    private Text mSummary;

    public static ReviewPanel Create(Transform parent, Vector2 position)
    {
        var rt = UiKit.CreateUi("Review", parent, typeof(Image), typeof(VerticalLayoutGroup), typeof(ReviewPanel));
        rt.sizeDelta = new Vector2(Width, 700);
        rt.anchoredPosition = position;
        rt.GetComponent<Image>().color = new Color32(16, 36, 48, 225);
        rt.gameObject.AddComponent<Outline>().effectColor = new Color32(31, 79, 99, 255);

        var layout = rt.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var panel = rt.GetComponent<ReviewPanel>();
        panel.mTitle = panel.Block(rt, 30, UiKit.TextLight);
        panel.mTitle.fontStyle = FontStyle.Bold;
        panel.mTitle.text = "Разбор партии";
        panel.mMove = panel.Block(rt, 22, UiKit.TextLight);
        panel.mSummary = panel.Block(rt, 20, new Color(0.8f, 0.85f, 0.9f, 1f));
        return panel;
    }

    private Text Block(Transform parent, int size, Color color)
    {
        var t = UiKit.CreateText("Text", parent, "", size, color, TextAnchor.UpperLeft, shadow: false);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.supportRichText = true;
        t.lineSpacing = 1.05f;
        return t;
    }

    public void Show(string move, string summary)
    {
        mMove.text = move;
        mSummary.text = summary;
    }
}
