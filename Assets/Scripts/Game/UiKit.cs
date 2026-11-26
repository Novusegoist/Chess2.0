using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Помощники для сборки интерфейса из кода (без префабов).</summary>
public static class UiKit
{
    public static readonly Color TextDark = new Color32(31, 79, 99, 255);
    public static readonly Color TextLight = new Color32(235, 235, 225, 255);
    public static readonly Color Background = new Color32(18, 38, 50, 255); // под фоны в цветах кнопок

    private static readonly Dictionary<string, Sprite> sSprites = new Dictionary<string, Sprite>();
    private static Font sFont;

    public static Font DefaultFont => sFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static Sprite LoadSprite(string path)
    {
        if (sSprites.TryGetValue(path, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            else
                Debug.LogError("Нет картинки Resources/" + path + ".png");
        }
        sSprites[path] = sprite;
        return sprite;
    }

    public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        // Expand: область 1600×900 видна целиком при любом размере окна —
        // в узком окне ничего не уезжает за край (окно можно тянуть мышкой).
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        // Стрелка «назад» — это стрелка «вперёд», развёрнутая на 180°. По умолчанию Unity не даёт
        // нажимать такие «развёрнутые задом» элементы, из-за этого кнопка «назад» не работала.
        go.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = false;
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    public static RectTransform CreateUi(string name, Transform parent, params Type[] components)
    {
        var types = new Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        components.CopyTo(types, 1);
        var go = new GameObject(name, types);
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>
    /// Картинка-фон из Resources/UI/Backgrounds: заполняет весь родитель без искажений
    /// (лишнее по краям обрезается), клики не перехватывает.
    /// </summary>
    public static Image CreateBackdrop(Transform parent, string name)
    {
        Sprite sprite = LoadSprite("UI/Backgrounds/" + name);
        if (sprite == null)
            return null;

        // Маска по размеру родителя, внутри — картинка с сохранением пропорций «на вырост».
        var clip = Stretch(CreateUi("Backdrop", parent, typeof(RectMask2D)));
        clip.SetAsFirstSibling();
        var rt = CreateUi("Image", clip, typeof(Image), typeof(AspectRatioFitter));
        var img = rt.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        var fitter = rt.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        return img;
    }

    public static Image CreatePanel(string name, Transform parent, Color color)
    {
        var rt = Stretch(CreateUi(name, parent, typeof(Image)));
        var img = rt.GetComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text CreateText(string name, Transform parent, string text, int size, Color color,
        TextAnchor align = TextAnchor.MiddleCenter, bool shadow = true)
    {
        var rt = CreateUi(name, parent, typeof(Text));
        var t = rt.GetComponent<Text>();
        t.font = DefaultFont;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (shadow)
            rt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
        return t;
    }

    /// <summary>Вертикальная колонка с автоматической раскладкой детей.</summary>
    public static RectTransform CreateColumn(string name, Transform parent, float spacing)
    {
        var rt = CreateUi(name, parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var layout = rt.GetComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = rt.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    public static RectTransform CreateRow(string name, Transform parent, float spacing)
    {
        var rt = CreateUi(name, parent, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        var layout = rt.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = rt.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    /// <summary>Строка настроек: подпись слева, элемент управления справа. Возвращает место под элемент.</summary>
    public static RectTransform CreateSettingRow(Transform parent, string label, float height = 58f)
    {
        var row = CreateUi("Row " + label, parent);
        row.sizeDelta = new Vector2(1000, height);

        var text = CreateText("Label", row, label, 30, TextLight, TextAnchor.MiddleRight);
        var trt = (RectTransform)text.transform;
        trt.anchorMin = new Vector2(0, 0);
        trt.anchorMax = new Vector2(0.5f, 1);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = new Vector2(-24, 0);

        var slot = CreateUi("Control", row);
        slot.anchorMin = new Vector2(0.5f, 0);
        slot.anchorMax = new Vector2(1, 1);
        slot.offsetMin = Vector2.zero;
        slot.offsetMax = Vector2.zero;
        return slot;
    }

    /// <summary>Переключатель «&lt; значение &gt;». step(-1) / step(+1) — листать варианты.</summary>
    public static Text CreateSelector(RectTransform slot, Action<int> step)
    {
        var left = CreatePlateButton(slot, "<", new Vector2(56, 52), () => step(-1), 34);
        Place((RectTransform)left.transform, new Vector2(0, 0.5f), new Vector2(28, 0));

        var value = CreateText("Value", slot, "", 30, TextLight);
        var vrt = (RectTransform)value.transform;
        vrt.anchorMin = vrt.anchorMax = new Vector2(0, 0.5f);
        vrt.sizeDelta = new Vector2(300, 52);
        vrt.anchoredPosition = new Vector2(56 + 150 + 8, 0);

        var right = CreatePlateButton(slot, ">", new Vector2(56, 52), () => step(+1), 34);
        Place((RectTransform)right.transform, new Vector2(0, 0.5f), new Vector2(56 + 300 + 16 + 28, 0));
        return value;
    }

    /// <summary>Ползунок с подписью значения справа (например, «80%»).</summary>
    public static Slider CreateSlider(RectTransform slot, float min, float max, Action<float> changed, out Text valueText)
    {
        var rt = CreateUi("Slider", slot, typeof(Slider));
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.sizeDelta = new Vector2(330, 30);
        rt.anchoredPosition = new Vector2(8, 0);

        var back = CreateUi("Background", rt, typeof(Image));
        Stretch(back).offsetMin = new Vector2(0, 9);
        back.offsetMax = new Vector2(0, -9);
        back.GetComponent<Image>().color = new Color32(20, 24, 22, 255);

        var fillArea = CreateUi("Fill Area", rt);
        Stretch(fillArea).offsetMin = new Vector2(0, 9);
        fillArea.offsetMax = new Vector2(0, -9);
        var fill = CreateUi("Fill", fillArea, typeof(Image));
        fill.GetComponent<Image>().color = new Color32(0, 165, 230, 255);
        fill.sizeDelta = Vector2.zero;

        var handleArea = CreateUi("Handle Area", rt);
        Stretch(handleArea).offsetMin = new Vector2(12, 0);
        handleArea.offsetMax = new Vector2(-12, 0);
        var handle = CreateUi("Handle", handleArea, typeof(Image));
        handle.sizeDelta = new Vector2(24, 0);
        var handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color32(214, 240, 242, 255);
        handle.gameObject.AddComponent<Outline>().effectColor = TextDark;

        var slider = rt.GetComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        var nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;
        slider.onValueChanged.AddListener(v => changed(v));

        valueText = CreateText("Value", slot, "", 28, TextLight, TextAnchor.MiddleLeft);
        var vrt = (RectTransform)valueText.transform;
        vrt.anchorMin = vrt.anchorMax = new Vector2(0, 0.5f);
        vrt.pivot = new Vector2(0, 0.5f);
        vrt.sizeDelta = new Vector2(120, 40);
        vrt.anchoredPosition = new Vector2(8 + 330 + 18, 0);
        return slider;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = position;
    }

    /// <summary>Поле ввода текста (например, IP-адреса).</summary>
    public static InputField CreateInputField(Transform parent, Vector2 size, string placeholder, int fontSize = 32)
    {
        var rt = CreateUi("Input", parent, typeof(Image), typeof(InputField));
        rt.sizeDelta = size;
        var back = rt.GetComponent<Image>();
        back.color = new Color32(235, 238, 230, 255);
        rt.gameObject.AddComponent<Outline>().effectColor = TextDark;

        Text MakeText(string name, string value, Color color, FontStyle style)
        {
            var t = CreateText(name, rt, value, fontSize, color, TextAnchor.MiddleLeft, shadow: false);
            var trt = (RectTransform)t.transform;
            Stretch(trt);
            trt.offsetMin = new Vector2(18, 4);
            trt.offsetMax = new Vector2(-18, -4);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.fontStyle = style;
            return t;
        }

        var placeholderText = MakeText("Placeholder", placeholder, new Color(0.45f, 0.48f, 0.45f, 1f), FontStyle.Italic);
        var text = MakeText("Text", "", TextDark, FontStyle.Normal);
        text.supportRichText = false;

        var field = rt.GetComponent<InputField>();
        field.textComponent = text;
        field.placeholder = placeholderText;
        field.targetGraphic = back;
        field.lineType = InputField.LineType.SingleLine;
        return field;
    }

    /// <summary>Кнопка-картинка из набора (текст уже нарисован на ней).</summary>
    public static MenuButton CreateImageButton(Transform parent, string sprite, float height, Action onClick)
    {
        Sprite s = LoadSprite("UI/" + sprite);
        float width = s != null ? height * s.rect.width / s.rect.height : height * 4f;
        var rt = CreateUi("Btn " + sprite, parent, typeof(Image), typeof(Button), typeof(MenuButton));
        rt.sizeDelta = new Vector2(width, height);
        var img = rt.GetComponent<Image>();
        img.sprite = s;
        img.preserveAspect = true;
        var btn = rt.GetComponent<MenuButton>();
        btn.Init(img, null, onClick);
        return btn;
    }

    /// <summary>Пустая плашка с текстом из кода — для выбора сложности, цвета и т.п.</summary>
    public static MenuButton CreatePlateButton(Transform parent, string label, Vector2 size, Action onClick, int fontSize = 30)
    {
        Sprite s = LoadSprite("UI/plate");
        var rt = CreateUi("Btn " + label, parent, typeof(Image), typeof(Button), typeof(MenuButton));
        rt.sizeDelta = size;
        var img = rt.GetComponent<Image>();
        img.sprite = s;
        if (s != null && s.border != Vector4.zero)
        {
            // Сохраняем толщину рамки как у больших кнопок, как бы ни растягивали плашку.
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = s.rect.height / size.y;
        }

        // Под стиль надписей на готовых кнопках: голубой текст, светлая обводка, тонкий тёмный контур.
        var text = CreateText("Label", rt, label, fontSize, new Color32(0, 160, 240, 255), TextAnchor.MiddleCenter, shadow: false);
        Stretch((RectTransform)text.transform);
        text.fontStyle = FontStyle.Bold;
        var light = text.gameObject.AddComponent<Outline>();
        light.effectColor = new Color32(214, 240, 242, 255);
        light.effectDistance = new Vector2(2.5f, -2.5f);
        var dark = text.gameObject.AddComponent<Outline>();
        dark.effectColor = new Color32(31, 79, 99, 160);
        dark.effectDistance = new Vector2(1f, -1f);

        var btn = rt.GetComponent<MenuButton>();
        btn.Init(img, text, onClick);
        return btn;
    }
}
