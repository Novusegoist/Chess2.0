using UnityEngine;

/// <summary>
/// Пересборка интерфейса после смены языка: ждёт, пока старый объект игры уничтожится
/// (это происходит в конце кадра), и создаёт его заново через MenuManager.Bootstrap.
/// </summary>
public class LanguageReload : MonoBehaviour
{
    void Update()
    {
        if (FindAnyObjectByType<GameManager>() != null)
            return;
        MenuManager.Bootstrap();
        Destroy(gameObject);
    }
}
