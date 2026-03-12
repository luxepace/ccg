using UnityEngine;
using System.IO;

public class MapGenerator3D : MonoBehaviour
{
    public static MapGenerator3D Instance;

    [Header("Настройки генерации")]
    public int mapSize = 200;
    public int mapHeight = 60;
    public float noiseScale = 50f;
    public float heightMultiplier = 25f;

    [Header("Детализация")]
    [Range(1, 6)]
    public int octaves = 3;
    public float persistence = 0.5f;
    public float lacunarity = 2f;

    [Header("Сохранение")]
    public string saveFileName = "mapData.json";

    private Terrain terrain;
    private TerrainData terrainData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        terrain = FindObjectOfType<Terrain>();

        if (terrain != null)
        {
            // Центрируем Terrain
            terrain.transform.position = Vector3.zero;

            terrainData = terrain.terrainData;
            terrainData.size = new Vector3(mapSize, mapHeight, mapSize);

            Debug.Log($"Terrain найден. Позиция: {terrain.transform.position}");
            Debug.Log($"Размер карты: {mapSize}x{mapSize}, Высота: {mapHeight}");

            // Проверяем есть ли сохранение
            if (HasSavedMap())
            {
                LoadMap();
            }
            else
            {
                GenerateNewMap();
            }
        }
        else
        {
            Debug.LogError("Terrain не найден в сцене!");
        }
    }

    /// <summary>
    /// Генерация новой карты
    /// </summary>
    public void GenerateNewMap()
    {
        Debug.Log("Генерация новой карты...");

        float[,] heights = GenerateHeights();
        terrainData.SetHeights(0, 0, heights);

        SaveMap(heights);
    }

    /// <summary>
    /// Генерация высот через шум Перлина с октавами
    /// </summary>
    float[,] GenerateHeights()
    {
        int resolution = terrainData.heightmapResolution;
        float[,] heights = new float[resolution, resolution];

        float maxNoiseHeight = 0;
        float minNoiseHeight = 0;

        // Генерируем шум с октавами для более естественного рельефа
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float noiseHeight = 0;
                float amplitude = 1;
                float frequency = 1;

                for (int i = 0; i < octaves; i++)
                {
                    float xCoord = (float)x / resolution * noiseScale * frequency;
                    float yCoord = (float)y / resolution * noiseScale * frequency;

                    float perlinValue = Mathf.PerlinNoise(xCoord, yCoord) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                // Нормализуем значения
                if (noiseHeight > maxNoiseHeight) maxNoiseHeight = noiseHeight;
                if (noiseHeight < minNoiseHeight) minNoiseHeight = noiseHeight;

                heights[y, x] = noiseHeight;
            }
        }

        // Финальная нормализация и применение множителя высоты
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                heights[y, x] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, heights[y, x]);
                heights[y, x] *= heightMultiplier / mapHeight;
            }
        }

        return heights;
    }

    /// <summary>
    /// Проверка наличия сохранённой карты
    /// </summary>
    bool HasSavedMap()
    {
        string path = GetSavePath();
        return File.Exists(path);
    }

    /// <summary>
    /// Загрузка карты из файла
    /// </summary>
    public void LoadMap()
    {
        Debug.Log("Загрузка сохранённой карты...");

        string path = GetSavePath();
        string json = File.ReadAllText(path);

        MapSaveData saveData = JsonUtility.FromJson<MapSaveData>(json);

        // Проверка на null
        if (saveData == null || saveData.heights == null)
        {
            Debug.LogError("Не удалось загрузить данные карты!");
            GenerateNewMap();
            return;
        }

        int width = saveData.width;
        int height = saveData.height;

        float[,] heights = new float[width, height];

        // Конвертируем 1D → 2D
        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < height; x++)
            {
                heights[y, x] = saveData.heights[y * height + x];
            }
        }

        terrainData.SetHeights(0, 0, heights);
    }

    /// <summary>
    /// Сохранение карты в файл
    /// </summary>
    void SaveMap(float[,] heights)
    {
        Debug.Log("Сохранение карты...");

        MapSaveData saveData = new MapSaveData();

        int width = heights.GetLength(0);
        int height = heights.GetLength(1);

        saveData.width = width;
        saveData.height = height;
        saveData.heights = new float[width * height];

        // Конвертируем 2D → 1D
        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < height; x++)
            {
                saveData.heights[y * height + x] = heights[y, x];
            }
        }

        string json = JsonUtility.ToJson(saveData, true);
        string path = GetSavePath();

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        Debug.Log($"Карта сохранена в {path}");
    }

    string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, saveFileName);
    }

    /// <summary>
    /// Удалить сохранение (для новой игры)
    /// </summary>
    public void DeleteSave()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Сохранение карты удалено");
        }
    }

    /// <summary>
    /// Регенерировать карту (для тестов)
    /// </summary>
    [ContextMenu("Regenerate Map")]
    public void RegenerateMap()
    {
        DeleteSave();
        GenerateNewMap();
    }

    /// <summary>
    /// Показать путь к файлу сохранения
    /// </summary>
    [ContextMenu("Show Save Path")]
    public void ShowSavePath()
    {
        string path = GetSavePath();
        Debug.Log($"Путь к сохранению: {path}");

        // Открыть папку в проводнике (Windows)
#if UNITY_EDITOR_WIN
        System.Diagnostics.Process.Start("explorer.exe", "/select," + path);
#endif
    }
}

/// <summary>
/// Класс для сериализации данных карты
/// </summary>
[System.Serializable]
public class MapSaveData
{
    public int width;
    public int height;
    public float[] heights; // 1D массив вместо 2D
}