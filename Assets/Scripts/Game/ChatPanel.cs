using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Чат сетевой партии слева от доски: история, поле ввода, быстрые фразы и эмодзи.
/// Сам ничего не отправляет — зовёт sendText / sendEmote, которые даёт GameManager.
/// </summary>
public class ChatPanel : MonoBehaviour
{
    /// <summary>Быстрые фразы (одно нажатие — сообщение).</summary>
    public static readonly string[] QuickPhrases =
    {
        "Привет!", "Удачи!", "Хороший ход!", "Ой…",
        "Спасибо!", "Думаю…", "Хорошая игра!", "Ещё партию?"
    };

    /// <summary>
    /// Эмодзи OpenMoji (openmoji.org, CC BY-SA 4.0): 😀 😂 😅 😢 😠 😮 😎 🤔 👍 ❤️ 🔥 👻.
    /// Номер = индекс файла emoji_NN и номер в сетевом протоколе — порядок не менять.
    /// </summary>
    public const int EmojiCount = 12;
    public const float Width = 360f;

    private Action<string> mSendText;
    private Action<int> mSendEmote;
    private RectTransform mContent;
    private ScrollRect mScroll;
    private InputField mInput;
    private GameObject mPhrasesPopup;
    private GameObject mEmojiPopup;
    private RectTransform mFloatLayer;
    private Text mTitle;
    private string mTitleShown;
    private static Sprite[] sEmoji;

    public static Sprite Emoji(int id)
    {
        if (sEmoji == null)
        {
            sEmoji = new Sprite[EmojiCount];
            for (int i = 0; i < EmojiCount; i++)
                sEmoji[i] = UiKit.LoadSprite($"UI/Emoji/emoji_{i:00}");
        }
        return id >= 0 && id < EmojiCount ? sEmoji[id] : null;
    }

    /// <summary>Собрать панель. floatLayer — где всплывают крупные эмодзи (поверх доски).</summary>
    public static ChatPanel Create(Transform parent, Vector2 position, RectTransform floatLayer,
        Action<string> sendText, Action<int> sendEmote)
    {
        var rt = UiKit.CreateUi("Chat", parent, typeof(Image), typeof(ChatPanel));
        rt.sizeDelta = new Vector2(Width, 700);
        rt.anchoredPosition = position;
        var back = rt.GetComponent<Image>();
        back.color = new Color32(28, 33, 30, 215);
        rt.gameObject.AddComponent<Outline>().effectColor = new Color32(0, 0, 0, 110);

        var chat = rt.GetComponent<ChatPanel>();
        chat.mSendText = sendText;
        chat.mSendEmote = sendEmote;
        chat.mFloatLayer = floatLayer;
        chat.Build(rt);
        return chat;
    }

    /// <summary>Очистить историю (новая партия идёт в той же истории — очищаем только при новом сопернике).</summary>
    public void Clear()
    {
        for (int i = mContent.childCount - 1; i >= 0; i--)
            Destroy(mContent.GetChild(i).gameObject);
        ClosePopups();
    }

    /// <summary>Добавить сообщение в историю.</summary>
    public void AddText(string who, bool mine, string text)
    {
        var t = UiKit.CreateText("Msg", mContent, "", 24, UiKit.TextLight, TextAnchor.UpperLeft, shadow: false);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.supportRichText = true;
        // Текст соперника экранируем, чтобы он не мог подсунуть свою разметку.
        t.text = $"<color={(mine ? "#8fd0ff" : "#ffb86b")}><b>{who}:</b></color> {Escape(text)}";
        ScrollToBottom();
    }

    /// <summary>Добавить эмодзи в историю и показать его крупно.</summary>
    public void AddEmote(string who, bool mine, int id, Vector2 floatPosition)
    {
        Sprite sprite = Emoji(id);
        if (sprite == null)
            return;

        var row = UiKit.CreateUi("Emote", mContent, typeof(HorizontalLayoutGroup));
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var name = UiKit.CreateText("Who", row, $"<color={(mine ? "#8fd0ff" : "#ffb86b")}><b>{who}:</b></color>",
            24, UiKit.TextLight, TextAnchor.MiddleLeft, shadow: false);
        name.horizontalOverflow = HorizontalWrapMode.Overflow;
        var img = UiKit.CreateUi("Emoji", row, typeof(Image), typeof(LayoutElement));
        img.GetComponent<Image>().sprite = sprite;
        img.GetComponent<Image>().preserveAspect = true;
        var le = img.GetComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = 40;

        ScrollToBottom();
        StartCoroutine(FloatEmote(sprite, floatPosition));
    }

    /// <summary>Состояние связи в заголовке: «Чат · 34 мс» или предупреждение.</summary>
    public void SetConnection(bool connected, int? pingMs, bool shaky)
    {
        string text;
        if (!connected)
            text = L.T("Чат · <color=#ff7a6b>нет связи</color>");
        else if (shaky)
            text = L.T("Чат · <color=#ffb84d>связь нестабильна…</color>");
        else if (pingMs.HasValue)
        {
            string color = pingMs < 100 ? "#7fdc8a" : pingMs < 250 ? "#ffd166" : "#ff9f6b";
            text = L.F("Чат · <color={0}>{1} мс</color>", color, pingMs);
        }
        else
            text = L.T("Чат");

        if (text == mTitleShown)
            return;
        mTitleShown = text;
        mTitle.text = text;
    }

    /// <summary>Системная строка серым («Соперник подключился» и т.п.).</summary>
    public void AddSystem(string text)
    {
        var t = UiKit.CreateText("Sys", mContent, Escape(text), 20, new Color(0.65f, 0.7f, 0.65f, 1f),
            TextAnchor.UpperLeft, shadow: false);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.fontStyle = FontStyle.Italic;
        ScrollToBottom();
    }

    #region Сборка

    private void Build(RectTransform root)
    {
        var title = UiKit.CreateText("Title", root, L.T("Чат"), 28, UiKit.TextLight);
        title.fontStyle = FontStyle.Bold;
        title.supportRichText = true;
        mTitle = title;
        var trt = (RectTransform)title.transform;
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, -6);
        trt.sizeDelta = new Vector2(0, 40);

        // История с прокруткой.
        var view = UiKit.CreateUi("History", root, typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        view.anchorMin = new Vector2(0, 0);
        view.anchorMax = new Vector2(1, 1);
        view.offsetMin = new Vector2(10, 150);
        view.offsetMax = new Vector2(-10, -50);
        view.GetComponent<Image>().color = new Color32(18, 22, 20, 200);

        mContent = UiKit.CreateUi("Content", view, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        mContent.anchorMin = new Vector2(0, 1);
        mContent.anchorMax = new Vector2(1, 1);
        mContent.pivot = new Vector2(0.5f, 1);
        mContent.offsetMin = mContent.offsetMax = Vector2.zero;
        var vl = mContent.GetComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(10, 10, 8, 8);
        vl.spacing = 6;
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;
        mContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        mScroll = view.GetComponent<ScrollRect>();
        mScroll.content = mContent;
        mScroll.viewport = view;
        mScroll.horizontal = false;
        mScroll.movementType = ScrollRect.MovementType.Clamped;
        mScroll.scrollSensitivity = 30;

        // Поле ввода и «отправить».
        mInput = UiKit.CreateInputField(root, new Vector2(Width - 110, 52), L.T("Сообщение…"), 24);
        var irt = (RectTransform)mInput.transform;
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0, 0);
        irt.anchoredPosition = new Vector2(10, 84);
        mInput.characterLimit = Chess2.Core.Net.NetMatch.MaxChatLength;
        // Enter — отправить и оставить фокус, чтобы писать дальше (уход фокуса кликом — не отправка).
        mInput.onEndEdit.AddListener(_ =>
        {
            // На телефоне — кнопка «Готово» экранной клавиатуры.
            bool done = mInput.touchScreenKeyboard != null
                        && mInput.touchScreenKeyboard.status == TouchScreenKeyboard.Status.Done;
            if (!EnterPressed() && !done)
                return;
            Submit();
            if (!done)
                mInput.ActivateInputField();
        });

        var send = UiKit.CreatePlateButton(root, "»", new Vector2(80, 52), Submit, 36);
        var srt = (RectTransform)send.transform;
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1, 0);
        srt.anchoredPosition = new Vector2(-10, 84);

        // Быстрые фразы и эмодзи.
        var phrases = UiKit.CreatePlateButton(root, L.T("Фразы"), new Vector2(Width - 110, 56), () => Toggle(mPhrasesPopup), 28);
        var prt = (RectTransform)phrases.transform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0, 0);
        prt.anchoredPosition = new Vector2(10, 14);

        var emojiBtn = UiKit.CreateUi("EmojiButton", root, typeof(Image), typeof(Button), typeof(MenuButton));
        emojiBtn.anchorMin = emojiBtn.anchorMax = emojiBtn.pivot = new Vector2(1, 0);
        emojiBtn.anchoredPosition = new Vector2(-18, 14);
        emojiBtn.sizeDelta = new Vector2(60, 60);
        var eimg = emojiBtn.GetComponent<Image>();
        eimg.sprite = Emoji(0);
        eimg.preserveAspect = true;
        emojiBtn.GetComponent<MenuButton>().Init(eimg, null, () => Toggle(mEmojiPopup));

        mPhrasesPopup = BuildPhrases(root);
        mEmojiPopup = BuildEmojiGrid(root);
        ClosePopups();
    }

    private GameObject BuildPhrases(RectTransform root)
    {
        var popup = Popup(root, "Phrases", 4 * 54 + 16);
        var grid = popup.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2((Width - 20 - 24) / 2, 48);
        grid.spacing = new Vector2(8, 6);
        grid.padding = new RectOffset(8, 8, 8, 8);
        foreach (string phrase in QuickPhrases)
        {
            string p = L.T(phrase);
            UiKit.CreatePlateButton(popup, p, grid.cellSize, () =>
            {
                mSendText?.Invoke(p);
                ClosePopups();
            }, 22);
        }
        return popup.gameObject;
    }

    private GameObject BuildEmojiGrid(RectTransform root)
    {
        var popup = Popup(root, "EmojiGrid", 3 * 72 + 16);
        var grid = popup.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(64, 64);
        grid.spacing = new Vector2(8, 8);
        grid.padding = new RectOffset(10, 10, 8, 8);
        for (int i = 0; i < EmojiCount; i++)
        {
            int id = i;
            var btn = UiKit.CreateUi("Emoji " + id, popup, typeof(Image), typeof(Button), typeof(MenuButton));
            var img = btn.GetComponent<Image>();
            img.sprite = Emoji(id);
            img.preserveAspect = true;
            btn.GetComponent<MenuButton>().Init(img, null, () =>
            {
                mSendEmote?.Invoke(id);
                ClosePopups();
            });
        }
        return popup.gameObject;
    }

    /// <summary>Всплывающая панель над строкой ввода.</summary>
    private static RectTransform Popup(RectTransform root, string name, float height)
    {
        var rt = UiKit.CreateUi(name, root, typeof(Image));
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 146);
        rt.sizeDelta = new Vector2(Width - 16, height);
        rt.GetComponent<Image>().color = new Color32(40, 47, 43, 250);
        rt.gameObject.AddComponent<Outline>().effectColor = new Color32(0, 0, 0, 160);
        return rt;
    }

    #endregion

    private void Submit()
    {
        string text = mInput.text;
        mInput.text = "";
        if (!string.IsNullOrWhiteSpace(text))
            mSendText?.Invoke(text);
    }

    private void Toggle(GameObject popup)
    {
        bool open = !popup.activeSelf;
        ClosePopups();
        popup.SetActive(open);
        if (open)
            popup.transform.SetAsLastSibling();
    }

    private void ClosePopups()
    {
        if (mPhrasesPopup != null) mPhrasesPopup.SetActive(false);
        if (mEmojiPopup != null) mEmojiPopup.SetActive(false);
    }

    private void ScrollToBottom()
    {
        if (isActiveAndEnabled)
            StartCoroutine(ScrollNextFrame());
    }

    private IEnumerator ScrollNextFrame()
    {
        yield return null; // раскладка пересчитается в конце кадра
        Canvas.ForceUpdateCanvases();
        mScroll.verticalNormalizedPosition = 0f;
    }

    /// <summary>Крупный эмодзи: выпрыгивает, немного всплывает и тает.</summary>
    private IEnumerator FloatEmote(Sprite sprite, Vector2 position)
    {
        if (mFloatLayer == null)
            yield break;
        var rt = UiKit.CreateUi("Float Emoji", mFloatLayer, typeof(Image));
        rt.sizeDelta = new Vector2(130, 130);
        rt.anchoredPosition = position;
        var img = rt.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        const float duration = 2.2f;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float pop = k < 0.12f ? Mathf.SmoothStep(0.2f, 1.15f, k / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Min(1f, (k - 0.12f) / 0.1f));
            rt.localScale = Vector3.one * pop;
            rt.anchoredPosition = position + new Vector2(0, 40f * k);
            img.color = new Color(1, 1, 1, k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    private static string Escape(string text) =>
        text.Replace("<", "‹").Replace(">", "›"); // без rich-text разметки от соперника

    private static bool EnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }
}
