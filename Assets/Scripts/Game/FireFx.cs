using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Огонь за фигурой, которая получила способность. Проигрывает кадры из Resources/Fx/Fire по кругу.
/// Лежит в отдельном слое под фигурами и ходит за своей фигурой (см. PieceView).
/// </summary>
public class FireFx : MonoBehaviour
{
    public const float FramesPerSecond = 30f;

    public RectTransform Rect { get; private set; }

    private Sprite[] mFrames;
    private Image mImage;
    private float mPhase;

    public void Init(Sprite[] frames)
    {
        mFrames = frames;
        Rect = (RectTransform)transform;
        mImage = GetComponent<Image>();
        mImage.raycastTarget = false;
        mImage.preserveAspect = true;
        // Разная фаза — чтобы огоньки разных фигур не мигали синхронно.
        mPhase = Random.value * frames.Length;
        Update();
    }

    void Update()
    {
        if (mFrames == null || mFrames.Length == 0)
            return;
        int frame = (int)(Time.time * FramesPerSecond + mPhase) % mFrames.Length;
        mImage.sprite = mFrames[frame];
    }
}
