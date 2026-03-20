using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class LoadingScreenController : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI loadingText;

    private static LoadingScreenController instance;
    private Coroutine currentFadeCoroutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // Инициализация
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (loadingText == null) loadingText = GetComponentInChildren<TextMeshProUGUI>();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public static void Show(string text = "Загрузка...", float minDuration = 1.0f, bool isSceneLoad = true)
    {
        if (instance == null) return;

        instance.loadingText.text = text;

        if (currentFadeCoroutine != null) instance.StopCoroutine(currentFadeCoroutine);

        if (isSceneLoad)
        {
            // Логика загрузки сцены: Появиться -> Ждать мин. время -> Исчезнуть
            currentFadeCoroutine = instance.StartCoroutine(instance.FadeInWaitAndOut(minDuration));
        }
        else
        {
            // Логика перехода внутри сцены: Появиться (1с) -> Ждать (1с) -> Исчезнуть (1с)
            currentFadeCoroutine = instance.StartCoroutine(instance.FadeTransitionSequence());
        }
    }

    // Корутин для загрузки сцены
    IEnumerator FadeInWaitAndOut(float minDuration)
    {
        float startTime = Time.time;

        // Плавное появление (быстро, за 0.2с)
        while (canvasGroup.alpha < 1f)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.deltaTime * 5f);
            yield return null;
        }
        canvasGroup.blocksRaycasts = true; // Блокируем клики пока видно

        // Ждем пока пройдет минимальное время
        while (Time.time - startTime < minDuration)
        {
            yield return null;
        }

        // Плавное исчезновение
        while (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.deltaTime * 2f);
            yield return null;
        }

        canvasGroup.blocksRaycasts = false;
    }

    // Корутин для перехода между главами (фиксированное время)
    IEnumerator FadeTransitionSequence()
    {
        // 1. Появление за 1 секунду
        float duration = 1.0f;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / duration);
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.1f;
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 2. Ожидание 1 секунда
        yield return new WaitForSeconds(1.0f);

        // 3. Исчезновение за 1 секунду
        timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / duration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }
}