using UnityEngine;

public class WaterManager : MonoBehaviour
{
    public static WaterManager Instance;

    [Header("Настройки воды")]
    public Material waterMaterial; // Перетащи сюда свой WaterMaterial
    public float baseWaterLevel = 10f; // Базовый уровень воды для обычной карты

    private GameObject waterObject;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Установить уровень воды для конкретной темы.
    /// Вызывать после загрузки ландшафта.
    /// </summary>
    /// <param name="themeId">ID темы (forest, swamp, mountains)</param>
    /// <param name="mapHeight">Максимальная высота меша (из MapGenerator3D.mapHeight)</param>
    public void SetWaterLevelForTheme(string themeId, float mapHeight)
    {
        float level = baseWaterLevel;

        // 1. Пытаемся получить точное значение из конфига темы
        MapTheme config = StoryContentLoader.GetThemeById(themeId);

        if (config != null)
        {
            // Конвертируем множитель высоты в абсолютный уровень, если нужно, 
            // или берем готовый waterLevel, если он есть в твоем классе MapTheme.
            // Судя по твоему JSON, там есть поле "waterLevel". Убедись, что оно есть в классе MapTheme в StoryData.cs

            // Если в классе MapTheme есть поле waterLevel:
            level = config.waterLevel;

            Debug.Log($"[Water] Используем уровень воды из темы '{themeId}': {level}");
        }
        else
        {
            // Фоллбэк, если тема не найдена (старая логика)
            string t = themeId.ToLower();
            switch (t)
            {
                case "swamp": level = 20f; break;
                case "forest": level = 15f; break; // Тут было расхождение с JSON (8.0)
                case "mountains": level = 10f; break;
                default: level = baseWaterLevel; break;
            }
            Debug.LogWarning($"[Water] Тема '{themeId}' не найдена, используем дефолт: {level}");
        }

        CreateOrUpdateWater(level);
    }

    /// <summary>
    /// Создать воду или обновить позицию существующей.
    /// </summary>
    void CreateOrUpdateWater(float height)
    {
        if (waterObject == null)
        {
            // Создаем плоскость
            waterObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterObject.name = "GlobalWater";

            // Удаляем коллайдер, если он мешает (или оставляем, если нужна физика воды)
            // Collider collider = waterObject.GetComponent<Collider>();
            // if (collider != null) Destroy(collider);

            // Назначаем материал
            Renderer rend = waterObject.GetComponent<Renderer>();
            if (rend != null && waterMaterial != null)
            {
                rend.material = waterMaterial;
            }
            else if (rend != null)
            {
                Debug.LogWarning("[Water] Материал не назначен! Вода будет белой.");
            }
        }

        // Позиционируем воду
        // Plane в Unity имеет размер 10x10 единиц. Нам нужно растянуть её под размер карты.
        // Карта у нас 200x200. Значит масштаб должен быть 20.
        waterObject.transform.localScale = new Vector3(20f, 1f, 20f); // 20 * 10 = 200

        // Ставим высоту. Y = уровень воды.
        waterObject.transform.position = new Vector3(100f, height, 100f); // Центр карты (100, 100)
    }

    // Опционально: анимация волн (простое покачивание)
    private void Update()
    {
        if (waterObject != null)
        {
            // Легкое покачивание вверх-вниз на 0.05 единиц
            float wave = Mathf.Sin(Time.time * 2f) * 0.05f;
            Vector3 pos = waterObject.transform.position;
            pos.y = (baseWaterLevel > 0 ? baseWaterLevel : 10f) + wave; // Упрощено, лучше хранить целевую высоту отдельно
            // Для продакшена лучше не менять Y каждый кадр, если не используешь шейдер волн
        }
    }
}