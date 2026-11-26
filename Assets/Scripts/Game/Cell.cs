using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Клетка доски: фон, метка подсветки и клик.</summary>
public class Cell : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector] public int mSquare;
    [HideInInspector] public RectTransform mRectTransform;
    [HideInInspector] public Image mImage;
    [HideInInspector] public Image mMarker;

    private GameManager mGameManager;

    public void Setup(int square, GameManager gameManager, Image marker)
    {
        mSquare = square;
        mGameManager = gameManager;
        mRectTransform = (RectTransform)transform;
        mImage = GetComponent<Image>();
        mImage.raycastTarget = true;
        mMarker = marker;
        mMarker.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        mGameManager.OnSquareClicked(mSquare);
    }
}
