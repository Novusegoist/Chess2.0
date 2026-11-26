using System;
using System.Text;
using Chess2.Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Панель урока слева от доски: что изучаем, шаги, подсказка и кнопки в конце.</summary>
public class LessonPanel : MonoBehaviour
{
    public const float Width = 360f;

    private Text mTitle;
    private Text mIntro;
    private Text mSteps;
    private Text mNote;
    private GameObject mButtons;
    private MenuButton mNextButton;

    public static LessonPanel Create(Transform parent, Vector2 position, Action next, Action toLessons)
    {
        var rt = UiKit.CreateUi("Lesson", parent, typeof(Image), typeof(VerticalLayoutGroup), typeof(LessonPanel));
        rt.sizeDelta = new Vector2(Width, 700);
        rt.anchoredPosition = position;
        rt.GetComponent<Image>().color = new Color32(16, 36, 48, 225);
        rt.gameObject.AddComponent<Outline>().effectColor = new Color32(31, 79, 99, 255);

        var layout = rt.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 16;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var panel = rt.GetComponent<LessonPanel>();
        panel.Build(rt, next, toLessons);
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

    private void Build(RectTransform rt, Action next, Action toLessons)
    {
        mTitle = Block(rt, 30, UiKit.TextLight);
        mTitle.fontStyle = FontStyle.Bold;
        mIntro = Block(rt, 20, new Color(0.82f, 0.86f, 0.9f, 1f));
        mSteps = Block(rt, 21, UiKit.TextLight);
        mNote = Block(rt, 20, new Color32(255, 200, 120, 255));

        // Кнопки в конце урока.
        var buttons = UiKit.CreateColumn("Buttons", rt, 12);
        mButtons = buttons.gameObject;
        mNextButton = UiKit.CreatePlateButton(buttons, "Следующий урок", new Vector2(300, 64), next, 28);
        UiKit.CreatePlateButton(buttons, "К урокам", new Vector2(300, 64), toLessons, 28);
        mButtons.SetActive(false);
    }

    /// <summary>Показать урок. step — текущий шаг (равен числу шагов, если урок пройден).</summary>
    public void Show(Lesson lesson, int step, string note, bool hasNext)
    {
        bool finished = step >= lesson.Steps.Length;
        mTitle.text = lesson.Title;
        mIntro.text = lesson.Intro;

        var sb = new StringBuilder();
        for (int i = 0; i < lesson.Steps.Length; i++)
        {
            LessonStep s = lesson.Steps[i];
            if (i < step)
                sb.Append("<color=#6fd67c>+ ").Append(s.Task).Append("</color>\n<color=#9fb7c4><size=18>")
                    .Append(s.Done).Append("</size></color>");
            else if (i == step)
                sb.Append("<color=#ffd76a><b>→ ").Append(s.Task).Append("</b></color>");
            else
                sb.Append("<color=#7d8c94>• ").Append(s.Task).Append("</color>");
            sb.Append('\n');
        }
        if (finished)
            sb.Append("\n<color=#6fd67c><b>Урок пройден!</b></color>\n").Append(lesson.Outro);
        mSteps.text = sb.ToString();

        mNote.text = note ?? "";
        mNote.gameObject.SetActive(!string.IsNullOrEmpty(note));
        mButtons.SetActive(finished);
        mNextButton.gameObject.SetActive(hasNext);
    }
}
