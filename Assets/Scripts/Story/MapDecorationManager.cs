using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class MapDecorationManager : MonoBehaviour
{
    public static MapDecorationManager Instance;

    [Header("Настройки")]
    public float generationStep = 4f;

    [Header("Настройки путей")]
    // Толщина линии (сделал чуть тоньше для аккуратности, можно вернуть 2.5 если нужно)
    public float pathWidth = 2.0f;
    public Color pathColor = new Color(0.35f, 0.22f, 0.10f);

    // Детализация: сколько точек просчета на единицу длины
    [Range(1, 10)]
    public int pathDetailPoints = 4;

    private string baseSaveFileName = "map_chapter_";
    private string decorSaveSuffix = "_decor.json";
    private string finalTextureSuffix = "_final.png";

    private List<MapDecoration> currentDecorations = new List<MapDecoration>();
    private GameObject decorContainer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void GenerateBasePaperTexture(int chapterIndex, float[,] heights, List<FoldLine> folds)
    {
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null || terrain.terrainData == null) return;

        Debug.Log($"[DecorMan] Генерация базовой текстуры (бумага) для главы {chapterIndex}...");

        int resolution = terrain.terrainData.heightmapResolution;
        float mapSize = terrain.terrainData.size.x;

        Texture2D texture = GeneratePaperTextureWithFolds(heights, resolution, folds, mapSize);

        SaveFinalTexture(texture, chapterIndex);
        ApplyTextureToTerrain(terrain, texture);

        Debug.Log($"[DecorMan] Базовая текстура главы {chapterIndex} сохранена.");
    }

    public void GenerateObjectDecorationsOnly(int chapterIndex, string themeId, List<FoldLine> folds)
    {
        currentDecorations.Clear();
        if (StoryContentLoader.AllDecorations == null) return;
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null) return;
        float mapSize = terrain.terrainData.size.x;

        foreach (var cfg in StoryContentLoader.AllDecorations)
        {
            if (cfg.allowedThemes.Count > 0 && !cfg.allowedThemes.Contains(themeId)) continue;

            for (float x = 0; x < mapSize; x += generationStep)
            {
                for (float z = 0; z < mapSize; z += generationStep)
                {
                    if (Random.value > cfg.density * generationStep * generationStep) continue;

                    float h = MapGenerator3D.GetTerrainHeightAt(x, z);
                    if (h < cfg.minHeight || h > cfg.maxHeight) continue;

                    if (cfg.avoidPaths && folds != null)
                    {
                        bool onFold = false;
                        foreach (var f in folds)
                        {
                            if (GetDistanceToLineSegment(x, z, f.startX, f.startZ, f.endX, f.endZ) < f.width * 1.2f)
                            { onFold = true; break; }
                        }
                        if (onFold) continue;
                    }

                    MapDecoration d = new MapDecoration();
                    d.prefabId = cfg.prefabId;
                    d.position = new Vector3(x, h, z);
                    d.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    d.scale = Vector3.one * Random.Range(cfg.minScale, cfg.maxScale);
                    currentDecorations.Add(d);
                }
            }
        }
        SaveDecorations(currentDecorations, GetDecorSavePath(chapterIndex));
        currentDecorations.Clear();
        Debug.Log($"[DecorMan] Объекты декораций для главы {chapterIndex} сохранены.");
    }

    public void DrawPathsOnTexture(List<PathData> paths, int chapterIndex)
    {
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

        DrawWindingPathsOnPixels(pixels, resolution, paths);

        tex.SetPixels(pixels);
        tex.Apply();

        SaveFinalTexture(tex, chapterIndex);

        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain != null) ApplyTextureToTerrain(terrain, tex);

        Debug.Log($"[DecorMan] Извилистые пути нарисованы для главы {chapterIndex}.");
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

    // === ГЕНЕРАЦИЯ БУМАГИ (Без изменений) ===
    Texture2D GeneratePaperTextureWithFolds(float[,] heightData, int resolution, List<FoldLine> folds, float mapSize)
    {
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[resolution * resolution];

        Color basePaperLight = new Color(0.75f, 0.65f, 0.45f);
        Color basePaperDark = new Color(0.45f, 0.35f, 0.20f);
        Color foldShadowColor = new Color(0.35f, 0.28f, 0.18f);
        Color edgeBurnColor = new Color(0.25f, 0.20f, 0.12f);

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

    // === НОВАЯ ЛОГИКА: ИЗВИЛИСТЫЕ ПУТИ С ГЛАДКИМИ КРАЯМИ ===
    void DrawWindingPathsOnPixels(Color[] pixels, int resolution, List<PathData> paths)
    {
        float mapSize = MapGenerator3D.Instance.mapSize;
        float halfW = pathWidth * 0.5f;

        foreach (var path in paths)
        {
            float totalDist = Vector2.Distance(path.start, path.end);
            // Количество шагов
            int steps = Mathf.Max(30, Mathf.FloorToInt(totalDist * pathDetailPoints));

            Vector2 dir = (path.end - path.start).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            // Параметры извилистости
            float curveAmplitude = 4.0f;  // СИЛЬНОЕ отклонение (виляние)
            float noiseFrequency = 0.08f; // НИЗКАЯ частота (плавные длинные изгибы)

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;

                // Базовая точка на прямой
                Vector2 currentPos = Vector2.Lerp(path.start, path.end, t);

                // Вычисляем смещение используя Перлин-шум для плавности
                // Используем координату вдоль пути как вход для шума
                float noiseInput = t * totalDist * noiseFrequency + MapGenerator3D.Instance.offsetX;
                float noiseValue = Mathf.PerlinNoise(noiseInput, 0f) * 2f - 1f; // От -1 до 1

                // Применяем смещение перпендикулярно направлению
                // Делаем затухание на самых концах (у нод), чтобы вход был ровным
                float fade = 1f - Mathf.Abs(t - 0.5f) * 2f;
                fade = Mathf.SmoothStep(0, 1, fade); // Плавное затухание

                // На концах (первые и последние 10%) смещение почти нулевое
                if (t < 0.1f || t > 0.9f) fade *= (t < 0.1f ? t * 10f : (1f - t) * 10f);

                float offset = noiseValue * curveAmplitude * fade;

                Vector2 windingPos = currentPos + perp * offset;

                // Рисуем четкий круг в этой точке
                DrawSolidCircleOnPixels(pixels, resolution, mapSize, windingPos, halfW);
            }
        }
    }

    // Рисует ТВЕРДЫЙ круг с мягким краем (антиалиасинг только по краю круга)
    void DrawSolidCircleOnPixels(Color[] pixels, int resolution, float mapSize, Vector2 centerWorld, float radiusWorld)
    {
        float pixelsPerUnit = resolution / mapSize;
        int cx = Mathf.FloorToInt(centerWorld.x * pixelsPerUnit);
        int cy = Mathf.FloorToInt(centerWorld.y * pixelsPerUnit);
        int r = Mathf.CeilToInt(radiusWorld * pixelsPerUnit);

        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(resolution - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(resolution - 1, cy + r);

        float rSquared = r * r;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                int distSq = dx * dx + dy * dy;

                if (distSq <= rSquared)
                {
                    int idx = y * resolution + x;

                    // Вычисляем прозрачность ТОЛЬКО для края круга (для сглаживания)
                    float dist = Mathf.Sqrt(distSq);
                    float edgeT = dist / r;

                    // Если пиксель глубоко внутри круга -> alpha = 1
                    // Если на краю -> alpha плавно падает
                    float alpha = 1f;
                    if (edgeT > 0.8f)
                    {
                        alpha = Mathf.Pow(1f - edgeT, 0.5f) * 1.25f; // Мягкий край
                        if (alpha > 1f) alpha = 1f;
                    }

                    // Смешиваем цвет пути с фоном
                    pixels[idx] = Color.Lerp(pixels[idx], pathColor, alpha * 0.9f);
                }
            }
        }
    }

    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

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

    public void LoadAndSpawnDecorations(int chapterIndex)
    {
        string path = GetDecorSavePath(chapterIndex);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            var wrapper = JsonUtility.FromJson<MapDecorationListWrapper>(json);
            if (wrapper != null) currentDecorations = wrapper.items;
        }

        if (decorContainer == null)
        {
            decorContainer = new GameObject("DecorationsContainer");
            decorContainer.transform.position = Vector3.zero;
        }
        else
        {
            foreach (Transform t in decorContainer.transform) Destroy(t.gameObject);
        }

        foreach (var d in currentDecorations)
        {
            var cfg = StoryContentLoader.AllDecorations.Find(c => c.prefabId == d.prefabId);
            if (cfg == null) continue;
            GameObject pref = Resources.Load<GameObject>(cfg.prefabPath);
            if (pref != null)
            {
                GameObject obj = Instantiate(pref, decorContainer.transform);
                obj.transform.SetPositionAndRotation(d.position, d.rotation);
                obj.transform.localScale = d.scale;
            }
        }
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

    [System.Serializable] public class MapDecorationListWrapper { public List<MapDecoration> items; }
}