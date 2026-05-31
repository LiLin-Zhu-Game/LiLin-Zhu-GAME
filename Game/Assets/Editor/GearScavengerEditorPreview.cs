using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GearScavengerEditorPreview
{
    private const string PreviewRootName = "Gear Scavenger Editor Preview";

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

        GameObject root = new GameObject(PreviewRootName);
        root.hideFlags = HideFlags.DontSaveInBuild;

        AddRoom(root.transform, -12, 0, 10, 8);
        AddRoom(root.transform, 0, 0, 10, 8);
        AddRoom(root.transform, 12, 0, 10, 8);
        AddRoom(root.transform, -6, 0, 4, 3);
        AddRoom(root.transform, 6, 0, 4, 3);

        AddLabel(root.transform, "Left Combat Room", new Vector3(-12f, 4.85f, 0f), new Color(1f, 0.45f, 0.35f, 1f));
        AddLabel(root.transform, "Start Room", new Vector3(0f, 4.85f, 0f), new Color(0.45f, 1f, 0.9f, 1f));
        AddLabel(root.transform, "Right Combat Room", new Vector3(12f, 4.85f, 0f), new Color(1f, 0.45f, 0.35f, 1f));

        AddPreviewMarker(root.transform, "Starter Weapons", new Vector3(0f, -2.25f, 0f), new Color(0.4f, 0.9f, 1f, 0.9f), 0.55f);
        AddPreviewMarker(root.transform, "Starter Enemies", new Vector3(0f, 2.4f, 0f), new Color(1f, 0.16f, 0.08f, 0.95f), 0.62f);
        AddPreviewMarker(root.transform, "Boss Wave Area", new Vector3(12f, 0f, 0f), new Color(1f, 0.08f, 0.65f, 0.9f), 0.75f);

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

    private static void AddRoom(Transform root, int centerX, int centerY, int width, int height)
    {
        GameObject room = new GameObject($"Preview Room ({centerX},{centerY})");
        room.transform.SetParent(root, false);
        room.transform.position = new Vector3(centerX, centerY, 0.25f);
        room.transform.localScale = new Vector3(width + 1f, height + 1f, 1f);

        SpriteRenderer floor = room.AddComponent<SpriteRenderer>();
        floor.sprite = PreviewSpriteCache.Square;
        floor.color = new Color(0.18f, 0.22f, 0.25f, 0.42f);
        floor.sortingOrder = -50;

        AddBorder(root, centerX, centerY + height / 2f + 0.55f, width + 1.6f, 0.18f);
        AddBorder(root, centerX, centerY - height / 2f - 0.55f, width + 1.6f, 0.18f);
        AddBorder(root, centerX - width / 2f - 0.55f, centerY, 0.18f, height + 1.6f);
        AddBorder(root, centerX + width / 2f + 0.55f, centerY, 0.18f, height + 1.6f);
    }

    private static void AddBorder(Transform root, float x, float y, float width, float height)
    {
        GameObject border = new GameObject("Preview Wall Outline");
        border.transform.SetParent(root, false);
        border.transform.position = new Vector3(x, y, 0.2f);
        border.transform.localScale = new Vector3(width, height, 1f);

        SpriteRenderer renderer = border.AddComponent<SpriteRenderer>();
        renderer.sprite = PreviewSpriteCache.Square;
        renderer.color = new Color(0.62f, 0.68f, 0.72f, 0.72f);
        renderer.sortingOrder = -45;
    }

    private static void AddPreviewMarker(Transform root, string name, Vector3 position, Color color, float scale)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(root, false);
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = PreviewSpriteCache.Circle;
        renderer.color = color;
        renderer.sortingOrder = -35;
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
        renderer.sortingOrder = -30;
    }

    private static class PreviewSpriteCache
    {
        private static Sprite square;
        private static Sprite circle;

        public static Sprite Square => square ??= MakeSprite(false);
        public static Sprite Circle => circle ??= MakeSprite(true);

        private static Sprite MakeSprite(bool round)
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pixel = new Vector2(x, y);
                    bool filled = !round || Vector2.Distance(pixel, center) <= 14f;
                    texture.SetPixel(x, y, filled ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
