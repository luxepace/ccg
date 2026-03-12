using UnityEngine;

public class MapDecoration : MonoBehaviour
{
    [Header("Деревья")]
    public GameObject[] treePrefabs;
    public int treeCount = 50;

    [Header("Настройки")]
    public float minHeight = 0.3f;
    public float maxHeight = 0.8f;

    private Terrain terrain;
    private TerrainData terrainData;

    void Start()
    {
        terrain = Terrain.activeTerrain;
        terrainData = terrain.terrainData;

        PlaceTrees();
    }

    void PlaceTrees()
    {
        TreeInstance[] trees = new TreeInstance[treeCount];

        for (int i = 0; i < treeCount; i++)
        {
            TreeInstance tree = new TreeInstance();

            // Случайная позиция
            tree.position = new Vector3(
                Random.Range(0f, 1f),
                Random.Range(minHeight, maxHeight),
                Random.Range(0f, 1f)
            );

            // Случайное дерево
            tree.prototypeIndex = Random.Range(0, treePrefabs.Length);

            // Случайный размер
            tree.widthScale = Random.Range(0.8f, 1.2f);
            tree.heightScale = Random.Range(0.8f, 1.2f);

            tree.color = Color.white;
            tree.lightmapColor = Color.white;

            trees[i] = tree;
        }

        terrainData.treeInstances = trees;
    }
}