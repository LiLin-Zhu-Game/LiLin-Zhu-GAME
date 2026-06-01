using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GearScavengerEditorPreview
{
    private const string PreviewRootName = "Gear Scavenger Editor Preview";
    private static readonly HashSet<Vector2Int> walkable = new HashSet<Vector2Int>();

    static GearScavengerEditorPreview()
    {
        EditorApplication.delayCall += RebuildPreview;
        EditorSceneManager.sceneOpened += (_, _) => EditorApplication.delayCall += RebuildPreview;
    }

    [MenuItem("Tools/Gear Scavenger/Rebuild Editor Map Preview")]
    public static void RebuildPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        RemoveExistingPreview();
        walkable.Clear();

        AddRect(0, 0, 10, 8);
        AddRect(-13, 0, 9, 8);
        AddRect(13, 0, 9, 8);
        AddRect(0, 10, 8, 7);
        AddRect(0, -10, 10, 7);
        AddRect(-6, 0, 4, 3);
        AddRect(6, 0, 4, 3);
        AddRect(0, 5, 3, 4);
        AddRect(0, -5, 3, 4);

        GameObject root = new GameObject(PreviewRootName);
        root.hideFlags = HideFlags.DontSaveInBuild;

        foreach (Vector2Int cell in walkable)
        {
            AddTile(root.transform, cell.x, cell.y);
        }

        HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in walkable)
        {
            AddWallIfNeeded(walls, cell + Vector2Int.up);
            AddWallIfNeeded(walls, cell + Vector2Int.down);
            AddWallIfNeeded(walls, cell + Vector2Int.left);
            AddWallIfNeeded(walls, cell + Vector2Int.right);
        }

        foreach (Vector2Int wall in walls)
        {
            AddWall(root.transform, wall.x, wall.y);
        }

        AddLabel(root.transform, "Start Room", new Vector3(0f, 4.85f, 0f), new Color(0.5f, 1f, 0.95f, 1f));
        AddLabel(root.transform, "Left Combat Room", new Vector3(-13f, 4.85f, 0f), new Color(1f, 0.55f, 0.38f, 1f));
        AddLabel(root.transform, "Right Combat Room", new Vector3(13f, 4.85f, 0f), new Color(1f, 0.55f, 0.38f, 1f));
        AddLabel(root.transform, "North Cache Room", new Vector3(0f, 13.85f, 0f), new Color(0.55f, 0.85f, 1f, 1f));
        AddLabel(root.transform, "South Boss Room", new Vector3(0f, -5.85f, 0f), new Color(1f, 0.35f, 0.8f, 1f));

        AddObstacle(root.transform, -15, 2, 1.15f);
        AddObstacle(root.transform, -12, -2, 0.9f);
        AddObstacle(root.transform, -9, 2, 0.85f);
        AddObstacle(root.transform, -3, 2, 0.8f);
        AddObstacle(root.transform, 3, -2, 0.9f);
        AddObstacle(root.transform, 10, 2, 1.0f);
        AddObstacle(root.transform, 15, -2, 1.2f);
        AddObstacle(root.transform, -2, 11, 1.05f);
        AddObstacle(root.transform, 2, 9, 0.9f);
        AddObstacle(root.transform, -2, -9, 1.05f);
        AddObstacle(root.transform, 3, -11, 1.25f);

        AddFloorFeature(root.transform, -13, 0, 2.2f, 0.95f, new Color(0.18f, 0.5f, 0.52f, 0.5f));
        AddFloorFeature(root.transform, 0, 10, 2.6f, 1.1f, new Color(0.16f, 0.75f, 0.95f, 0.42f));
        AddFloorFeature(root.transform, 13, 0, 2.4f, 0.9f, new Color(0.35f, 0.1f, 0.08f, 0.42f));
        AddFloorFeature(root.transform, 0, -10, 2.7f, 1.15f, new Color(0.42f, 0.12f, 0.5f, 0.38f));

        AddMarker(root.transform, "Starter Weapons", new Vector3(0f, -2.25f, 0f), new Color(0.35f, 0.9f, 1f, 0.9f), 0.58f);
        AddMarker(root.transform, "Repair Station", new Vector3(-2f, 0f, 0f), new Color(0.35f, 1f, 0.85f, 0.95f), 0.48f);
        AddMarker(root.transform, "Cooling Station", new Vector3(0f, 9f, 0f), new Color(0.2f, 0.65f, 1f, 0.95f), 0.48f);
        AddMarker(root.transform, "Boss Core", new Vector3(0f, -10f, 0f), new Color(1f, 0.05f, 0.75f, 0.95f), 0.7f);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Gear Scavenger/Clear Editor Map Preview")]
    public static void RemoveExistingPreview()
    {
        GameObject existing = GameObject.Find(PreviewRootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    private static void AddRect(int centerX, int centerY, int width, int height)
    {
        int minX = centerX - width / 2;
        int maxX = centerX + width / 2;
        int minY = centerY - height / 2;
        int maxY = centerY + height / 2;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                walkable.Add(new Vector2Int(x, y));
            }
        }
    }

    private static void AddWallIfNeeded(HashSet<Vector2Int> walls, Vector2Int cell)
    {
        if (!walkable.Contains(cell))
        {
            walls.Add(cell);
        }
    }

    private static void AddTile(Transform root, int x, int y)
    {
        GameObject tile = new GameObject("Preview Floor");
        tile.transform.SetParent(root, false);
        tile.transform.position = new Vector3(x, y, 1f);
        tile.transform.localScale = Vector3.one * 1.03f;
        SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = PreviewSpriteCache.Square;
        renderer.color = (x + y) % 2 == 0 ? new Color(0.19f, 0.22f, 0.25f, 0.82f) : new Color(0.15f, 0.18f, 0.2f, 0.82f);
        renderer.sortingOrder = -50;
    }

    private static void AddWall(Transform root, int x, int y)
    {
        GameObject wall = new GameObject("Preview Wall");
        wall.transform.SetParent(root, false);
        wall.transform.position = new Vector3(x, y, 0f);
        wall.transform.localScale = Vector3.one * 1.08f;
        SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
        renderer.sprite = PreviewSpriteCache.Square;
        renderer.color = new Color(0.66f, 0.72f, 0.74f, 0.88f);
        renderer.sortingOrder = -40;
    }

    private static void AddObstacle(Transform root, int x, int y, float scale)
    {
        GameObject obstacle = new GameObject("Preview Scrap Obstacle");
        obstacle.transform.SetParent(root, false);
        obstacle.transform.position = new Vector3(x, y, -0.05f);
        obstacle.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = obstacle.AddComponent<SpriteRenderer>();
        renderer.sprite = PreviewSpriteCache.Diamond;
        renderer.color = new Color(0.55f, 0.62f, 0.62f, 0.9f);
        renderer.sortingOrder = -30;
    }

    private static void AddFloorFeature(Transform root, int x, int y, float width, float height, Color color)
    {
        GameObject feature = new GameObject("Preview Coolant/Oil");
        feature.transform.SetParent(root, false);
        feature.transform.position = new Vector3(x, y, 0.82f);
        feature.transform.localScale = new Vector3(width, height, 1f);
        SpriteRenderer renderer = feature.AddComponent<SpriteRenderer>();
        renderer.sprite = PreviewSpriteCache.Circle;
        renderer.color = color;
        renderer.sortingOrder = -35;
    }

    private static void AddMarker(Transform root, string name, Vector3 position, Color color, float scale)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(root, false);
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = PreviewSpriteCache.Circle;
        renderer.color = color;
        renderer.sortingOrder = -20;
    }

    private static void AddLabel(Transform root, string text, Vector3 position, Color color)
    {
        GameObject label = new GameObject(text);
        label.transform.SetParent(root, false);
        label.transform.position = position;

        TextMesh mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.characterSize = 0.32f;
        mesh.fontSize = 28;
        mesh.color = color;

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        renderer.sortingOrder = -10;
    }

    private static class PreviewSpriteCache
    {
        private static Sprite square;
        private static Sprite circle;
        private static Sprite diamond;

        public static Sprite Square => square ??= MakeSprite(Shape.Square);
        public static Sprite Circle => circle ??= MakeSprite(Shape.Circle);
        public static Sprite Diamond => diamond ??= MakeSprite(Shape.Diamond);

        private static Sprite MakeSprite(Shape shape)
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 offset = new Vector2(x, y) - center;
                    bool filled = shape switch
                    {
                        Shape.Circle => offset.sqrMagnitude <= 14f * 14f,
                        Shape.Diamond => Mathf.Abs(offset.x) + Mathf.Abs(offset.y) <= 18f,
                        _ => Mathf.Abs(offset.x) <= 14f && Mathf.Abs(offset.y) <= 14f
                    };

                    texture.SetPixel(x, y, filled ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private enum Shape
        {
            Square,
            Circle,
            Diamond
        }
    }
}
