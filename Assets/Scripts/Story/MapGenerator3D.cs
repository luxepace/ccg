using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class MapGenerator3D : MonoBehaviour
{
    public static MapGenerator3D Instance;

    [Header("Основные настройки")]
    public int mapSize = 200;
    public int mapHeight = 30;

    [Header("Настройки шума")]
    public float noiseScale = 10f;
    public int octaves = 4;
    public float persistence = 0.5f;
    public float lacunarity = 2f;
    public float offsetX = 0f;
    public float offsetY = 0f;

    public float heightMultiplier = 1.0f;

    [Header("Сглаживание")]
    [Range(0, 5)]
    public int smoothingPasses = 1;

    [Header("Сохранение")]
    private string baseSaveFileName = "map_chapter_";
    private string foldsSaveSuffix = "_folds.json";

    private Terrain terrain;
    private TerrainData terrainData;
    private bool isHeightsLoaded = false;

    public List<FoldLine> currentFolds = new List<FoldLine>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        terrain = FindObjectOfType<Terrain>();
        if (terrain != null)
        {
            terrain.transform.position = Vector3.zero;
            terrainData = terrain.terrainData;
            terrainData.size = new Vector3(mapSize, mapHeight, mapSize);
            Debug.Log($"[MapGen] Terrain готов. Размер: {mapSize}x{mapSize}, Высота: {mapHeight}");
        }
        else
        {
            Debug.LogError("[MapGen] Terrain не найден!");
        }
    }

    public bool IsTerrainReady()
    {
        return terrainData != null && isHeightsLoaded;
    }

    // === НОВЫЙ МЕТОД: Генерация одной главы изолированно ===
    // Используется в цикле StartNewGame: Генерируем ландшафт -> Применяем -> Генерируем ноды
    public void GenerateAndSaveSingleChapterData(int chapterIndex, string themeId)
    {
        SetupThemeParameters(themeId);

        // Фиксируем смещение шума для этой главы
        if (chapterIndex == 0)
        {
            offsetX = Random.Range(0f, 10000f);
            offsetY = Random.Range(0f, 10000f);
        }
        else
        {
            // Для последующих глав можно немного сдвигать оффсет или генерировать новый
            offsetX += Random.Range(1000f, 5000f);
            offsetY += Random.Range(1000f, 5000f);
        }

        Debug.Log($"[MapGen] Генерация ландшафта для главы {chapterIndex} (Тема: {themeId})...");

        // 1. Генерируем высоты и складки
        float[,] heights = GenerateHeights();

        // 2. Сохраняем высоты (JSON)
        string heightFile = GetChapterSavePath(chapterIndex);
        SaveMap(heights, heightFile);

        // 3. Сохраняем складки (JSON)
        string foldsFile = GetFoldsSavePath(chapterIndex);
        SaveFolds(currentFolds, foldsFile);

        // 4. Генерируем базовую текстуру (Бумага + Складки) и сохраняем PNG
        if (MapDecorationManager.Instance != null)
        {
            MapDecorationManager.Instance.GenerateBasePaperTexture(chapterIndex, heights, currentFolds);

            // Генерируем объекты декораций (деревья/камни) и сохраняем JSON
            MapDecorationManager.Instance.GenerateObjectDecorationsOnly(chapterIndex, themeId, currentFolds);
        }

        Debug.Log($"[MapGen] Глава {chapterIndex}: Ландшафт, текстура и декорации сохранены.");
    }

    public void GenerateAndSaveAllChapterMaps(List<ChapterConfig> chapters)
    {
        if (chapters == null || chapters.Count == 0) return;
        Debug.Log("[MapGen] Массовая генерация (устаревший метод, используется только для тестов).");

        offsetX = Random.Range(0f, 10000f);
        offsetY = Random.Range(0f, 10000f);

        foreach (var chapter in chapters)
        {
            GenerateAndSaveSingleChapterData(chapter.chapterIndex, chapter.themeId);
        }
    }

    public void ApplyThemeAndLoadChapter(int chapterIndex, string themeId)
    {
        SetupThemeParameters(themeId);
        string heightFile = GetChapterSavePath(chapterIndex);
        string foldsFile = GetFoldsSavePath(chapterIndex);

        if (File.Exists(heightFile))
        {
            Debug.Log($"[MapGen] Загрузка данных ландшафта главы {chapterIndex}");
            LoadMapFromFile(heightFile);

            if (File.Exists(foldsFile))
            {
                currentFolds = LoadFolds(foldsFile);
                Debug.Log($"[MapGen] Загружено {currentFolds.Count} складок.");
            }
            else
            {
                RegenerateFoldsRandomly();
            }

            if (MapDecorationManager.Instance != null)
            {
                MapDecorationManager.Instance.LoadAndSpawnDecorations(chapterIndex);
            }

            isHeightsLoaded = true;
        }
        else
        {
            Debug.LogWarning($"[MapGen] Файл высот не найден! Экстренная генерация...");
            float[,] heights = GenerateHeights();
            SaveMap(heights, GetChapterSavePath(chapterIndex));
            SaveFolds(currentFolds, GetFoldsSavePath(chapterIndex));

            if (MapDecorationManager.Instance != null)
            {
                MapDecorationManager.Instance.GenerateBasePaperTexture(chapterIndex, heights, currentFolds);
                MapDecorationManager.Instance.GenerateObjectDecorationsOnly(chapterIndex, themeId, currentFolds);
            }
            isHeightsLoaded = true;
        }
    }

    void RegenerateFoldsRandomly()
    {
        currentFolds.Clear();
        int foldCount = Random.Range(8, 16);
        float hugeLength = 2000f;

        for (int i = 0; i < foldCount; i++)
        {
            FoldLine fold = new FoldLine();
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomOffset = Random.Range(-mapSize * 0.7f, mapSize * 0.7f);

            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 centerPoint = new Vector2(mapSize / 2f, mapSize / 2f) + perp * randomOffset;

            fold.startX = centerPoint.x + dir.x * hugeLength;
            fold.startZ = centerPoint.y + dir.y * hugeLength;
            fold.endX = centerPoint.x - dir.x * hugeLength;
            fold.endZ = centerPoint.y - dir.y * hugeLength;

            fold.type = Random.value > 0.5f ? 1 : -1;
            fold.width = Random.Range(15f, 40f);
            fold.strength = Random.Range(0.05f, 0.10f);

            currentFolds.Add(fold);
        }
    }

    void SetupThemeParameters(string themeId)
    {
        MapTheme config = StoryContentLoader.GetThemeById(themeId);
        if (config != null)
        {
            noiseScale = config.noiseScale;
            heightMultiplier = Mathf.Clamp(config.heightMultiplier / mapHeight, 0.1f, 0.6f);
            octaves = config.octaves;
            persistence = config.persistence;
            smoothingPasses = config.smoothingPasses;
        }
    }

    float[,] GenerateHeights()
    {
        int resolution = terrainData.heightmapResolution;
        float[,] heights = new float[resolution, resolution];

        currentFolds.Clear();
        int foldCount = Random.Range(8, 16);
        float hugeLength = 2000f;

        for (int i = 0; i < foldCount; i++)
        {
            FoldLine fold = new FoldLine();
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomOffset = Random.Range(-mapSize * 0.7f, mapSize * 0.7f);

            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 centerPoint = new Vector2(mapSize / 2f, mapSize / 2f) + perp * randomOffset;

            fold.startX = centerPoint.x + dir.x * hugeLength;
            fold.startZ = centerPoint.y + dir.y * hugeLength;
            fold.endX = centerPoint.x - dir.x * hugeLength;
            fold.endZ = centerPoint.y - dir.y * hugeLength;

            fold.type = Random.value > 0.5f ? 1 : -1;
            fold.width = Random.Range(15f, 40f);
            fold.strength = Random.Range(0.05f, 0.10f);

            currentFolds.Add(fold);
        }

        float largeNoiseScale = 150f;
        float largeNoiseStrength = 0.02f;
        float nScale = 100f;
        float nStrength = 0.01f;

        for (int y = 0; y < resolution; y++)
        {
            float worldZ = (float)y / (resolution - 1) * mapSize;
            for (int x = 0; x < resolution; x++)
            {
                float worldX = (float)x / (resolution - 1) * mapSize;
                float heightValue = 0.5f;

                float lx = worldX / largeNoiseScale + offsetX;
                float lz = worldZ / largeNoiseScale + offsetY;
                float largeNoise = Mathf.PerlinNoise(lx, lz) * 2f - 1f;
                heightValue += largeNoise * largeNoiseStrength;

                foreach (var fold in currentFolds)
                {
                    float dist = GetDistanceToLineSegment(worldX, worldZ, fold.startX, fold.startZ, fold.endX, fold.endZ);
                    if (dist < fold.width)
                    {
                        float t = dist / fold.width;
                        float profile = (1f - t) * (1f - t);
                        heightValue += fold.type * fold.strength * profile;
                    }
                }

                float nx = worldX / nScale + offsetX;
                float nz = worldZ / nScale + offsetY;
                float paperNoise = Mathf.PerlinNoise(nx, nz) * 2f - 1f;
                heightValue += paperNoise * nStrength;

                heightValue = Mathf.Clamp(heightValue, 0.3f, 0.7f);
                heights[y, x] = heightValue;
            }
        }

        int passes = Mathf.Max(smoothingPasses, 2);
        heights = SmoothHeightmap(heights, resolution, passes);

        return heights;
    }

    string GetFoldsSavePath(int chapterIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}{foldsSaveSuffix}");
    }

    void SaveFolds(List<FoldLine> folds, string path)
    {
        FoldSaveData data = new FoldSaveData();
        data.folds = new List<FoldLineSave>(folds.Count);
        foreach (var f in folds)
        {
            data.folds.Add(new FoldLineSave
            {
                startX = f.startX,
                startZ = f.startZ,
                endX = f.endX,
                endZ = f.endZ,
                type = f.type,
                width = f.width,
                strength = f.strength
            });
        }
        string json = JsonUtility.ToJson(data, true);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
    }

    List<FoldLine> LoadFolds(string path)
    {
        if (!File.Exists(path)) return new List<FoldLine>();
        string json = File.ReadAllText(path);
        FoldSaveData data = JsonUtility.FromJson<FoldSaveData>(json);
        List<FoldLine> folds = new List<FoldLine>();
        if (data != null && data.folds != null)
        {
            foreach (var fSave in data.folds)
            {
                folds.Add(new FoldLine
                {
                    startX = fSave.startX,
                    startZ = fSave.startZ,
                    endX = fSave.endX,
                    endZ = fSave.endZ,
                    type = fSave.type,
                    width = fSave.width,
                    strength = fSave.strength
                });
            }
        }
        return folds;
    }

    public void DeleteAllChapterMaps()
    {
        for (int i = 0; i < 10; i++)
        {
            string hPath = GetChapterSavePath(i);
            string fPath = GetFoldsSavePath(i);
            string texPath = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{i}_final.png");
            string decorPath = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{i}_decor.json");

            if (File.Exists(hPath)) File.Delete(hPath);
            if (File.Exists(fPath)) File.Delete(fPath);
            if (File.Exists(texPath)) File.Delete(texPath);
            if (File.Exists(decorPath)) File.Delete(decorPath);
        }
        Debug.Log("[MapGen] Все файлы карт удалены.");
    }

    float GetDistanceToLineSegment(float px, float pz, float x1, float z1, float x2, float z2)
    {
        float A = px - x1; float B = pz - z1; float C = x2 - x1; float D = z2 - z1;
        float dot = A * C + B * D;
        float lenSq = C * C + D * D;
        float param = lenSq != 0f ? dot / lenSq : -1f;
        float xx, yy;
        if (param < 0f) { xx = x1; yy = z1; }
        else if (param > 1f) { xx = x2; yy = z2; }
        else { xx = x1 + param * C; yy = z1 + param * D; }
        float dx = px - xx; float dy = pz - yy;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    float[,] SmoothHeightmap(float[,] input, int res, int passes)
    {
        float[,] output = new float[res, res];
        for (int p = 0; p < passes; p++)
        {
            float[,] source = (p == 0) ? input : output;
            for (int y = 1; y < res - 1; y++)
            {
                for (int x = 1; x < res - 1; x++)
                {
                    float sum = source[y, x] * 2.0f;
                    sum += source[y - 1, x] * 1.5f + source[y + 1, x] * 1.5f;
                    sum += source[y, x - 1] * 1.5f + source[y, x + 1] * 1.5f;
                    sum += source[y - 1, x - 1] * 0.5f + source[y + 1, x - 1] * 0.5f;
                    sum += source[y - 1, x + 1] * 0.5f + source[y + 1, x + 1] * 0.5f;
                    output[y, x] = sum / 9.0f;
                }
            }
            for (int i = 0; i < res; i++)
            {
                output[0, i] = source[0, i]; output[res - 1, i] = source[res - 1, i];
                output[i, 0] = source[i, 0]; output[i, res - 1] = source[i, res - 1];
            }
        }
        return output;
    }

    string GetChapterSavePath(int chapterIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}.json");
    }

    void SaveMap(float[,] heights, string specificPath)
    {
        MapSaveData saveData = new MapSaveData();
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        saveData.width = width;
        saveData.height = height;
        saveData.heights = new float[width * height];
        for (int y = 0; y < width; y++)
            for (int x = 0; x < height; x++)
                saveData.heights[y * height + x] = heights[y, x];

        string json = JsonUtility.ToJson(saveData);
        Directory.CreateDirectory(Path.GetDirectoryName(specificPath));
        File.WriteAllText(specificPath, json);
    }

    public static float GetTerrainHeightAt(float worldX, float worldZ)
    {
        if (Instance == null || Instance.terrainData == null) return 0f;
        TerrainData data = Instance.terrainData;
        float normalizedX = Mathf.Clamp01(worldX / data.size.x);
        float normalizedZ = Mathf.Clamp01(worldZ / data.size.z);
        float heightValue = data.GetInterpolatedHeight(normalizedX, normalizedZ);
        return heightValue * data.size.y;
    }

    void LoadMapFromFile(string specificPath)
    {
        if (!File.Exists(specificPath)) return;
        string json = File.ReadAllText(specificPath);
        MapSaveData saveData = JsonUtility.FromJson<MapSaveData>(json);
        if (saveData == null || saveData.heights == null) return;

        int width = saveData.width;
        int height = saveData.height;
        float[,] heights = new float[width, height];
        for (int y = 0; y < width; y++)
            for (int x = 0; x < height; x++)
                heights[y, x] = saveData.heights[y * height + x];

        if (terrainData != null)
            terrainData.SetHeights(0, 0, heights);
    }
}

[System.Serializable]
public class ThemeConfigRoot { public List<MapTheme> themes; }

[System.Serializable]
public class MapSaveData
{
    public int width;
    public int height;
    public float[] heights;
}