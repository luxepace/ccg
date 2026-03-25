using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class MapDecorationManager : MonoBehaviour
{
    public static MapDecorationManager Instance;

    [Header("Настройки")]
    public float generationStep = 4f;
    public string currentTheme = "";

    [Header("Настройки путей")]
    public float pathWidth = 2.0f;
    public Color pathColor = new Color(0.35f, 0.22f, 0.10f);
    [Range(1, 10)]
    public int pathDetailPoints = 4;

    [Header("Зоны отчуждения (Avoidance)")]
    public float nodeAvoidRadius = 8f;
    public float pathAvoidMargin = 15f;
    public float waterNodeAvoidRadius = 30f; // === НОВОЕ: Увеличенный радиус для воды ===

    [Header("Настройки мостов")]
    public Color bridgeColor = new Color(0.55f, 0.55f, 0.55f, 1.0f);
    public float bridgeWidthMargin = 0.7f; // === БЫЛО 0.5f, СТАЛО 1.2f ===

    private string baseSaveFileName = "map_chapter_";
    private string decorSaveSuffix = "_decor.json";
    private string finalTextureSuffix = "_final.png";

    private List<MapDecoration> currentDecorations = new List<MapDecoration>();
    private GameObject decorContainer;
    private Terrain terrainRef;

    // Храним данные о сгенерированной воде и горах для математической проверки коллизий
    private List<PaintedWaterObject> generatedWaterObjects = new List<PaintedWaterObject>();

    private void Awake()
    {
        terrainRef = FindObjectOfType<Terrain>();
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void GenerateBasePaperTexture(int chapterIndex, float[,] heights, List<FoldLine> folds, string theme = "")
    {
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null || terrain.terrainData == null) return;

        currentTheme = theme;
        Debug.Log($"[DecorMan] Генерация базовой текстуры для главы {chapterIndex}, тема: {theme}");

        int resolution = terrain.terrainData.heightmapResolution;
        float mapSize = terrain.terrainData.size.x;

        Texture2D texture = GeneratePaperTextureWithFolds(heights, resolution, folds, mapSize, theme);

        SaveFinalTexture(texture, chapterIndex);
        ApplyTextureToTerrain(terrain, texture);

        Debug.Log($"[DecorMan] Базовая текстура главы {chapterIndex} сохранена.");
    }

    public void GenerateObjectDecorationsOnly(int chapterIndex, string themeId, List<FoldLine> folds, List<PathData> paths, List<StoryNode> nodes)
    {
        currentDecorations.Clear();
        if (StoryContentLoader.AllDecorations == null)
        {
            Debug.LogError("[DecorMan] Список декораций пуст!");
            return;
        }

        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null) return;

        float mapSize = terrain.terrainData.size.x;
        float mapBorderMargin = 10.0f;
        float minValidX = mapBorderMargin;
        float maxValidX = mapSize - mapBorderMargin;
        float minValidZ = mapBorderMargin;
        float maxValidZ = mapSize - mapBorderMargin;

        int totalSpawned = 0;

        Debug.Log($"[DecorMan] Генерация для темы: '{themeId}'. Безопасная зона: от {mapBorderMargin} до {maxValidX}.");

        foreach (var cfg in StoryContentLoader.AllDecorations)
        {
            if (cfg.isPainted) continue;

            bool themeAllowed = false;
            if (cfg.allowedThemes == null || cfg.allowedThemes.Count == 0)
            {
                themeAllowed = true;
            }
            else
            {
                foreach (string t in cfg.allowedThemes)
                {
                    if (!string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(themeId) && t.Trim().ToLower() == themeId.Trim().ToLower())
                    {
                        themeAllowed = true;
                        break;
                    }
                }
            }

            if (!themeAllowed) continue;

            int spawnedThisType = 0;

            if (cfg.isClustered)
            {
                float minClusterDistance = cfg.clusterRadius * 2.5f;
                List<Vector2> clusterCenters = new List<Vector2>();

                for (int c = 0; c < cfg.clusterCount; c++)
                {
                    int attempts = 0;
                    int maxAttempts = 100;
                    Vector2 finalCenter = Vector2.zero;
                    bool centerFound = false;

                    while (attempts < maxAttempts)
                    {
                        attempts++;
                        Vector2 proposedCenter = new Vector2(
                            Random.Range(minValidX, maxValidX),
                            Random.Range(minValidZ, maxValidZ)
                        );

                        bool tooCloseToOtherCluster = false;
                        foreach (Vector2 existingCenter in clusterCenters)
                        {
                            if (Vector2.Distance(proposedCenter, existingCenter) < minClusterDistance)
                            {
                                tooCloseToOtherCluster = true;
                                break;
                            }
                        }

                        if (!tooCloseToOtherCluster)
                        {
                            if (IsPositionBlocked(proposedCenter, nodes, paths, cfg.avoidPaths))
                            {
                                tooCloseToOtherCluster = true;
                            }
                        }

                        if (!tooCloseToOtherCluster)
                        {
                            finalCenter = proposedCenter;
                            centerFound = true;
                            break;
                        }
                    }

                    if (!centerFound) continue;
                    clusterCenters.Add(finalCenter);

                    int spawnedInThisCluster = 0;
                    int treeAttempts = 0;
                    int maxTreeAttempts = cfg.objectsPerCluster * 20;
                    List<Vector3> treePositionsInCluster = new List<Vector3>();
                    float avgScale = (cfg.minScale + cfg.maxScale) * 0.5f;
                    float minTreeDistance = avgScale * 1.5f;

                    while (spawnedInThisCluster < cfg.objectsPerCluster && treeAttempts < maxTreeAttempts)
                    {
                        treeAttempts++;
                        float angle = Random.Range(0f, Mathf.PI * 2);
                        float dist = Random.Range(0f, cfg.clusterRadius);
                        float x = finalCenter.x + Mathf.Cos(angle) * dist;
                        float z = finalCenter.y + Mathf.Sin(angle) * dist;

                        if (x < minValidX || x >= maxValidX || z < minValidZ || z >= maxValidZ)
                        {
                            continue;
                        }

                        Vector2 pos2D = new Vector2(x, z);
                        Vector3 pos3D = new Vector3(x, 0, z);

                        bool localTooClose = false;
                        foreach (Vector3 existingPos in treePositionsInCluster)
                        {
                            if (Vector3.Distance(pos3D, existingPos) < minTreeDistance)
                            {
                                localTooClose = true;
                                break;
                            }
                        }
                        if (localTooClose) continue;

                        if (IsPositionBlocked(pos2D, nodes, paths, cfg.avoidPaths))
                        {
                            continue;
                        }

                        float h = MapGenerator3D.GetTerrainHeightAt(x, z);

                        MapDecoration d = new MapDecoration();
                        d.prefabId = cfg.prefabId;
                        d.position = new Vector3(x, h, z);
                        d.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        d.scale = Vector3.one * Random.Range(cfg.minScale, cfg.maxScale);

                        currentDecorations.Add(d);
                        treePositionsInCluster.Add(pos3D);
                        spawnedInThisCluster++;
                    }
                }
            }
            else
            {
                for (float x = minValidX; x < maxValidX; x += generationStep)
                {
                    for (float z = minValidZ; z < maxValidZ; z += generationStep)
                    {
                        if (Random.value > cfg.density * generationStep * generationStep) continue;

                        float jitterX = Random.Range(-generationStep * 0.4f, generationStep * 0.4f);
                        float jitterZ = Random.Range(-generationStep * 0.4f, generationStep * 0.4f);

                        float finalX = x + jitterX;
                        float finalZ = z + jitterZ;

                        if (finalX < minValidX || finalX >= maxValidX || finalZ < minValidZ || finalZ >= maxValidZ)
                        {
                            continue;
                        }

                        Vector2 pos2D = new Vector2(finalX, finalZ);

                        if (IsPositionBlocked(pos2D, nodes, paths, cfg.avoidPaths))
                        {
                            continue;
                        }

                        float h = MapGenerator3D.GetTerrainHeightAt(finalX, finalZ);

                        MapDecoration d = new MapDecoration();
                        d.prefabId = cfg.prefabId;
                        d.position = new Vector3(finalX, h, finalZ);
                        d.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        d.scale = Vector3.one * Random.Range(cfg.minScale, cfg.maxScale);

                        currentDecorations.Add(d);
                        spawnedThisType++;
                    }
                }
            }

            Debug.Log($"[DecorMan] Тип '{cfg.prefabId}': Создано={spawnedThisType}");
            totalSpawned += spawnedThisType;
        }

        Debug.Log($"[DecorMan] Всего сгенерировано объектов: {totalSpawned}. Сохранение...");

        // Сначала сохраняем координаты объектов
        SaveDecorations(currentDecorations, GetDecorSavePath(chapterIndex));

        // === НОВОЕ: Рисуем траву на текстуре, используя только что сохраненный файл ===
        DrawGrassUnderSavedDecorations(chapterIndex, themeId);

        currentDecorations.Clear();
    }

    private bool IsPositionBlocked(Vector2 pos, List<StoryNode> nodes, List<PathData> paths, bool checkPaths)
    {
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                Vector2 nodePos2D = new Vector2(node.position.x, node.position.z);
                if (Vector2.Distance(pos, nodePos2D) < nodeAvoidRadius) return true;
            }
        }

        if (checkPaths && paths != null)
        {
            float requiredClearance = (this.pathWidth * 0.5f) + this.pathAvoidMargin;
            foreach (var path in paths)
            {
                float dist = GetDistanceToLineSegment(pos.x, pos.y, path.start.x, path.start.y, path.end.x, path.end.y);
                if (dist < requiredClearance) return true;
            }
        }

        if (IsPositionOnWaterOrMountain(pos)) return true;

        return false;
    }

    private bool IsPositionOnWaterOrMountain(Vector2 pos)
    {
        foreach (var waterObj in generatedWaterObjects)
        {
            if (waterObj.shape == "circle" || waterObj.shape == "blob" || waterObj.shape == "mountain")
            {
                float safetyBuffer = waterNodeAvoidRadius * 0.5f; // === УВЕЛИЧЕНО ===
                float effectiveRadius = waterObj.radius + safetyBuffer;

                if (IsPointInsideBlob(pos, waterObj.center, effectiveRadius, waterObj.noiseOffsetX, waterObj.noiseOffsetY))
                {
                    return true;
                }
            }
            else if (waterObj.shape == "line")
            {
                float safetyBuffer = waterNodeAvoidRadius * 0.3f; // === УВЕЛИЧЕНО ===
                float dist = GetDistanceToLineSegment(pos.x, pos.y, waterObj.center.x, waterObj.center.y, waterObj.endPoint.x, waterObj.endPoint.y);
                if (dist < (waterObj.width * 0.5f) + safetyBuffer) return true;
            }
        }
        return false;
    }

    private bool IsPointInsideBlob(Vector2 point, Vector2 center, float baseRadius, float noiseOffsetX, float noiseOffsetY)
    {
        float dx = point.x - center.x;
        float dy = point.y - center.y;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);

        if (dist > baseRadius * 2.0f) return false;

        float angle = Mathf.Atan2(dy, dx);

        float noiseFrequencyMain = 0.10f;
        float noiseFrequencyDetail = 0.25f;
        float noiseAmplitudeMain = 0.7f;
        float noiseAmplitudeDetail = 0.15f;
        float asymmetryOffset = 200f;

        float noiseVal1 = Mathf.PerlinNoise(
            (Mathf.Cos(angle) * 2.0f + noiseOffsetX) * noiseFrequencyMain,
            (Mathf.Sin(angle) * 2.0f + noiseOffsetY) * noiseFrequencyMain
        );

        float noiseVal2 = Mathf.PerlinNoise(
            (Mathf.Cos(angle * 3.0f) + noiseOffsetX + asymmetryOffset) * noiseFrequencyDetail,
            (Mathf.Sin(angle * 3.0f) + noiseOffsetY + asymmetryOffset) * noiseFrequencyDetail
        );

        float radiusLayer1 = (1.0f - noiseAmplitudeMain) + noiseVal1 * (noiseAmplitudeMain * 2.0f);
        float radiusLayer2 = 1.0f + (noiseVal2 - 0.5f) * (noiseAmplitudeDetail * 4.0f);

        float finalRadiusMultiplier = radiusLayer1 * radiusLayer2;
        float currentFinalDist = baseRadius * finalRadiusMultiplier;

        return dist <= currentFinalDist;
    }

    public void GenerateAndDrawPaintedDecorations(int chapterIndex, string themeId, List<StoryNode> nodes, List<PathData> paths = null)
    {
        generatedWaterObjects.Clear();

        string path = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}{finalTextureSuffix}");
        if (!File.Exists(path))
        {
            Debug.LogError($"[DecorMan] Базовая текстура не найдена для главы {chapterIndex}!");
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        if (!tex.LoadImage(bytes)) return;

        int resolution = tex.width;
        Color[] pixels = tex.GetPixels();
        float mapSize = MapGenerator3D.Instance.mapSize;

        Debug.Log($"[DecorMan] Рисование декораций для темы: {themeId}");

        List<Vector2> spawnedWaterCenters = new List<Vector2>();
        List<float> spawnedWaterRadii = new List<float>();

        // === ШАГ 1: СНАЧАЛА РИСУЕМ ФОН ТЕМЫ ===
        foreach (var cfg in StoryContentLoader.AllDecorations)
        {
            if (!cfg.isPainted) continue;
            if (cfg.shape != "theme_background") continue;

            bool themeAllowed = (cfg.allowedThemes == null || cfg.allowedThemes.Count == 0) ||
                                cfg.allowedThemes.Any(t => !string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(themeId) && t.Trim().ToLower() == themeId.Trim().ToLower());

            if (!themeAllowed) continue;

            Debug.Log($"[DecorMan] Рисуем фон темы: {cfg.prefabId}");
            DrawMountainThemeBackground(pixels, resolution, mapSize, cfg.paintColor, cfg.secondaryColor);
        }

        // === ШАГ 2: РИСУЕМ ТРАВУ (ПЕРЕД ВОДОЙ!) ===
        foreach (var cfg in StoryContentLoader.AllDecorations)
        {
            if (!cfg.isPainted) continue;
            if (cfg.shape != "grass") continue;

            bool themeAllowed = (cfg.allowedThemes == null || cfg.allowedThemes.Count == 0) ||
                                cfg.allowedThemes.Any(t => !string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(themeId) && t.Trim().ToLower() == themeId.Trim().ToLower());

            if (!themeAllowed) continue;

            int baseCount = cfg.count;
            int actualCount = Random.Range(Mathf.Max(1, baseCount - 1), baseCount + 2);

            Debug.Log($"[DecorMan] Рисуем траву: {actualCount} пятен (ПЕРЕД водой)");

            for (int i = 0; i < actualCount; i++)
            {
                Vector2 finalPos = new Vector2(Random.Range(0f, mapSize), Random.Range(0f, mapSize));
                float finalSize = Random.Range(cfg.minSize, cfg.maxSize) * 0.5f;

                if (IsPositionBlockedByNodes(finalPos, nodes)) continue;

                float noiseOffX = Random.Range(0f, 1000f);
                float noiseOffY = Random.Range(0f, 1000f);

                generatedWaterObjects.Add(new PaintedWaterObject
                {
                    shape = "grass",
                    center = finalPos,
                    radius = finalSize,
                    color = cfg.paintColor,
                    noiseOffsetX = noiseOffX,
                    noiseOffsetY = noiseOffY
                });

                DrawGrassPatch(pixels, resolution, mapSize, finalPos, finalSize, cfg.paintColor, noiseOffX, noiseOffY);
            }
        }

        // === ШАГ 3: ТЕПЕРЬ РИСУЕМ ВОДУ, РЕКИ И ГОРЫ (ПОВЕРХ ТРАВЫ) ===
        foreach (var cfg in StoryContentLoader.AllDecorations)
        {
            if (!cfg.isPainted) continue;
            if (cfg.shape == "theme_background" || cfg.shape == "grass") continue;

            bool themeAllowed = (cfg.allowedThemes == null || cfg.allowedThemes.Count == 0) ||
                                cfg.allowedThemes.Any(t => !string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(themeId) && t.Trim().ToLower() == themeId.Trim().ToLower());

            if (!themeAllowed) continue;
                   

            int baseCount = cfg.count;
            int minCount = Mathf.Max(1, baseCount - 1);
            int maxCount = baseCount + 1;
            int actualCount = Random.Range(minCount, maxCount + 1);

            Debug.Log($"[DecorMan] Тип '{cfg.prefabId}': Планируется {actualCount} шт.");

            float minSize = cfg.minSize;
            float maxSize = cfg.maxSize;
            Color col = cfg.paintColor;
            string shape = cfg.shape;

            float minGap = 5f;

            for (int i = 0; i < actualCount; i++)
            {
                int attempts = 0;
                int maxAttempts = 50;
                bool positionFound = false;
                Vector2 finalPos = Vector2.zero;
                float finalSize = 0f;
                List<Vector2> riverPathCache = null;

                while (attempts < maxAttempts)
                {
                    attempts++;
                    Vector2 proposedPos = new Vector2(Random.Range(0f, mapSize), Random.Range(0f, mapSize));
                    float proposedRadius = 0f;

                    if (shape == "circle" || shape == "blob")
                    {
                        proposedRadius = Random.Range(minSize, maxSize) * 0.5f;
                    }
                    else if (shape == "line")
                    {
                        proposedRadius = Random.Range(minSize, maxSize) * 0.5f;
                    }
                    else if (shape == "mountain")
                    {
                        proposedRadius = Random.Range(minSize, maxSize) * 0.5f;
                    }

                    if (cfg.avoidNodes && IsPositionBlockedByNodes(proposedPos, nodes, waterNodeAvoidRadius))
                    {
                        continue;
                    }

                    // === НОВОЕ: Дополнительная проверка для рек ===
                    if (shape == "line" && nodes != null)
                    {
                        // Проверяем весь путь реки на пересечение с нодами
                        List<Vector2> testPath = GenerateRiverPath(proposedPos, proposedRadius, mapSize, nodes);
                        if (testPath == null)
                        {
                            continue; // Река пересекает ноды - пропускаем
                        }
                    }

                    // === НОВОЕ: ГОРЫ ИЗБЕГАЮТ ПУТЕЙ ===
                    if (shape == "mountain" && paths != null && paths.Count > 0)
                    {
                        float mountainPathAvoidMargin = 20f; // Ещё больше отступ для гор
                        float requiredClearance = (this.pathWidth * 0.5f) + mountainPathAvoidMargin;

                        bool tooCloseToPath = false;
                        foreach (var texPath in paths)
                        {
                            float dist = GetDistanceToLineSegment(proposedPos.x, proposedPos.y,
                                                                   texPath.start.x, texPath.start.y,
                                                                   texPath.end.x, texPath.end.y);
                            if (dist < requiredClearance)
                            {
                                tooCloseToPath = true;
                                break;
                            }
                        }

                        if (tooCloseToPath) continue;
                    }

                    bool overlaps = false;
                    float requiredDistance = proposedRadius + minGap;

                    for (int k = 0; k < spawnedWaterCenters.Count; k++)
                    {
                        float dist = Vector2.Distance(proposedPos, spawnedWaterCenters[k]);
                        float otherRadius = spawnedWaterRadii[k];

                        if (dist < (requiredDistance + otherRadius))
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        finalPos = proposedPos;
                        finalSize = proposedRadius;

                        if (shape == "line")
                        {
                            List<Vector2> tempPath = GenerateRiverPath(finalPos, finalSize, mapSize, nodes);
                            if (tempPath != null && tempPath.Count > 1)
                            {
                                riverPathCache = tempPath;
                                positionFound = true;
                                break;
                            }
                        }
                        else
                        {
                            positionFound = true;
                            break;
                        }
                    }
                }

                if (!positionFound)
                {
                    Debug.LogWarning($"[DecorMan] Не удалось найти место для '{cfg.prefabId}' #{i + 1}. Пропущено.");
                    continue;
                }

                if (shape == "circle" || shape == "blob")
                {
                    float noiseOffX = Random.Range(0f, 1000f);
                    float noiseOffY = Random.Range(0f, 1000f);

                    generatedWaterObjects.Add(new PaintedWaterObject
                    {
                        shape = "blob",
                        center = finalPos,
                        radius = finalSize,
                        color = col,
                        noiseOffsetX = noiseOffX,
                        noiseOffsetY = noiseOffY
                    });

                    spawnedWaterCenters.Add(finalPos);
                    spawnedWaterRadii.Add(finalSize);
                    DrawOrganicBlob(pixels, resolution, mapSize, finalPos, finalSize, col, noiseOffX, noiseOffY);
                }
                else if (shape == "line")
                {
                    List<Vector2> riverPath = riverPathCache;
                    float riverWidth = finalSize * 2f;

                    float riverNoiseOffX = Random.Range(0f, 1000f);
                    float riverNoiseOffY = Random.Range(0f, 1000f);

                    float stepSize = riverWidth * 0.6f;

                    for (int j = 0; j < riverPath.Count - 1; j++)
                    {
                        Vector2 p1 = riverPath[j];
                        Vector2 p2 = riverPath[j + 1];
                        float dist = Vector2.Distance(p1, p2);

                        if (dist > stepSize)
                        {
                            int subSteps = Mathf.CeilToInt(dist / stepSize);
                            for (int s = 0; s <= subSteps; s++)
                            {
                                float t = (float)s / subSteps;
                                Vector2 interpPos = Vector2.Lerp(p1, p2, t);
                                DrawOrganicRiverSegment(pixels, resolution, mapSize, interpPos, riverWidth * 0.65f, col, riverNoiseOffX, riverNoiseOffY, j * 0.1f);
                            }
                        }
                        else
                        {
                            Vector2 midPos = (p1 + p2) * 0.5f;
                            DrawOrganicRiverSegment(pixels, resolution, mapSize, midPos, riverWidth * 0.65f, col, riverNoiseOffX, riverNoiseOffY, j * 0.1f);
                        }
                    }

                    for (int j = 0; j < riverPath.Count - 1; j += 5)
                    {
                        generatedWaterObjects.Add(new PaintedWaterObject
                        {
                            shape = "line",
                            center = riverPath[j],
                            endPoint = riverPath[Mathf.Min(j + 5, riverPath.Count - 1)],
                            width = riverWidth,
                            color = col
                        });
                    }

                    spawnedWaterCenters.Add(riverPath[0]);
                    spawnedWaterRadii.Add(finalSize);
                }
                else if (shape == "mountain")
                {
                    Color rockBase = cfg.secondaryColor;
                    if (rockBase == default(Color)) rockBase = new Color(0.5f, 0.5f, 0.5f);

                    float noiseOffX = Random.Range(0f, 1000f);
                    float noiseOffY = Random.Range(0f, 1000f);

                    generatedWaterObjects.Add(new PaintedWaterObject
                    {
                        shape = "blob",
                        center = finalPos,
                        radius = finalSize,
                        color = col,
                        noiseOffsetX = noiseOffX,
                        noiseOffsetY = noiseOffY
                    });

                    spawnedWaterCenters.Add(finalPos);
                    spawnedWaterRadii.Add(finalSize);
                    DrawMountainPeakWithRidges(pixels, resolution, mapSize, finalPos, finalSize, col, rockBase, paths ?? new List<PathData>());
                }
            }
        }
        

        tex.SetPixels(pixels);
        tex.Apply();
        SaveFinalTexture(tex, chapterIndex);

        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain != null) ApplyTextureToTerrain(terrain, tex);

        Debug.Log("[DecorMan] Рисованные декорации нанесены.");
    }
  

    // === ОТРИСОВКА ФОНА ГОРНОЙ ТЕМЫ (ТЕМНЕЕ) ===
    void DrawMountainThemeBackground(Color[] pixels, int resolution, float mapSize, Color peakColor, Color baseColor)
    {
        float pixelsPerUnit = resolution / mapSize;
        float noiseScale = 0.02f;
        float noiseOffsetX = Random.Range(0f, 1000f);
        float noiseOffsetY = Random.Range(0f, 1000f);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float worldX = x / pixelsPerUnit;
                float worldY = y / pixelsPerUnit;

                float noiseVal = Mathf.PerlinNoise((worldX * noiseScale) + noiseOffsetX, (worldY * noiseScale) + noiseOffsetY);

                // Градиент от тёмно-серого к светло-серому для имитации гор
                float mountainIntensity = noiseVal;
                Color mountainColor = Color.Lerp(baseColor, peakColor, Mathf.Pow(mountainIntensity, 0.7f));

                int idx = y * resolution + x;

                // Смешиваем с текущим пикселем (базовая текстура бумаги)
                pixels[idx] = Color.Lerp(pixels[idx], mountainColor, 0.6f);
            }
        }
    }

    // === ОТРИСОВКА РЕКИ (ИСПРАВЛЕНО - СВЕТЛЕЕ) ===
    void DrawOrganicRiverSegment(Color[] pixels, int resolution, float mapSize, Vector2 centerWorld, float radius, Color col, float globalNoiseX, float globalNoiseY, float localOffset)
    {
        float pixelsPerUnit = resolution / mapSize;
        int cx = Mathf.FloorToInt(centerWorld.x * pixelsPerUnit);
        int cy = Mathf.FloorToInt(centerWorld.y * pixelsPerUnit);

        int maxR = Mathf.CeilToInt(radius * 2.2f * pixelsPerUnit);

        int minX = Mathf.Max(0, cx - maxR);
        int maxX = Mathf.Min(resolution - 1, cx + maxR);
        int minY = Mathf.Max(0, cy - maxR);
        int maxY = Mathf.Min(resolution - 1, cy + maxR);

        float noiseFrequencyMain = 0.10f;
        float noiseFrequencyDetail = 0.25f;
        float noiseAmplitudeMain = 0.7f;
        float noiseAmplitudeDetail = 0.15f;
        float asymmetryOffset = 200f;

        float seedX = globalNoiseX + localOffset * 100f;
        float seedY = globalNoiseY + localOffset * 50f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = x / pixelsPerUnit;
                float worldY = y / pixelsPerUnit;

                float dx = worldX - centerWorld.x;
                float dy = worldY - centerWorld.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > radius * 2.0f) continue;

                float angle = Mathf.Atan2(dy, dx);

                float noiseVal1 = Mathf.PerlinNoise(
                    (Mathf.Cos(angle) * 2.0f + seedX) * noiseFrequencyMain,
                    (Mathf.Sin(angle) * 2.0f + seedY) * noiseFrequencyMain
                );

                float noiseVal2 = Mathf.PerlinNoise(
                    (Mathf.Cos(angle * 3.0f) + seedX + asymmetryOffset) * noiseFrequencyDetail,
                    (Mathf.Sin(angle * 3.0f) + seedY + asymmetryOffset) * noiseFrequencyDetail
                );

                float radiusLayer1 = (1.0f - noiseAmplitudeMain) + noiseVal1 * (noiseAmplitudeMain * 2.0f);
                float radiusLayer2 = 1.0f + (noiseVal2 - 0.5f) * (noiseAmplitudeDetail * 4.0f);

                float finalRadiusMultiplier = radiusLayer1 * radiusLayer2;
                float currentFinalDist = radius * finalRadiusMultiplier;

                if (dist <= currentFinalDist)
                {
                    int idx = y * resolution + x;
                    float edgeDist = currentFinalDist - dist;
                    float alpha = 1f;

                    if (edgeDist < 3.5f)
                    {
                        alpha = Mathf.Clamp01(edgeDist / 3.5f);
                    }

                    if (alpha > 0.01f)
                    {
                        // ИСПРАВЛЕНО: используем более сильное смешивание, чтобы вода была ярче
                        pixels[idx] = Color.Lerp(pixels[idx], col, alpha * 0.9f);
                    }
                }
            }
        }
    }

    private List<Vector2> GenerateRiverPath(Vector2 seedPos, float halfWidth, float mapSize, List<StoryNode> nodes)
    {
        bool isHorizontal = Random.value > 0.5f;
        float margin = mapSize * 0.5f;
        Vector2 riverStart, riverEnd;

        if (isHorizontal)
        {
            riverStart = new Vector2(-margin, seedPos.y);
            riverEnd = new Vector2(mapSize + margin, seedPos.y + Random.Range(-mapSize * 0.2f, mapSize * 0.2f));
        }
        else
        {
            riverStart = new Vector2(seedPos.x, -margin);
            riverEnd = new Vector2(seedPos.x + Random.Range(-mapSize * 0.2f, mapSize * 0.2f), mapSize + margin);
        }

        float windingStrength = halfWidth * 2.5f * 2.0f;
        float noiseScale = 0.015f;
        float seedOffset = Random.Range(0f, 1000f);

        List<Vector2> riverPath = new List<Vector2>();
        float totalDist = Vector2.Distance(riverStart, riverEnd);
        int steps = Mathf.CeilToInt(totalDist * 1.5f);
        Vector2 direction = (riverEnd - riverStart).normalized;

        for (int j = 0; j <= steps; j++)
        {
            float t = (float)j / steps;
            Vector2 basePoint = Vector2.Lerp(riverStart, riverEnd, t);

            float noiseVal = Mathf.PerlinNoise(t * totalDist * noiseScale + seedOffset, 0f) * 2f - 1f;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 windingPoint = basePoint + perpendicular * noiseVal * windingStrength;

            PaintedWaterObject hitMountain = null;
            foreach (var mtn in generatedWaterObjects)
            {
                if (mtn.shape == "blob" || mtn.shape == "mountain")
                {
                    if (IsPointInsideBlob(windingPoint, mtn.center, mtn.radius, mtn.noiseOffsetX, mtn.noiseOffsetY))
                    {
                        hitMountain = mtn;
                        break;
                    }
                }
            }

            if (hitMountain != null)
            {
                Vector2 toCenter = windingPoint - hitMountain.center;
                if (toCenter.magnitude < 1f)
                {
                    windingPoint += perpendicular * (halfWidth * 2.0f) * Mathf.Sign(noiseVal);
                }
                else
                {
                    windingPoint += toCenter.normalized * (halfWidth * 1.5f);
                }
            }

            foreach (var node in nodes)
            {
                Vector2 nodePos = new Vector2(node.position.x, node.position.z);
                float distToNode = Vector2.Distance(windingPoint, nodePos);
                float safeDist = nodeAvoidRadius + halfWidth * 2.0f;

                if (distToNode < safeDist)
                {
                    Vector2 pushDir = (windingPoint - nodePos).normalized;
                    windingPoint = nodePos + pushDir * safeDist;
                }
            }

            riverPath.Add(windingPoint);
        }

        for (int s = 0; s < 3; s++)
        {
            for (int j = 1; j < riverPath.Count - 1; j++)
            {
                riverPath[j] = (riverPath[j - 1] + riverPath[j] + riverPath[j + 1]) / 3f;
            }
        }

        foreach (var pt in riverPath)
        {
            foreach (var mtn in generatedWaterObjects)
            {
                if (mtn.shape == "blob" || mtn.shape == "mountain")
                {
                    if (IsPointInsideBlob(pt, mtn.center, mtn.radius * 0.8f, mtn.noiseOffsetX, mtn.noiseOffsetY))
                    {
                        return null;
                    }
                }
            }
        }

        return riverPath;
    }

    // === ОТРИСОВКА ГОРНЫХ ПИКОВ (ГРАДИЕНТ ОТ БЕЛОГО К ЦВЕТУ КАРТЫ) ===
    void DrawMountainPeakWithRidges(Color[] pixels, int resolution, float mapSize, Vector2 centerWorld, float baseRadius, Color snowColor, Color rockColor, List<PathData> paths)
    {
        float pixelsPerUnit = resolution / mapSize;
        int cx = Mathf.FloorToInt(centerWorld.x * pixelsPerUnit);
        int cy = Mathf.FloorToInt(centerWorld.y * pixelsPerUnit);

        int maxR = Mathf.CeilToInt(baseRadius * 1.6f * pixelsPerUnit);

        int minX = Mathf.Max(0, cx - maxR);
        int maxX = Mathf.Min(resolution - 1, cx + maxR);
        int minY = Mathf.Max(0, cy - maxR);
        int maxY = Mathf.Min(resolution - 1, cy + maxR);

        float verticalSquash = 0.6f;
        float peakOffsetY = baseRadius * 0.25f;

        Vector2 peakWorldPos = new Vector2(centerWorld.x, centerWorld.y + peakOffsetY);
        float snowRadius = baseRadius * 0.5f; // Зона белого снега

        List<float> ridgeAngles = new List<float>();
        int ridgeCount = Random.Range(3, 6);
        for (int i = 0; i < ridgeCount; i++)
        {
            ridgeAngles.Add(Random.Range(0f, Mathf.PI * 2));
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = x / pixelsPerUnit;
                float worldY = y / pixelsPerUnit;

                float dx = worldX - centerWorld.x;
                float dy = (worldY - centerWorld.y) / verticalSquash;
                float distSq = dx * dx + dy * dy;
                float dist = Mathf.Sqrt(distSq);

                if (dist > baseRadius) continue;

                float t = dist / baseRadius;
                Color finalColor;

                float distToPeakSq = dx * dx + ((worldY - peakOffsetY - centerWorld.y) / verticalSquash) * ((worldY - peakOffsetY - centerWorld.y) / verticalSquash);
                float distToPeak = Mathf.Sqrt(distToPeakSq);

                // === ГРАДИЕНТ: БЕЛЫЙ ВЕРХ -> СЕРЫЙ НИЗ ===
                if (distToPeak < snowRadius)
                {
                    // В зоне снега - белый с плавным переходом
                    float snowT = distToPeak / snowRadius;
                    finalColor = Color.Lerp(snowColor, rockColor, Mathf.SmoothStep(0f, 0.5f, snowT * snowT));
                }
                else
                {
                    // Вне зоны снега - переход к цвету карты
                    float rockStartT = snowRadius / baseRadius;
                    float rockT = Mathf.InverseLerp(rockStartT, 1.0f, t);
                    finalColor = Color.Lerp(rockColor, rockColor * 0.8f, Mathf.SmoothStep(0f, 1f, rockT));
                }

                // === ГРЕБНИ (хребты) ===
                float dxFromPeak = worldX - peakWorldPos.x;
                float dyFromPeak = (worldY - peakWorldPos.y) / verticalSquash;
                float distFromPeakFlat = Mathf.Sqrt(dxFromPeak * dxFromPeak + dyFromPeak * dyFromPeak);

                float ridgeIntensity = 0f;

                if (distFromPeakFlat > 0.5f)
                {
                    float angleFromPeak = Mathf.Atan2(dyFromPeak, dxFromPeak);
                    if (angleFromPeak < 0) angleFromPeak += Mathf.PI * 2;

                    float ridgeWidthRad = 0.25f;

                    foreach (float ridgeAngle in ridgeAngles)
                    {
                        float diff = Mathf.Abs(angleFromPeak - ridgeAngle);
                        if (diff > Mathf.PI) diff = (Mathf.PI * 2) - diff;

                        if (diff < ridgeWidthRad)
                        {
                            float fade = 1f - (diff / ridgeWidthRad);
                            float edgeFade = 1f - (dist / baseRadius);
                            float centerFade = Mathf.Clamp01(distFromPeakFlat / (snowRadius * 0.8f));

                            ridgeIntensity = Mathf.Max(ridgeIntensity, fade * edgeFade * centerFade);
                        }
                    }
                }

                if (ridgeIntensity > 0.05f)
                {
                    Color ridgeColor = new Color(0.3f, 0.3f, 0.35f);
                    finalColor = Color.Lerp(finalColor, ridgeColor, ridgeIntensity * 0.6f);
                }

                int idx = y * resolution + x;

                // Плавное затухание к краям
                float edgeFadeGlobal = 1f;
                if (t > 0.9f)
                {
                    edgeFadeGlobal = Mathf.Clamp01((1f - t) * 10f);
                }

                if (edgeFadeGlobal > 0.01f)
                {
                    pixels[idx] = Color.Lerp(pixels[idx], finalColor, edgeFadeGlobal);
                }
            }
        }
    }

    // === ОТРИСОВКА ОЗЁР (ИСПРАВЛЕНО - СВЕТЛЕЕ) ===
    void DrawOrganicBlob(Color[] pixels, int resolution, float mapSize, Vector2 centerWorld, float baseRadius, Color col, float noiseOffsetX, float noiseOffsetY)
    {
        float pixelsPerUnit = resolution / mapSize;
        int cx = Mathf.FloorToInt(centerWorld.x * pixelsPerUnit);
        int cy = Mathf.FloorToInt(centerWorld.y * pixelsPerUnit);

        int maxR = Mathf.CeilToInt(baseRadius * 2.5f * pixelsPerUnit);

        int minX = Mathf.Max(0, cx - maxR);
        int maxX = Mathf.Min(resolution - 1, cx + maxR);
        int minY = Mathf.Max(0, cy - maxR);
        int maxY = Mathf.Min(resolution - 1, cy + maxR);

        float noiseFrequencyMain = 0.10f;
        float noiseFrequencyDetail = 0.25f;
        float noiseAmplitudeMain = 0.7f;
        float noiseAmplitudeDetail = 0.15f;
        float asymmetryOffset = 200f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = x / pixelsPerUnit;
                float worldY = y / pixelsPerUnit;

                float dx = worldX - centerWorld.x;
                float dy = worldY - centerWorld.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > baseRadius * (1.0f + noiseAmplitudeMain + noiseAmplitudeDetail)) continue;

                float angle = Mathf.Atan2(dy, dx);

                float noiseVal1 = Mathf.PerlinNoise(
                    (Mathf.Cos(angle) * 2.0f + noiseOffsetX) * noiseFrequencyMain,
                    (Mathf.Sin(angle) * 2.0f + noiseOffsetY) * noiseFrequencyMain
                );

                float noiseVal2 = Mathf.PerlinNoise(
                    (Mathf.Cos(angle * 3.0f) + noiseOffsetX + asymmetryOffset) * noiseFrequencyDetail,
                    (Mathf.Sin(angle * 3.0f) + noiseOffsetY + asymmetryOffset) * noiseFrequencyDetail
                );

                float radiusLayer1 = (1.0f - noiseAmplitudeMain) + noiseVal1 * (noiseAmplitudeMain * 2.0f);
                float radiusLayer2 = 1.0f + (noiseVal2 - 0.5f) * (noiseAmplitudeDetail * 4.0f);

                float finalRadiusMultiplier = radiusLayer1 * radiusLayer2;
                float currentFinalDist = baseRadius * finalRadiusMultiplier;

                if (dist <= currentFinalDist)
                {
                    int idx = y * resolution + x;
                    float edgeDist = currentFinalDist - dist;
                    float alpha = 1f;

                    if (edgeDist < 3.0f)
                    {
                        alpha = Mathf.Clamp01(edgeDist / 3.0f);
                    }

                    if (alpha > 0.01f)
                    {
                        // ИСПРАВЛЕНО: используем более сильное смешивание
                        pixels[idx] = Color.Lerp(pixels[idx], col, alpha * 0.9f);
                    }
                }
            }
        }
    }

    // === ОТРИСОВКА ЗЕЛЕНОЙ ПОЛЯНЫ (ОРГАНИЧЕСКАЯ ФОРМА) ===
    void DrawGrassPatch(Color[] pixels, int resolution, float mapSize, Vector2 centerWorld, float baseRadius, Color grassColor, float noiseOffsetX, float noiseOffsetY)
    {
        float pixelsPerUnit = resolution / mapSize;
        int cx = Mathf.FloorToInt(centerWorld.x * pixelsPerUnit);
        int cy = Mathf.FloorToInt(centerWorld.y * pixelsPerUnit);

        int maxR = Mathf.CeilToInt(baseRadius * 2.5f * pixelsPerUnit);

        int minX = Mathf.Max(0, cx - maxR);
        int maxX = Mathf.Min(resolution - 1, cx + maxR);
        int minY = Mathf.Max(0, cy - maxR);
        int maxY = Mathf.Min(resolution - 1, cy + maxR);

        float noiseFrequencyMain = 0.12f;
        float noiseFrequencyDetail = 0.30f;
        float noiseAmplitudeMain = 0.8f;
        float noiseAmplitudeDetail = 0.2f;
        float asymmetryOffset = 300f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = x / pixelsPerUnit;
                float worldY = y / pixelsPerUnit;

                float dx = worldX - centerWorld.x;
                float dy = worldY - centerWorld.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > baseRadius * (1.0f + noiseAmplitudeMain + noiseAmplitudeDetail)) continue;

                float angle = Mathf.Atan2(dy, dx);

                float noiseVal1 = Mathf.PerlinNoise(
                    (Mathf.Cos(angle) * 2.0f + noiseOffsetX) * noiseFrequencyMain,
                    (Mathf.Sin(angle) * 2.0f + noiseOffsetY) * noiseFrequencyMain
                );

                float noiseVal2 = Mathf.PerlinNoise(
                    (Mathf.Cos(angle * 3.0f) + noiseOffsetX + asymmetryOffset) * noiseFrequencyDetail,
                    (Mathf.Sin(angle * 3.0f) + noiseOffsetY + asymmetryOffset) * noiseFrequencyDetail
                );

                float radiusLayer1 = (1.0f - noiseAmplitudeMain) + noiseVal1 * (noiseAmplitudeMain * 2.0f);
                float radiusLayer2 = 1.0f + (noiseVal2 - 0.5f) * (noiseAmplitudeDetail * 4.0f);

                float finalRadiusMultiplier = radiusLayer1 * radiusLayer2;
                float currentFinalDist = baseRadius * finalRadiusMultiplier;

                if (dist <= currentFinalDist)
                {
                    int idx = y * resolution + x;
                    float edgeDist = currentFinalDist - dist;
                    float alpha = 1f;

                    float blurZone = 5.0f;
                    if (edgeDist < blurZone)
                    {
                        alpha = Mathf.Clamp01(edgeDist / blurZone);
                    }

                    alpha *= grassColor.a;

                    if (alpha > 0.01f)
                    {
                        pixels[idx] = Color.Lerp(pixels[idx], grassColor, alpha * 0.7f);
                    }
                }
            }
        }
    }

    public void DrawGrassUnderSavedDecorations(int chapterIndex, string themeId)
    {
        string decorPath = GetDecorSavePath(chapterIndex);
        if (!File.Exists(decorPath))
        {
            Debug.LogWarning($"[DecorMan] Файл декораций не найден для травы: {decorPath}");
            return;
        }

        string texPath = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}{finalTextureSuffix}");
        if (!File.Exists(texPath))
        {
            Debug.LogWarning($"[DecorMan] Текстура не найдена для травы: {texPath}");
            return;
        }

        // 1. Загружаем координаты деревьев из сохранения
        string json = File.ReadAllText(decorPath);
        var wrapper = JsonUtility.FromJson<MapDecorationListWrapper>(json);
        if (wrapper == null || wrapper.items == null || wrapper.items.Count == 0) return;

        // 2. Загружаем текстуру
        byte[] bytes = File.ReadAllBytes(texPath);
        Texture2D tex = new Texture2D(2, 2);
        if (!tex.LoadImage(bytes)) return;

        int resolution = tex.width;
        Color[] pixels = tex.GetPixels();
        float mapSize = MapGenerator3D.Instance != null ? MapGenerator3D.Instance.mapSize : 200f;

        // 3. Находим конфиг травы для цвета
        Color grassColor = Color.green;
        DecorationConfig grassCfg = StoryContentLoader.AllDecorations?.Find(c => c.isPainted && c.shape == "grass");
        if (grassCfg != null && grassCfg.paintColor != default(Color))
        {
            grassColor = grassCfg.paintColor;
        }

        Debug.Log($"[DecorMan] Рисование травы под {wrapper.items.Count} объектами...");

        // Параметры для проверки воды
        float waterSafetyMargin = 3.5f; // Минимальное расстояние до воды (в единицах карты)

        // 4. Проходим по всем объектам и рисуем траву под деревьями
        foreach (var decor in wrapper.items)
        {
            if (string.IsNullOrEmpty(decor.prefabId)) continue;

            // Проверка: является ли объект деревом
            bool isTree = decor.prefabId.ToLower().Contains("tree");
            if (!isTree)
            {
                var cfg = StoryContentLoader.AllDecorations?.Find(c => c.prefabId == decor.prefabId);
                if (cfg != null && cfg.prefabId.ToLower().Contains("tree")) isTree = true;
            }

            if (isTree)
            {
                Vector2 pos = new Vector2(decor.position.x, decor.position.z);

                // === УМЕНЬШЕН РАЗМЕР ===
                // Было 2.5f, стало 1.8f - клякса будет меньше ствола дерева
                float baseSize = decor.scale.x * 1.8f;

                // === ПРОВЕРКА НА ВОДУ ===
                float minDistToWater = float.MaxValue;
                foreach (var waterObj in generatedWaterObjects)
                {
                    if (waterObj.shape == "circle" || waterObj.shape == "blob" || waterObj.shape == "mountain")
                    {
                        float dist = Vector2.Distance(pos, waterObj.center) - waterObj.radius;
                        if (dist < minDistToWater) minDistToWater = dist;
                    }
                    else if (waterObj.shape == "line")
                    {
                        float dist = GetDistanceToLineSegment(pos.x, pos.y, waterObj.center.x, waterObj.center.y, waterObj.endPoint.x, waterObj.endPoint.y) - (waterObj.width * 0.5f);
                        if (dist < minDistToWater) minDistToWater = dist;
                    }
                }

                // Если ближе чем safetyMargin к воде - уменьшаем размер или пропускаем
                if (minDistToWater < waterSafetyMargin + baseSize)
                {
                    if (minDistToWater < waterSafetyMargin * 0.5f)
                    {
                        // Слишком близко к воде - не рисуем траву здесь
                        continue;
                    }
                    else
                    {
                        // Близо к воде - уменьшаем размер кляксы
                        float reductionFactor = (minDistToWater - waterSafetyMargin * 0.5f) / (baseSize + waterSafetyMargin - waterSafetyMargin * 0.5f);
                        baseSize *= Mathf.Clamp01(reductionFactor);
                    }
                }

                if (baseSize < 0.5f) continue; // Слишком маленькая - не рисуем

                // Добавляем случайный шум для уникальности каждой кляксы
                float noiseX = Random.Range(0f, 1000f);
                float noiseY = Random.Range(0f, 1000f);

                DrawGrassPatch(pixels, resolution, mapSize, pos, baseSize, grassColor, noiseX, noiseY);
            }
        }

        // 5. Сохраняем обновленную текстуру
        tex.SetPixels(pixels);
        tex.Apply();
        SaveFinalTexture(tex, chapterIndex);

        Debug.Log("[DecorMan] Трава под деревьями нанесена на текстуру (с проверкой воды).");
    }

    public void DrawPathsOnTextureWithBridges(List<PathData> paths, int chapterIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}{finalTextureSuffix}");
        if (!File.Exists(path)) return;

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        if (!tex.LoadImage(bytes)) return;

        int resolution = tex.width;
        Color[] pixels = tex.GetPixels();
        float mapSize = MapGenerator3D.Instance.mapSize;

        Debug.Log("[DecorMan] Отрисовка мостов и путей...");

        DrawBridgesPrePass(pixels, resolution, paths, mapSize);
        DrawPathsSkippingBridges(pixels, resolution, paths, mapSize);

        tex.SetPixels(pixels);
        tex.Apply();
        SaveFinalTexture(tex, chapterIndex);

        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain != null) ApplyTextureToTerrain(terrain, tex);

        Debug.Log($"[DecorMan] Пути и мосты готовы для главы {chapterIndex}.");
    }

    void DrawBridgesPrePass(Color[] pixels, int resolution, List<PathData> paths, float mapSize)
    {
        float pixelsPerUnit = resolution / mapSize;
        float bridgeHalfWidthPx = (pathWidth * 0.5f + bridgeWidthMargin + 0.7f) * pixelsPerUnit;
        float edgeSoftness = 3.0f; // Зона размытия в пикселях

        foreach (var path in paths)
        {
            float totalDist = Vector2.Distance(path.start, path.end);
            int steps = Mathf.Max(30, Mathf.FloorToInt(totalDist * pathDetailPoints));
            Vector2 dir = (path.end - path.start).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float curveAmplitude = 4.0f;
            float noiseFrequency = 0.08f;

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 currentPos = Vector2.Lerp(path.start, path.end, t);
                float noiseInput = t * totalDist * noiseFrequency + MapGenerator3D.Instance.offsetX;
                float noiseValue = Mathf.PerlinNoise(noiseInput, 0f) * 2f - 1f;
                float fade = 1f - Mathf.Abs(t - 0.5f) * 2f;
                fade = Mathf.SmoothStep(0, 1, fade);
                if (t < 0.1f || t > 0.9f) fade *= (t < 0.1f ? t * 10f : (1f - t) * 10f);

                float offset = noiseValue * curveAmplitude * fade;
                Vector2 windingPos = currentPos + perp * offset;

                bool isOverWater = false;
                bool isOverMountain = false;

                foreach (var waterObj in generatedWaterObjects)
                {
                    if (waterObj.shape == "mountain")
                    {
                        if (IsPointInsideBlob(windingPos, waterObj.center, waterObj.radius, waterObj.noiseOffsetX, waterObj.noiseOffsetY))
                        {
                            isOverMountain = true;
                            break;
                        }
                    }
                    else if (waterObj.shape == "circle" || waterObj.shape == "blob")
                    {
                        if (IsPointInsideBlob(windingPos, waterObj.center, waterObj.radius, waterObj.noiseOffsetX, waterObj.noiseOffsetY))
                        {
                            isOverWater = true;
                            break;
                        }
                    }
                    else if (waterObj.shape == "line")
                    {
                        float dist = GetDistanceToLineSegment(windingPos.x, windingPos.y, waterObj.center.x, waterObj.center.y, waterObj.endPoint.x, waterObj.endPoint.y);
                        if (dist < (waterObj.width * 0.5f))
                        {
                            isOverWater = true;
                            break;
                        }
                    }
                }

                if (isOverWater && !isOverMountain)
                {
                    int cx = Mathf.FloorToInt(windingPos.x * pixelsPerUnit);
                    int cy = Mathf.FloorToInt(windingPos.y * pixelsPerUnit);
                    int r = Mathf.CeilToInt(bridgeHalfWidthPx);

                    // === ВАЖНО: Расширяем зону проверки на edgeSoftness ===
                    int minX = Mathf.Max(0, cx - r - (int)edgeSoftness);
                    int maxX = Mathf.Min(resolution - 1, cx + r + (int)edgeSoftness);
                    int minY = Mathf.Max(0, cy - r - (int)edgeSoftness);
                    int maxY = Mathf.Min(resolution - 1, cy + r + (int)edgeSoftness);

                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            int dx = x - cx;
                            int dy = y - cy;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            float edgeT = dist / r;

                            // === Вычисляем альфу с размытием ===
                            float alpha = 1f;

                            if (edgeT > 0.7f) // Начинаем затухание раньше
                            {
                                float edgeDist = r - dist; // Расстояние до края
                                alpha = Mathf.Clamp01(edgeDist / edgeSoftness);
                                alpha = Mathf.Pow(alpha, 0.6f); // Плавная кривая
                            }

                            if (alpha > 0.01f)
                            {
                                int idx = y * resolution + x;
                                pixels[idx] = Color.Lerp(pixels[idx], bridgeColor, alpha * 0.95f);
                            }
                        }
                    }
                }
            }
        }
    }

    // === НОВЫЙ МЕТОД: Отрисовка пути с органическими краями ===
    void DrawOrganicPath(Color[] pixels, int resolution, float mapSize, Vector2 start, Vector2 end, Color col, float width, float noiseOffsetX, float noiseOffsetY)
    {
        float pixelsPerUnit = resolution / mapSize;
        float halfWidthPx = (width * 0.5f) * pixelsPerUnit;
        float edgeSoftness = 3.0f;

        float totalDist = Vector2.Distance(start, end);
        int steps = Mathf.Max(40, Mathf.FloorToInt(totalDist * 3));
        Vector2 dir = (end - start).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 currentPos = Vector2.Lerp(start, end, t);

            // Добавляем шум для органичности
            float noiseInput = t * totalDist * 0.05f + noiseOffsetX;
            float noiseValue = Mathf.PerlinNoise(noiseInput, noiseOffsetY) * 2f - 1f;
            Vector2 windingPos = currentPos + perp * noiseValue * 2f;

            int cx = Mathf.FloorToInt(windingPos.x * pixelsPerUnit);
            int cy = Mathf.FloorToInt(windingPos.y * pixelsPerUnit);
            int r = Mathf.CeilToInt(halfWidthPx);

            int minX = Mathf.Max(0, cx - r - (int)edgeSoftness);
            int maxX = Mathf.Min(resolution - 1, cx + r + (int)edgeSoftness);
            int minY = Mathf.Max(0, cy - r - (int)edgeSoftness);
            int maxY = Mathf.Min(resolution - 1, cy + r + (int)edgeSoftness);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = 1f;
                    if (dist > r * 0.7f)
                    {
                        float edgeDist = r - dist;
                        alpha = Mathf.Clamp01(edgeDist / edgeSoftness);
                        alpha = Mathf.Pow(alpha, 0.6f);
                    }

                    if (alpha > 0.01f)
                    {
                        int idx = y * resolution + x;
                        pixels[idx] = Color.Lerp(pixels[idx], col, alpha * 0.85f);
                    }
                }
            }
        }
    }

    void DrawPathsSkippingBridges(Color[] pixels, int resolution, List<PathData> paths, float mapSize)
    {
        float pixelsPerUnit = resolution / mapSize;
        float halfPathPx = (pathWidth * 0.5f) * pixelsPerUnit;
        float edgeSoftness = 2.5f; // Зона размытия в пикселях

        foreach (var path in paths)
        {
            float totalDist = Vector2.Distance(path.start, path.end);
            int steps = Mathf.Max(30, Mathf.FloorToInt(totalDist * pathDetailPoints));
            Vector2 dir = (path.end - path.start).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float curveAmplitude = 4.0f;
            float noiseFrequency = 0.08f;

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 currentPos = Vector2.Lerp(path.start, path.end, t);
                float noiseInput = t * totalDist * noiseFrequency + MapGenerator3D.Instance.offsetX;
                float noiseValue = Mathf.PerlinNoise(noiseInput, 0f) * 2f - 1f;
                float fade = 1f - Mathf.Abs(t - 0.5f) * 2f;
                fade = Mathf.SmoothStep(0, 1, fade);
                if (t < 0.1f || t > 0.9f) fade *= (t < 0.1f ? t * 10f : (1f - t) * 10f);

                float offset = noiseValue * curveAmplitude * fade;
                Vector2 windingPos = currentPos + perp * offset;

                int cx = Mathf.FloorToInt(windingPos.x * pixelsPerUnit);
                int cy = Mathf.FloorToInt(windingPos.y * pixelsPerUnit);
                int r = Mathf.CeilToInt(halfPathPx);

                // === ВАЖНО: Расширяем зону проверки ===
                int minX = Mathf.Max(0, cx - r - (int)edgeSoftness);
                int maxX = Mathf.Min(resolution - 1, cx + r + (int)edgeSoftness);
                int minY = Mathf.Max(0, cy - r - (int)edgeSoftness);
                int maxY = Mathf.Min(resolution - 1, cy + r + (int)edgeSoftness);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int dx = x - cx;
                        int dy = y - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float edgeT = dist / r;

                        // === Вычисляем альфу с размытием ===
                        float alpha = 1f;

                        if (edgeT > 0.7f)
                        {
                            float edgeDist = r - dist;
                            alpha = Mathf.Clamp01(edgeDist / edgeSoftness);
                            alpha = Mathf.Pow(alpha, 0.6f);
                        }

                        if (alpha > 0.01f)
                        {
                            int idx = y * resolution + x;

                            if (pixels[idx] == bridgeColor) continue;

                            pixels[idx] = Color.Lerp(pixels[idx], pathColor, alpha * 0.9f);
                        }
                    }
                }
            }
        }
    }

    private bool IsPositionBlockedByNodes(Vector2 pos, List<StoryNode> nodes, float avoidRadius = -1f)
    {
        if (nodes == null) return false;

        float radius = (avoidRadius > 0f) ? avoidRadius : nodeAvoidRadius;

        foreach (var node in nodes)
        {
            if (Vector2.Distance(pos, new Vector2(node.position.x, node.position.z)) < radius)
                return true;
        }
        return false;
    }

    void DrawThickLineOnPixels(Color[] pixels, int resolution, float mapSize, Vector2 startWorld, Vector2 endWorld, float widthWorld, Color col)
    {
        float pixelsPerUnit = resolution / mapSize;
        Vector2 startPx = startWorld * pixelsPerUnit;
        Vector2 endPx = endWorld * pixelsPerUnit;
        int widthPx = Mathf.CeilToInt(widthWorld * pixelsPerUnit);

        float dist = Vector2.Distance(startPx, endPx);
        int steps = Mathf.CeilToInt(dist);
        Vector2 dir = (endPx - startPx).normalized;

        for (int i = 0; i <= steps; i++)
        {
            Vector2 pt = startPx + dir * i;
            int cx = Mathf.FloorToInt(pt.x);
            int cy = Mathf.FloorToInt(pt.y);
            int r = widthPx / 2;

            int minX = Mathf.Max(0, cx - r);
            int maxX = Mathf.Min(resolution - 1, cx + r);
            int minY = Mathf.Max(0, cy - r);
            int maxY = Mathf.Min(resolution - 1, cy + r);
            int rSq = r * r;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= rSq)
                    {
                        int idx = y * resolution + x;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float t = d / r;
                        float alpha = 1f;
                        if (t > 0.8f) alpha = Mathf.Pow(1f - t, 0.5f) * 1.2f;
                        pixels[idx] = Color.Lerp(pixels[idx], col, alpha);
                    }
                }
            }
        }
    }

    Texture2D GeneratePaperTextureWithFolds(float[,] heightData, int resolution, List<FoldLine> folds, float mapSize, string theme = "")
    {
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[resolution * resolution];

        // === ЦВЕТА ИЗ JSON КОНФИГА ===
        Color basePaperLight = new Color(0.75f, 0.65f, 0.45f); // Default
        Color basePaperDark = new Color(0.45f, 0.35f, 0.20f);
        Color foldShadowColor = new Color(0.35f, 0.28f, 0.18f);
        Color edgeBurnColor = new Color(0.25f, 0.20f, 0.12f);

        Debug.Log($"[DEBUG] Тема для генерации: '{theme}'");
        Debug.Log($"[DEBUG] AllDecorations загружен? {StoryContentLoader.AllDecorations != null}");

        // Пытаемся найти конфиг фона для текущей темы
        if (StoryContentLoader.AllDecorations != null && !string.IsNullOrEmpty(theme))
        {
            var themeBgConfig = StoryContentLoader.AllDecorations.Find(
                c => c.isPainted && c.shape == "theme_background" &&
                     (c.allowedThemes == null || c.allowedThemes.Contains(theme))
            );

            Debug.Log($"[DEBUG] Найден theme_background конфиг? {themeBgConfig != null}");

            if (themeBgConfig != null)
            {
                Debug.Log($"[DEBUG] basePaperLight из JSON: {themeBgConfig.basePaperLight}");
                Debug.Log($"[DEBUG] basePaperDark из JSON: {themeBgConfig.basePaperDark}");

                // Используем цвета из JSON если они заданы (не default)
                if (themeBgConfig.basePaperLight != default(Color))
                {
                    basePaperLight = themeBgConfig.basePaperLight;
                    Debug.Log($"[DEBUG] Установлен basePaperLight: {basePaperLight}");
                }
                if (themeBgConfig.basePaperDark != default(Color))
                {
                    basePaperDark = themeBgConfig.basePaperDark;
                    Debug.Log($"[DEBUG] Установлен basePaperDark: {basePaperDark}");
                }
                if (themeBgConfig.foldShadowColor != default(Color))
                    foldShadowColor = themeBgConfig.foldShadowColor;
                if (themeBgConfig.edgeBurnColor != default(Color))
                    edgeBurnColor = themeBgConfig.edgeBurnColor;
            }
            else
            {
                Debug.LogWarning($"[DEBUG] Не найден theme_background для темы '{theme}'");
                // Выведем все доступные theme_background
                foreach (var decor in StoryContentLoader.AllDecorations)
                {
                    if (decor.isPainted && decor.shape == "theme_background")
                    {
                        Debug.Log($"[DEBUG] Доступен theme_background с allowedThemes: {string.Join(", ", decor.allowedThemes ?? new List<string>())}");
                    }
                }
            }
        }

        int index = 0;
        float centerX = resolution * 0.5f;
        float centerY = resolution * 0.5f;
        float maxDistSqr = centerX * centerX + centerY * centerY;
        float fScaleX = 0.15f;
        float fScaleY = 0.05f;
        float fOffX = Random.value * 100f;
        float fOffY = Random.value * 100f;

        for (int y = 0; y < resolution; y++)
        {
            float worldZ = (float)y / (resolution - 1) * mapSize;
            float dy = y - centerY;
            for (int x = 0; x < resolution; x++)
            {
                float worldX = (float)x / (resolution - 1) * mapSize;
                float h = heightData[y, x];
                Color col = Color.Lerp(basePaperDark, basePaperLight, h * h * 0.9f + h * 0.1f);

                if (folds != null && folds.Count > 0)
                {
                    float closestDist = 1000f;
                    float nearestWidth = 0f;
                    for (int i = 0; i < folds.Count; i++)
                    {
                        var fold = folds[i];
                        float dist = GetDistanceToLineSegment(worldX, worldZ, fold.startX, fold.startZ, fold.endX, fold.endZ);
                        float drawWidth = fold.width * 0.25f;
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
                            float shadow = Mathf.Pow(intensity, 10.0f);
                            float mix = shadow * 0.4f;
                            if (mix > 0.001f)
                            {
                                col.r = Mathf.Lerp(col.r, foldShadowColor.r, mix);
                                col.g = Mathf.Lerp(col.g, foldShadowColor.g, mix);
                                col.b = Mathf.Lerp(col.b, foldShadowColor.b, mix);
                            }
                        }
                    }
                }

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

    void SaveFinalTexture(Texture2D tex, int chapterIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}{finalTextureSuffix}");
        byte[] bytes = tex.EncodeToPNG();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
    }

    void ApplyTextureToTerrain(Terrain terrain, Texture2D tex)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (sh == null) return;
        if (terrain.materialTemplate == null || terrain.materialTemplate.shader != sh)
            terrain.materialTemplate = new Material(sh);
        Material mat = terrain.materialTemplate;
        mat.SetTexture("_BaseMap", tex);
        mat.SetTexture("_MainTex", tex);
        mat.SetFloat("_Glossiness", 0f);
        mat.SetFloat("_Smoothness", 0.05f);
        mat.SetFloat("_Metallic", 0f);
        Renderer r = terrain.GetComponent<Renderer>();
        if (r != null) r.material = mat;
    }

    public void LoadAndApplySavedPaths(int chapterIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}{finalTextureSuffix}");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[DecorMan] Файл текстуры не найден: {path}.");
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);

        if (tex.LoadImage(bytes))
        {
            tex.filterMode = FilterMode.Point;
            Terrain terrain = FindObjectOfType<Terrain>();
            if (terrain != null)
            {
                ApplyTextureToTerrain(terrain, tex);
                Debug.Log("[DecorMan] Текстура загружена из файла (МГНОВЕННО).");
            }
        }
    }

    public void LoadAndSpawnDecorations(int chapterIndex)
    {
        string path = GetDecorSavePath(chapterIndex);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            var wrapper = JsonUtility.FromJson<MapDecorationListWrapper>(json);
            if (wrapper != null) currentDecorations = wrapper.items;
            else currentDecorations = new List<MapDecoration>();
        }
        else
        {
            Debug.LogWarning($"[DecorMan] Файл декораций не найден: {path}");
            return;
        }

        if (currentDecorations.Count == 0) return;

        if (decorContainer == null)
        {
            decorContainer = new GameObject("DecorationsContainer");
            decorContainer.transform.position = Vector3.zero;
        }
        else
        {
            foreach (Transform t in decorContainer.transform) Destroy(t.gameObject);
        }

        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null || terrain.terrainData == null) return;

        TerrainData data = terrain.terrainData;
        int spawnedCount = 0;

        foreach (var d in currentDecorations)
        {
            var cfg = StoryContentLoader.AllDecorations.Find(c => c.prefabId == d.prefabId);
            if (cfg == null) continue;

            GameObject pref = Resources.Load<GameObject>(cfg.prefabPath);
            if (pref == null) continue;

            GameObject obj = Instantiate(pref, decorContainer.transform);

            float nx = Mathf.Clamp01((d.position.x - terrain.transform.position.x) / data.size.x);
            float nz = Mathf.Clamp01((d.position.z - terrain.transform.position.z) / data.size.z);
            float hNormalized = data.GetInterpolatedHeight(nx, nz);
            float finalY = terrain.transform.position.y + hNormalized + cfg.heightOffset;

            obj.transform.SetPositionAndRotation(new Vector3(d.position.x, finalY, d.position.z), d.rotation);
            obj.transform.localScale = d.scale;
            spawnedCount++;
        }
        Debug.Log($"[DecorMan] Заспавнено {spawnedCount} объектов.");
    }

    string GetDecorSavePath(int i) => Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{i}{decorSaveSuffix}");

    void SaveDecorations(List<MapDecoration> list, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(new MapDecorationListWrapper { items = list }, true));
    }

    float GetDistanceToLineSegment(float px, float pz, float x1, float z1, float x2, float z2)
    {
        float A = px - x1, B = pz - z1, C = x2 - x1, D = z2 - z1;
        float dot = A * C + B * D, lenSq = C * C + D * D;
        float param = lenSq != 0 ? dot / lenSq : -1;
        float xx, yy;
        if (param < 0) { xx = x1; yy = z1; }
        else if (param > 1) { xx = x2; yy = z2; }
        else { xx = x1 + param * C; yy = z1 + param * D; }
        return Mathf.Sqrt((px - xx) * (px - xx) + (pz - yy) * (pz - yy));
    }
}
