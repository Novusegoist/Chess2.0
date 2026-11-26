using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Поведение кнопки меню: увеличение при наведении, «нажатие», режим «выбрано».</summary>
public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Text Label { get; private set; }

    private Image mImage;
    private Button mButton;
    private bool mHover;
    private bool mPressed;
    private bool mSelected;
    private bool mSelectable;

    public void Init(Image image, Text label, Action onClick)
    {
        mImage = image;
        Label = label;
        mButton = GetComponent<Button>();
        mButton.transition = Selectable.Transition.None;
        var nav = mButton.navigation;
        nav.mode = Navigation.Mode.None;
        mButton.navigation = nav;
        if (onClick != null)
            mButton.onClick.AddListener(() => onClick());
        Refresh();
    }

    /// <summary>Для групп «выбери один вариант»: невыбранные кнопки приглушены.</summary>
    public void SetSelected(bool selected)
    {
        mSelectable = true;
        mSelected = selected;
        Refresh();
    }

    public void SetText(string text)
    {
        if (Label != null)
            Label.text = text;
    }

    public void SetSprite(Sprite sprite) => mImage.sprite = sprite;

    public void OnPointerEnter(PointerEventData e) { mHover = true; Refresh(); }
    public void OnPointerExit(PointerEventData e) { mHover = false; mPressed = false; Refresh(); }
    public void OnPointerDown(PointerEventData e) { mPressed = true; Refresh(); }
    public void OnPointerUp(PointerEventData e) { mPressed = false; Refresh(); }

    void OnDisable()
    {
        mHover = mPressed = false;
        Refresh();
    }

    private void Refresh()
    {
        if (mImage == null)
            return;
        float scale = mPressed ? 0.95f : mHover ? 1.05f : 1f;
        if (mSelectable && mSelected)
            scale *= 1.04f;
        transform.localScale = Vector3.one * scale;

        Color c = Color.white;
        if (mSelectable && !mSelected)
            c = new Color(0.62f, 0.62f, 0.66f, 1f);
        if (mHover && !mPressed)
            c = Color.Lerp(c, Color.white, 0.5f) * 1.0f;
        mImage.color = c;
    }
}
