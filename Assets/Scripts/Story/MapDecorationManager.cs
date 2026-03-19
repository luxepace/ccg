using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class MapDecorationManager : MonoBehaviour
{
    public static MapDecorationManager Instance;

    [Header("Настройки")]
    public float generationStep = 4f;

    [Header("Настройки путей")]
    public float pathWidth = 2.2f;       // Ширина тропы
    public float pathCurveStrength = 4f; // Сила изгиба
    public int pathSegments = 10;        // Количество точек изгиба (чем больше, тем плавнее, но медленнее)
    public Color pathColor = new Color(0.3f, 0.18f, 0.08f); // Коричневый

    private string baseSaveFileName = "map_chapter_";
    private string decorSaveSuffix = "_decor.json";
    private string pathTextureSuffix = "_pathTex.png"; // Суффикс для сохраненной текстуры

    private List<MapDecoration> currentDecorations = new List<MapDecoration>();
    private GameObject decorContainer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void GenerateAndSaveDecorations(int chapterIndex, string themeId, List<FoldLine> folds)
    {
        // Логика генерации декораций (без изменений, она быстрая)
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
    }

    /// <summary>
    /// ГЛАВНЫЙ МЕТОД: Берет ГОТОВУЮ текстуру с террейна, рисует пути и сохраняет.
    /// НЕ ПЕРЕРИСОВЫВАЕТ БУМАГУ И СКЛАДКИ.
    /// </summary>
    public void DrawPathsOnTexture(List<PathData> paths, int chapterIndex)
    {
        Terrain terrain = FindObjectOfType<Terrain>();
        if (terrain == null || terrain.terrainData == null) return;

        // 1. БЕРЕМ ТЕКУЩУЮ ТЕКСТУРУ С ТЕРРЕЙНА (там уже есть бумага и складки!)
        Texture2D currentTex = terrain.materialTemplate.GetTexture("_MainTex") as Texture2D;

        if (currentTex == null)
        {
            Debug.LogError("[DecorMan] На террейне нет текстуры! Сначала сгенерируйте ландшафт.");
            return;
        }

        // 2. Читаем пиксели в память
        int resolution = currentTex.width;
        Color[] pixels = currentTex.GetPixels();

        // 3. Рисуем пути прямо по этим пикселям
        DrawCurvedPathsOnPixels(pixels, resolution, paths);

        // 4. Применяем изменения обратно
        currentTex.SetPixels(pixels);
        currentTex.Apply();

        // 5. СОХРАНЯЕМ ИТОГОВУЮ КАРТИНКУ В ФАЙЛ ДЛЯ МГНОВЕННОЙ ЗАГРУЗКИ
        SavePathTexture(currentTex, chapterIndex);

        Debug.Log($"[DecorMan] Пути нарисованы поверх готовой текстуры и сохранены (Глава {chapterIndex}).");
    }

    /// <summary>
    /// МГНОВЕННАЯ ЗАГРУЗКА: Просто читает файл и ставит на террейн.
    /// Никакой генерации!
    /// </summary>
    public void LoadAndApplySavedPaths(int chapterIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}{pathTextureSuffix}");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[DecorMan] Файл путей не найден: {path}.");
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
                Debug.Log("[DecorMan] Пути загружены из файла (МГНОВЕННО).");
            }
        }
    }

    // Рисование извилистых линий по массиву пикселей
    void DrawCurvedPathsOnPixels(Color[] pixels, int resolution, List<PathData> paths)
    {
        float mapSize = MapGenerator3D.Instance.mapSize;
        float halfW = pathWidth * 0.5f;

        foreach (var path in paths)
        {
            // Генерируем точки кривой ОДИН РАЗ для этого пути
            List<Vector2> curvePoints = GenerateCurvedPathPoints(path.start, path.end);

            // Проходим по всем пикселям (можно оптимизировать через bounding box, но для старта пойдет)
            // Чтобы было быстрее, проверяем расстояние до сегментов кривой
            for (int i = 0; i < pixels.Length; i++)
            {
                int x = i % resolution;
                int y = i / resolution;

                float worldX = (float)x / (resolution - 1) * mapSize;
                float worldZ = (float)y / (resolution - 1) * mapSize;

                bool onPath = false;
                // Проверяем расстояние до каждого сегмента кривой
                for (int j = 0; j < curvePoints.Count - 1; j++)
                {
                    float dist = GetDistanceToLineSegment(worldX, worldZ, curvePoints[j].x, curvePoints[j].y, curvePoints[j + 1].x, curvePoints[j + 1].y);

                    if (dist < halfW)
                    {
                        float t = dist / halfW;
                        // Мягкий край
                        float alpha = Mathf.Pow(1f - t, 0.5f);

                        // Смешиваем цвет пути с текущим цветом пикселя (бумаги/складки)
                        pixels[i] = Color.Lerp(pixels[i], pathColor, alpha * 0.9f);
                        onPath = true;
                        break; // Пиксель закрашен, переходим к следующему
                    }
                }
            }
        }
    }

    /// <summary>
    /// Генерирует точки для извилистой линии с ЧАСТЫМИ, но НЕБОЛЬШИМИ поворотами.
    /// Гарантирует перекрытие сегментов за счет достаточного количества точек.
    /// </summary>
    List<Vector2> GenerateCurvedPathPoints(Vector2 start, Vector2 end)
    {
        List<Vector2> points = new List<Vector2>();

        // Берем чуть больше сегментов для гарантии сплошности на резких поворотах
        // Даже если в настройках стоит 30, здесь возьмем 50-60 для качества линии.
        int segments = Mathf.Max(pathSegments, 20);

        Vector2 dir = (end - start).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        // === НАСТРОЙКИ ДЛЯ "ЧАСТОГО ВИЛЯНИЯ" ===
        float curveStrength = 2.5f;      // УМЕНЬШИЛИ: было 8. Теперь отклонение небольшое (всего +/- 2.5 единицы)
        float noiseFrequency = 0.25f;    // УВЕЛИЧИЛИ: было 0.15. Теперь шум меняется чаще, создавая частые зигзаги

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector2 p = Vector2.Lerp(start, end, t);

            // Шум только в центре, чтобы у нод вход был ровным
            if (i > 4 && i < segments - 4)
            {
                // Частый шум дает много мелких поворотов
                float noise = Mathf.PerlinNoise(p.x * noiseFrequency + MapGenerator3D.Instance.offsetX,
                                                p.y * noiseFrequency + MapGenerator3D.Instance.offsetY) * 2f - 1f;

                // Плавное затухание к краям
                float fade = 1f - Mathf.Abs(t - 0.5f) * 2f;
                fade = fade * fade;

                // Небольшое, но частое смещение
                p += perp * noise * curveStrength * fade;
            }
            points.Add(p);
        }
        return points;
    }

    void SavePathTexture(Texture2D tex, int chapterIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{baseSaveFileName}{chapterIndex}{pathTextureSuffix}");
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

    // --- Декорации (спавн объектов) ---
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