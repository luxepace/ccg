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

    // Текущие складки для активной генерации
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

    public void GenerateAndSaveAllChapterMaps(List<ChapterConfig> chapters)
    {
        if (chapters == null || chapters.Count == 0) return;

        Debug.Log("[MapGen] Начало массовой генерации ландшафтов...");
        offsetX = Random.Range(0f, 10000f);
        offsetY = Random.Range(0f, 10000f);

        foreach (var chapter in chapters)
        {
            SetupThemeParameters(chapter.themeId);

            float[,] heights = GenerateHeights();

            string heightFile = GetChapterSavePath(chapter.chapterIndex);
            SaveMap(heights, heightFile);

            string foldsFile = GetFoldsSavePath(chapter.chapterIndex);
            SaveFolds(currentFolds, foldsFile);

            // === НОВОЕ: Генерация и сохранение декораций ===
            if (MapDecorationManager.Instance != null)
            {
                MapDecorationManager.Instance.GenerateAndSaveDecorations(
                    chapter.chapterIndex,
                    chapter.themeId,
                    currentFolds // Передаем текущие складки, чтобы избегать их
                );
            }
            // ==============================================

            Debug.Log($"[MapGen] Глава {chapter.chapterIndex}: сохранены высоты, складки и декорации.");
            Debug.Log($"[MapGen] Глава {chapter.chapterIndex}: сохранены высоты и складки.");
        }
        Debug.Log("[MapGen] Все ландшафты созданы.");
    }

    // В конец класса MapGenerator3D добавить:
    public List<FoldLine> GetCurrentFolds()
    {
        return currentFolds;
    }

    public void ApplyThemeAndLoadChapter(int chapterIndex, string themeId)
    {
        SetupThemeParameters(themeId);
        string heightFile = GetChapterSavePath(chapterIndex);
        string foldsFile = GetFoldsSavePath(chapterIndex);

        if (File.Exists(heightFile))
        {
            Debug.Log($"[MapGen] Загрузка ландшафта главы {chapterIndex}");

            LoadMapFromFile(heightFile);

            if (File.Exists(foldsFile))
            {
                currentFolds = LoadFolds(foldsFile);
                Debug.Log($"[MapGen] Загружено {currentFolds.Count} складок из файла.");
            }
            else
            {
                Debug.LogWarning($"[MapGen] Файл складок не найден: {foldsFile}. Генерируем новые.");
                RegenerateFoldsRandomly();
            }

            if (MapDecorationManager.Instance != null)
            {
                string decorFile = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}_decor.json");

                if (File.Exists(decorFile))
                {
                    // Менеджер сам загрузит и заспавнит
                    MapDecorationManager.Instance.LoadAndSpawnDecorations(chapterIndex);
                }
                else
                {
                    Debug.LogWarning($"[MapGen] Файл декораций не найден. Генерируем новые прямо сейчас.");
                    // Экстренная генерация
                    MapDecorationManager.Instance.GenerateAndSaveDecorations(chapterIndex, themeId, currentFolds);
                    // Теперь нужно загрузить то, что только что сгенерировалось (менеджер очистил список после сохранения)
                    // Поэтому лучше вызвать спавн вручную или изменить менеджер, чтобы он возвращал список.
                    // Простой хак: вызовем LoadAndSpawnDecorations снова, файл-то уже создан!
                    MapDecorationManager.Instance.LoadAndSpawnDecorations(chapterIndex);
                }
            }

            if (terrainData != null)
            {
                int res = terrainData.heightmapResolution;
                float[,] loadedHeights = terrainData.GetHeights(0, 0, res, res);
                ApplyPaperTextureToTerrain(loadedHeights, res);
                Debug.Log("[MapGen] Текстура сгенерирована по загруженным складкам.");
            }
        }
        else
        {
            Debug.LogWarning($"[MapGen] Файл высот не найден! Экстренная генерация...");
            float[,] heights = GenerateHeights();
            SaveMap(heights, heightFile);
            SaveFolds(currentFolds, foldsFile);
        }

        isHeightsLoaded = true;
        Debug.Log("[MapGen] Ландшафт загружен и готов.");
    }

    void RegenerateFoldsRandomly()
    {
        currentFolds.Clear();
        int foldCount = Random.Range(8, 16);
        float hugeLength = 2000f;

        for (int i = 0; i < foldCount; i++)
        {
            FoldLine fold = new FoldLine();

            // ИСПРАВЛЕНИЕ: Генерируем truly случайные линии
            // 1. Случайный угол
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

            // 2. Случайное смещение линии от центра (чтобы они не все проходили через центр!)
            // Мы берем случайную точку на перпендикуляре к направлению линии
            float randomOffset = Random.Range(-mapSize * 0.7f, mapSize * 0.7f);

            // Вектор направления линии
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            // Вектор перпендикуляра (нормаль)
            Vector2 perp = new Vector2(-dir.y, dir.x);

            // Центральная точка этой конкретной линии (смещена от центра карты)
            Vector2 centerPoint = new Vector2(mapSize / 2f, mapSize / 2f) + perp * randomOffset;

            // Начало и конец линии далеко за картой относительно её собственной центральной точки
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
        else
        {
            Debug.LogWarning($"[MapGen] Тема '{themeId}' не найдена. Используются значения по умолчанию.");
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

                // 1. Базовый шум
                float lx = worldX / largeNoiseScale + offsetX;
                float lz = worldZ / largeNoiseScale + offsetY;
                float largeNoise = Mathf.PerlinNoise(lx, lz) * 2f - 1f;
                heightValue += largeNoise * largeNoiseStrength;

                // 2. Прямые складки
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

                // 3. Мелкий шум
                float nx = worldX / nScale + offsetX;
                float nz = worldZ / nScale + offsetY;
                float paperNoise = Mathf.PerlinNoise(nx, nz) * 2f - 1f;
                heightValue += paperNoise * nStrength;

                // Ограничение диапазона (плоский рельеф: 0.3 - 0.7)
                heightValue = Mathf.Clamp(heightValue, 0.3f, 0.7f);

                heights[y, x] = heightValue;
            }
        }

        // Усиленное сглаживание
        int passes = Mathf.Max(smoothingPasses, 2);
        heights = SmoothHeightmap(heights, resolution, passes);

        ApplyPaperTextureToTerrain(heights, resolution);

        return heights;
    }

    void ApplyPaperTextureToTerrain(float[,] heightData, int resolution)
    {
        Texture2D paperTexture = GeneratePaperTexture(heightData, resolution);

        if (terrain != null)
        {
            Shader targetShader = Shader.Find("Universal Render Pipeline/Lit");
            if (targetShader == null) targetShader = Shader.Find("Standard");

            if (targetShader == null)
            {
                Debug.LogError("[MapGen] Шейдер не найден!");
                return;
            }

            if (terrain.materialTemplate == null || terrain.materialTemplate.shader != targetShader)
            {
                terrain.materialTemplate = new Material(targetShader);
            }

            Material mat = terrain.materialTemplate;
            mat.SetTexture("_BaseMap", paperTexture);
            mat.SetTexture("_MainTex", paperTexture);
            mat.SetFloat("_Glossiness", 0.0f);
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0.0f);
            mat.color = Color.white;

            Renderer rend = terrain.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = mat;
                rend.sharedMaterial = mat;
            }
        }
    }

    Texture2D GeneratePaperTexture(float[,] heightData, int resolution)
    {
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        texture.filterMode = FilterMode.Point; // Четкость
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[resolution * resolution];

        // Цвета: Темная бумага, Светлые линии
        Color basePaperLight = new Color(0.75f, 0.65f, 0.45f);
        Color basePaperDark = new Color(0.45f, 0.35f, 0.20f);
        Color foldShadowColor = new Color(0.35f, 0.28f, 0.18f); // Светлый оттенок для линий
        Color edgeBurnColor = new Color(0.25f, 0.20f, 0.12f);

        int index = 0;
        float centerX = resolution * 0.5f;
        float centerY = resolution * 0.5f;
        float maxDistSqr = centerX * centerX + centerY * centerY;

        var folds = currentFolds;
        int foldCount = folds.Count;

        float fScaleX = 0.15f;
        float fScaleY = 0.05f;
        float fOffX = offsetX * 2f;
        float fOffY = offsetY * 2f;

        for (int y = 0; y < resolution; y++)
        {
            float worldZ = (float)y / (resolution - 1) * mapSize;
            float dy = y - centerY;

            for (int x = 0; x < resolution; x++)
            {
                float worldX = (float)x / (resolution - 1) * mapSize;

                // 1. Базовый цвет от высоты
                float h = heightData[y, x];
                Color col = Color.Lerp(basePaperDark, basePaperLight, h * h * 0.9f + h * 0.1f);

                // 2. Линии складок (Прямые)
                if (foldCount > 0)
                {
                    float closestDist = 1000f;
                    float nearestWidth = 0f;

                    for (int i = 0; i < foldCount; i++)
                    {
                        var fold = folds[i];
                        float dist = GetDistanceToLineSegment(worldX, worldZ, fold.startX, fold.startZ, fold.endX, fold.endZ);

                        float drawWidth = fold.width * 0.25f; // Тонкие линии
                        if (dist < drawWidth && dist < closestDist)
                        {
                            closestDist = dist;
                            nearestWidth = drawWidth;
                        }
                    }

                    if (nearestWidth > 0f)
                    {
                        float t = closestDist / nearestWidth;
                        float intensity = 1f - t;

                        if (intensity > 0.01f)
                        {
                            float shadow = Mathf.Pow(intensity, 10.0f); // Резкий переход
                            float mix = shadow * 0.4f; // Светлое смешивание

                            if (mix > 0.001f)
                            {
                                col.r = Mathf.Lerp(col.r, foldShadowColor.r, mix);
                                col.g = Mathf.Lerp(col.g, foldShadowColor.g, mix);
                                col.b = Mathf.Lerp(col.b, foldShadowColor.b, mix);
                            }
                        }
                    }
                }

                // 3. Волокна бумаги
                float fiber = Mathf.PerlinNoise(x * fScaleX + fOffX, y * fScaleY + fOffY);
                if (fiber > 0.55f)
                {
                    float fVal = (fiber - 0.55f) * 0.06f;
                    col.r += fVal; col.g += fVal * 0.9f; col.b += fVal * 0.8f;
                }
                else if (fiber < 0.45f)
                {
                    float fVal = (0.45f - fiber) * 0.06f;
                    col.r -= fVal; col.g -= fVal * 0.9f; col.b -= fVal * 0.8f;
                }

                // 4. Виньетка
                float dx = x - centerX;
                float distSqr = dx * dx + dy * dy;
                if (distSqr > 0.1f)
                {
                    float vignette = Mathf.Sqrt(distSqr / maxDistSqr);
                    if (vignette < 1f)
                    {
                        float vFactor = Mathf.Pow(vignette, 5.0f);
                        if (vFactor > 0.001f)
                        {
                            float vMix = vFactor * 0.3f;
                            col.r = Mathf.Lerp(col.r, edgeBurnColor.r, vMix);
                            col.g = Mathf.Lerp(col.g, edgeBurnColor.g, vMix);
                            col.b = Mathf.Lerp(col.b, edgeBurnColor.b, vMix);
                        }
                    }
                }

                // Clamp
                if (col.r > 1f) col.r = 1f; if (col.r < 0f) col.r = 0f;
                if (col.g > 1f) col.g = 1f; if (col.g < 0f) col.g = 0f;
                if (col.b > 1f) col.b = 1f; if (col.b < 0f) col.b = 0f;

                pixels[index++] = col;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    // === МЕТОДЫ СОХРАНЕНИЯ/ЗАГРУЗКИ (Для прямых линий) ===

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

            if (File.Exists(hPath)) File.Delete(hPath);
            if (File.Exists(fPath)) File.Delete(fPath);
        }
        Debug.Log("[MapGen] Все файлы карт и складок удалены.");
    }

    
    float GetDistanceToLineSegment(float px, float pz, float x1, float z1, float x2, float z2)
    {
        float A = px - x1;
        float B = pz - z1;
        float C = x2 - x1;
        float D = z2 - z1;
        float dot = A * C + B * D;
        float lenSq = C * C + D * D;
        float param = -1f;
        if (lenSq != 0f) param = dot / lenSq;
        float xx, yy;
        if (param < 0f) { xx = x1; yy = z1; }
        else if (param > 1f) { xx = x2; yy = z2; }
        else { xx = x1 + param * C; yy = z1 + param * D; }
        float dx = px - xx;
        float dy = pz - yy;
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
                    // Мягкое ядро сглаживания
                    float sum = source[y, x] * 2.0f;
                    sum += source[y - 1, x] * 1.5f;
                    sum += source[y + 1, x] * 1.5f;
                    sum += source[y, x - 1] * 1.5f;
                    sum += source[y, x + 1] * 1.5f;
                    sum += source[y - 1, x - 1] * 0.5f;
                    sum += source[y + 1, x - 1] * 0.5f;
                    sum += source[y - 1, x + 1] * 0.5f;
                    sum += source[y + 1, x + 1] * 0.5f;

                    output[y, x] = sum / 9.0f;
                }
            }

            for (int i = 0; i < res; i++)
            {
                output[0, i] = source[0, i];
                output[res - 1, i] = source[res - 1, i];
                output[i, 0] = source[i, 0];
                output[i, res - 1] = source[i, res - 1];
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

    public void GenerateNewMap()
    {
        float[,] heights = GenerateHeights();
        terrainData.SetHeights(0, 0, heights);
        isHeightsLoaded = true;
    }

    public void DeleteSave()
    {
        string oldPath = Path.Combine(Application.persistentDataPath, "mapData.json");
        if (File.Exists(oldPath)) File.Delete(oldPath);
    }
}

[System.Serializable]
public class ThemeConfigRoot { public List<MapThemeConfig> themes; }
[System.Serializable]
public class MapThemeConfig
{
    public string themeId;
    public string themeName;
    public string terrainTexture;
    public string skyboxMaterial;
    public float r, g, b, a;
    public string[] decorationPrefabs;
    public float noiseScale = 50f;
    public float heightMultiplier = 25f;
    public int octaves = 4;
    public float persistence = 0.5f;
    public float peakThreshold = 0.5f;
    public float peakSharpness = 2.5f;
    public int smoothingPasses = 1;
}
[System.Serializable]
public class MapSaveData
{
    public int width;
    public int height;
    public float[] heights;
}