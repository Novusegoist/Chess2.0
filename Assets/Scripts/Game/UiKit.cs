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
    public static readonly Color Background = new Color32(38, 44, 40, 255);

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
        scaler.matchWidthOrHeight = 1f; // по высоте — доска и меню всегда влезают
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
