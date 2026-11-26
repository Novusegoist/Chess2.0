using Chess2.Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Табло шахматных часов одной стороны: подпись и оставшееся время.</summary>
public class ClockPanel : MonoBehaviour
{
    private static readonly Color IdleBack = new Color32(28, 33, 30, 235);
    private static readonly Color ActiveBack = new Color32(70, 82, 72, 255);
    private static readonly Color IdleText = new Color32(170, 178, 170, 255);
    private static readonly Color ActiveText = new Color32(245, 245, 235, 255);
    private static readonly Color LowText = new Color32(255, 90, 80, 255);

    private Image mBack;
    private Text mLabel;
    private Text mTime;
    private long mLastShown = -1;
    private bool mLastActive;
    private string mLastLabel;

    public static ClockPanel Create(Transform parent, Vector2 position)
    {
        var rt = UiKit.CreateUi("Clock", parent, typeof(Image), typeof(ClockPanel));
        rt.sizeDelta = new Vector2(230, 100);
        rt.anchoredPosition = position;

        var panel = rt.GetComponent<ClockPanel>();
        panel.mBack = rt.GetComponent<Image>();
        panel.mBack.raycastTarget = false;
        rt.gameObject.AddComponent<Outline>().effectColor = new Color32(0, 0, 0, 120);

        panel.mLabel = UiKit.CreateText("Label", rt, "", 20, IdleText, TextAnchor.MiddleCenter, shadow: false);
        var lrt = (RectTransform)panel.mLabel.transform;
        lrt.anchorMin = new Vector2(0, 1);
        lrt.anchorMax = new Vector2(1, 1);
        lrt.pivot = new Vector2(0.5f, 1);
        lrt.anchoredPosition = new Vector2(0, -6);
        lrt.sizeDelta = new Vector2(0, 26);

        panel.mTime = UiKit.CreateText("Time", rt, "", 48, IdleText, TextAnchor.MiddleCenter, shadow: false);
        panel.mTime.fontStyle = FontStyle.Bold;
        var trt = (RectTransform)panel.mTime.transform;
        trt.anchorMin = new Vector2(0, 0);
        trt.anchorMax = new Vector2(1, 0);
        trt.pivot = new Vector2(0.5f, 0);
        trt.anchoredPosition = new Vector2(0, 8);
        trt.sizeDelta = new Vector2(0, 60);

        return panel;
    }

    /// <summary>active — сейчас ход этой стороны и часы идут.</summary>
    public void Show(string label, double seconds, bool active)
    {
        // Перерисовываем, только если показания изменились (не каждый кадр).
        long shown = seconds < 10 ? (long)(seconds * 10) : (long)System.Math.Ceiling(seconds) * 10;
        if (shown == mLastShown && active == mLastActive && label == mLastLabel)
            return;
        mLastShown = shown;
        mLastActive = active;
        mLastLabel = label;

        mLabel.text = label;
        mTime.text = ChessClock.Format(seconds);
        mBack.color = active ? ActiveBack : IdleBack;
        mLabel.color = active ? ActiveText : IdleText;
        mTime.color = seconds < 30 ? LowText : active ? ActiveText : IdleText;
    }
}
