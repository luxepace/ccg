using UnityEngine;
using System.IO;

public class MapGenerator3D : MonoBehaviour
{
    public static MapGenerator3D Instance;

    [Header("Настройки генерации")]
    public int mapSize = 100;
    public int mapHeight = 50;
    public float noiseScale = 20f;
    public float heightMultiplier = 15f;

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
        // Ищем Terrain надёжным способом
        terrain = FindObjectOfType<Terrain>();

        if (terrain != null)
        {
            // Центрируем Terrain ПЕРЕД изменением размера
            terrain.transform.position = Vector3.zero;

            terrainData = terrain.terrainData;
            terrainData.size = new Vector3(mapSize, mapHeight, mapSize);

            Debug.Log($"Terrain найден. Позиция: {terrain.transform.position}");

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

    public void GenerateNewMap()
    {
        Debug.Log("Генерация новой карты...");

        float[,] heights = GenerateHeights();
        terrainData.SetHeights(0, 0, heights);

        SaveMap(heights);
    }

    float[,] GenerateHeights()
    {
        int resolution = terrainData.heightmapResolution;
        float[,] heights = new float[resolution, resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float xCoord = (float)x / resolution * noiseScale;
                float yCoord = (float)y / resolution * noiseScale;

                float noise = Mathf.PerlinNoise(xCoord, yCoord);
                heights[y, x] = noise * heightMultiplier / mapHeight;
            }
        }

        return heights;
    }

    bool HasSavedMap()
    {
        string path = GetSavePath();
        return File.Exists(path);
    }

    public void LoadMap()
    {
        Debug.Log("Загрузка сохранённой карты...");

        string path = GetSavePath();
        string json = File.ReadAllText(path);

        MapSaveData saveData = JsonUtility.FromJson<MapSaveData>(json);

        if (saveData == null || saveData.heights == null)
        {
            Debug.LogError("Не удалось загрузить данные карты!");
            GenerateNewMap();
            return;
        }

        int width = saveData.width;
        int height = saveData.height;

        float[,] heights = new float[width, height];

        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < height; x++)
            {
                heights[y, x] = saveData.heights[y * height + x];
            }
        }

        terrainData.SetHeights(0, 0, heights);
    }

    void SaveMap(float[,] heights)
    {
        Debug.Log("Сохранение карты...");

        MapSaveData saveData = new MapSaveData();

        int width = heights.GetLength(0);
        int height = heights.GetLength(1);

        saveData.width = width;
        saveData.height = height;
        saveData.heights = new float[width * height];

        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < height; x++)
            {
                saveData.heights[y * height + x] = heights[y, x];
            }
        }

        string json = JsonUtility.ToJson(saveData, true);
        string path = GetSavePath();

        File.WriteAllText(path, json);
        Debug.Log($"Карта сохранена в {path}");
    }

    string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, saveFileName);
    }

    public void DeleteSave()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Сохранение карты удалено");
        }
    }
}

[System.Serializable]
public class MapSaveData
{
    public int width;
    public int height;
    public float[] heights;
}