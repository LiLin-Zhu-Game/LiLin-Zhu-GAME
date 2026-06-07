using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GearScavengerBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartGame()
    {
        if (Object.FindObjectOfType<GameDirector>() != null)
        {
            return;
        }

        GameObject runtime = new GameObject("Gear Scavenger Runtime");
        runtime.AddComponent<GameDirector>();
    }
}

public class GameDirector : MonoBehaviour
{
    private enum GameMode
    {
        Story,
        Challenge,
        Training
    }

    private struct RoomDefinition
    {
        public Vector2 Center;
        public int Width;
        public int Height;
        public bool CombatRoom;
        public int Difficulty;
        public string Name;
    }

    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private readonly List<Vector2Int> floorCells = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> walkable = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();
    private readonly List<RoomDefinition> rooms = new List<RoomDefinition>();

    private ObjectPool playerBullets;
    private ObjectPool enemyBullets;
    private ObjectPool enemies;
    private ObjectPool scraps;
    private ObjectPool weaponPickups;
    private ObjectPool hitBursts;
    private PlayerController player;
    private GameHud hud;
    private Camera mainCamera;
    private Sprite[] floorSprites;
    private Sprite[] propSprites;
    private WeaponStats[] weaponTable;
    private GameObject mainMenuCanvas;
    private GameObject tutorialPanel;
    private Text modeDescriptionText;
    private GameMode selectedMode = GameMode.Story;
    private int wave;
    private int salvageCores;
    private int defeatedMachines;
    private int roomsCleared;
    private bool gameOver;
    private bool gameStarted;
    private float statusTimer;

    public Sprite FloorSprite { get; private set; }
    public Sprite WallSprite { get; private set; }
    public Sprite PlayerSprite { get; private set; }
    public Sprite ChaserSprite { get; private set; }
    public Sprite DroneSprite { get; private set; }
    public Sprite SupportSprite { get; private set; }
    public Sprite BossSprite { get; private set; }
    public Sprite BulletSprite { get; private set; }
    public Sprite EnemyBulletSprite { get; private set; }
    public Sprite ScrapSprite { get; private set; }
    public Sprite DefaultWeaponSprite { get; private set; }
    public Sprite FloorDetailSprite { get; private set; }
    public Sprite SupplyStationSprite { get; private set; }
    public Sprite CrateSprite { get; private set; }
    public Sprite BarrelSprite { get; private set; }
    public Sprite TerminalSprite { get; private set; }
    public int SalvageCores => salvageCores;
    public int DefeatedMachines => defeatedMachines;
    public int RoomsCleared => roomsCleared;

    private void Start()
    {
        Time.timeScale = 1f;
        Random.InitState(System.DateTime.Now.Millisecond);
        BuildSprites();
        ConfigureCamera();
        RemoveEditorPreviewMap();
        CreateMainMenu();
    }

    private void StartGameSession()
    {
        if (gameStarted)
        {
            return;
        }

        gameStarted = true;
        if (mainMenuCanvas != null)
        {
            Destroy(mainMenuCanvas);
            mainMenuCanvas = null;
        }

        wave = 0;
        salvageCores = 0;
        defeatedMachines = 0;
        roomsCleared = 0;
        gameOver = false;
        statusTimer = 0f;
        BuildLevel();
        BuildPools();
        SpawnPlayer();
        SpawnStarterWeaponDrops();
        if (selectedMode == GameMode.Training)
        {
            SpawnRoomWeaponDrops(2);
        }

        SpawnGuaranteedRoomEnemies();
        if (selectedMode == GameMode.Challenge)
        {
            SpawnExtraWavePressure(3);
        }

        ValidateStartupSpawns();
        CreateHudSafely();
        ShowStatus(GetModeStartMessage(), 4f);
        StartCoroutine(WaveRoutine());
    }

    private void Update()
    {
        if (!gameStarted)
        {
            return;
        }

        hud?.Refresh(wave, AlertEnemyCount(), activeEnemies.Count);

        if (statusTimer > 0f)
        {
            statusTimer -= Time.deltaTime;
            if (statusTimer <= 0f && !gameOver)
            {
                hud?.SetMessage("Clear rooms -> collect salvage cores -> adapt weapons -> defeat the breaker boss");
            }
        }

        if (gameOver && Input.GetKeyDown(KeyCode.Return))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void OnGUI()
    {
        if (!gameStarted)
        {
            return;
        }

        Color previous = GUI.color;
        Rect panel = new Rect(12f, 12f, 520f, 132f);
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(24f, 20f, 480f, 24f), "HUD / Debug: top-left game state");
        GUI.Label(new Rect(24f, 44f, 480f, 24f), $"Enemies awake: {AlertEnemyCount()}/{activeEnemies.Count}    Weapon pickups: {ActiveWeaponPickupCount()}");
        GUI.Label(new Rect(24f, 68f, 480f, 24f), $"Salvage cores: {salvageCores}/3    Machines defeated: {defeatedMachines}");
        GUI.Label(new Rect(24f, 92f, 480f, 24f), "Goal: clear rooms, upgrade weapons, defeat the breaker boss");
        GUI.Label(new Rect(24f, 116f, 480f, 24f), "E equip    Mouse fire    Q Scrap Nova    F Magnetic Guard    R purge");
        GUI.color = previous;
    }

    private void CreateMainMenu()
    {
        EnsureEventSystem();

        mainMenuCanvas = new GameObject("Gear Scavenger Main Menu");
        Canvas canvas = mainMenuCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = mainMenuCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        mainMenuCanvas.AddComponent<GraphicRaycaster>();

        Image background = CreateMenuImage(mainMenuCanvas.transform, "Wasteland Background", Color.clear);
        Stretch(background.rectTransform);
        background.color = new Color(0.035f, 0.045f, 0.05f, 1f);

        Image floorBand = CreateMenuImage(mainMenuCanvas.transform, "Workshop Floor Band", new Color(0.09f, 0.12f, 0.11f, 1f));
        floorBand.rectTransform.anchorMin = new Vector2(0f, 0f);
        floorBand.rectTransform.anchorMax = new Vector2(1f, 0.36f);
        floorBand.rectTransform.offsetMin = Vector2.zero;
        floorBand.rectTransform.offsetMax = Vector2.zero;

        CreateMenuDecoration(mainMenuCanvas.transform, new Vector2(-360f, -246f), new Vector2(430f, 16f), new Color(0.34f, 0.21f, 0.12f, 0.85f), -8f);
        CreateMenuDecoration(mainMenuCanvas.transform, new Vector2(340f, -242f), new Vector2(430f, 16f), new Color(0.24f, 0.36f, 0.35f, 0.85f), 8f);
        CreateMenuDecoration(mainMenuCanvas.transform, new Vector2(0f, -286f), new Vector2(720f, 10f), new Color(0.55f, 0.18f, 0.1f, 0.55f), 0f);

        Text title = CreateMenuText(mainMenuCanvas.transform, "Title", "GEAR SCAVENGER", 56, TextAnchor.MiddleCenter, new Color(0.68f, 1f, 0.94f));
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        title.rectTransform.sizeDelta = new Vector2(900f, 78f);

        Text subtitle = CreateMenuText(mainMenuCanvas.transform, "Subtitle", "Rebuild a scavenger robot, raid ruined machine rooms, and survive the breaker core.", 22, TextAnchor.MiddleCenter, new Color(0.86f, 0.92f, 0.9f));
        subtitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, -116f);
        subtitle.rectTransform.sizeDelta = new Vector2(980f, 42f);

        Image modePanel = CreateMenuImage(mainMenuCanvas.transform, "Mode Panel", new Color(0.018f, 0.03f, 0.034f, 0.94f));
        modePanel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        modePanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        modePanel.rectTransform.anchoredPosition = new Vector2(-250f, -48f);
        modePanel.rectTransform.sizeDelta = new Vector2(430f, 370f);

        Text modeTitle = CreateMenuText(modePanel.transform, "Mode Title", "SELECT MODE", 30, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.52f));
        modeTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        modeTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        modeTitle.rectTransform.anchoredPosition = new Vector2(0f, -38f);
        modeTitle.rectTransform.sizeDelta = new Vector2(-40f, 42f);

        CreateModeButton(modePanel.transform, "Story Mode", "Balanced mission", new Vector2(215f, -102f), GameMode.Story);
        CreateModeButton(modePanel.transform, "Challenge Mode", "More enemies, faster pressure", new Vector2(215f, -172f), GameMode.Challenge);
        CreateModeButton(modePanel.transform, "Training Mode", "Extra weapon drops for practice", new Vector2(215f, -242f), GameMode.Training);

        modeDescriptionText = CreateMenuText(modePanel.transform, "Mode Description", "", 20, TextAnchor.UpperLeft, new Color(0.86f, 0.94f, 0.9f));
        modeDescriptionText.rectTransform.anchorMin = new Vector2(0f, 0f);
        modeDescriptionText.rectTransform.anchorMax = new Vector2(1f, 0f);
        modeDescriptionText.rectTransform.anchoredPosition = new Vector2(0f, 46f);
        modeDescriptionText.rectTransform.sizeDelta = new Vector2(-54f, 64f);
        modeDescriptionText.rectTransform.offsetMin = new Vector2(28f, modeDescriptionText.rectTransform.offsetMin.y);
        modeDescriptionText.rectTransform.offsetMax = new Vector2(-28f, modeDescriptionText.rectTransform.offsetMax.y);
        UpdateModeDescription();

        Image actionPanel = CreateMenuImage(mainMenuCanvas.transform, "Action Panel", new Color(0.018f, 0.03f, 0.034f, 0.94f));
        actionPanel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        actionPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        actionPanel.rectTransform.anchoredPosition = new Vector2(250f, -48f);
        actionPanel.rectTransform.sizeDelta = new Vector2(430f, 370f);

        Text brief = CreateMenuText(actionPanel.transform, "Briefing", "MISSION BRIEFING\n\nCollect salvage cores, swap weapons from room drops, use scrap to trigger robot abilities, then break through the final vault.", 23, TextAnchor.UpperLeft, new Color(0.92f, 0.96f, 0.94f));
        brief.rectTransform.anchorMin = new Vector2(0f, 1f);
        brief.rectTransform.anchorMax = new Vector2(1f, 1f);
        brief.rectTransform.anchoredPosition = new Vector2(0f, -118f);
        brief.rectTransform.sizeDelta = new Vector2(-56f, 190f);

        CreateMenuButton(actionPanel.transform, "START GAME", new Vector2(215f, -244f), new Vector2(350f, 62f), new Color(0.1f, 0.62f, 0.56f, 0.96f), StartGameSession);
        CreateMenuButton(actionPanel.transform, "TUTORIAL", new Vector2(144f, -318f), new Vector2(190f, 52f), new Color(0.22f, 0.28f, 0.33f, 0.96f), ShowTutorial);
        CreateMenuButton(actionPanel.transform, "QUIT", new Vector2(320f, -318f), new Vector2(120f, 52f), new Color(0.34f, 0.16f, 0.14f, 0.96f), QuitGame);

        CreateTutorialPanel();
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private void CreateModeButton(Transform parent, string title, string subtitle, Vector2 position, GameMode mode)
    {
        Button button = CreateMenuButton(parent, title, position, new Vector2(354f, 52f), new Color(0.1f, 0.18f, 0.2f, 0.96f), () =>
        {
            selectedMode = mode;
            UpdateModeDescription();
        });

        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = $"{title}\n{subtitle}";
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(20f, 0f);
            label.rectTransform.offsetMax = new Vector2(-14f, 0f);
        }
    }

    private Button CreateMenuButton(Transform parent, string label, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject(label);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        Button button = obj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = new Color(Mathf.Min(color.r + 0.14f, 1f), Mathf.Min(color.g + 0.14f, 1f), Mathf.Min(color.b + 0.14f, 1f), color.a);
        colors.pressedColor = new Color(color.r * 0.72f, color.g * 0.72f, color.b * 0.72f, color.a);
        button.colors = colors;
        button.onClick.AddListener(action);

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = CreateMenuText(obj.transform, "Label", label, 22, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    private void CreateTutorialPanel()
    {
        tutorialPanel = new GameObject("Tutorial Panel");
        tutorialPanel.transform.SetParent(mainMenuCanvas.transform, false);

        Image shade = tutorialPanel.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.72f);
        Stretch(shade.rectTransform);

        Image panel = CreateMenuImage(tutorialPanel.transform, "Tutorial Content", new Color(0.025f, 0.035f, 0.04f, 0.98f));
        panel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        panel.rectTransform.sizeDelta = new Vector2(700f, 500f);

        Text title = CreateMenuText(panel.transform, "Tutorial Title", "HOW TO PLAY", 34, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.52f));
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -48f);
        title.rectTransform.sizeDelta = new Vector2(-70f, 50f);

        string tutorial = "WASD: move the robot\nMouse: aim and fire\nSpace: dash through danger\nE: equip nearby weapon drops\nQ: spend 10 scrap to release Scrap Nova\nF: spend 6 scrap for Magnetic Guard\nR: purge weapon heat\n\nClear combat rooms, collect salvage cores, destroy crates for scrap, and defeat the breaker boss.";
        Text body = CreateMenuText(panel.transform, "Tutorial Body", tutorial, 23, TextAnchor.UpperLeft, new Color(0.9f, 0.96f, 0.95f));
        body.rectTransform.anchorMin = new Vector2(0f, 0f);
        body.rectTransform.anchorMax = new Vector2(1f, 1f);
        body.rectTransform.offsetMin = new Vector2(54f, 82f);
        body.rectTransform.offsetMax = new Vector2(-54f, -92f);

        CreateMenuButton(panel.transform, "BACK", new Vector2(350f, -452f), new Vector2(220f, 50f), new Color(0.18f, 0.3f, 0.32f, 0.96f), HideTutorial);
        tutorialPanel.SetActive(false);
    }

    private void ShowTutorial()
    {
        tutorialPanel?.SetActive(true);
    }

    private void HideTutorial()
    {
        tutorialPanel?.SetActive(false);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Quit button pressed. Application.Quit only exits a built player.");
#else
        Application.Quit();
#endif
    }

    private void UpdateModeDescription()
    {
        if (modeDescriptionText == null)
        {
            return;
        }

        switch (selectedMode)
        {
            case GameMode.Challenge:
                modeDescriptionText.text = "Challenge Mode: extra pressure spawns at the start. Best for showing combat and weapon variety quickly.";
                break;
            case GameMode.Training:
                modeDescriptionText.text = "Training Mode: extra weapons spawn near the start so you can test guns and abilities before harder fights.";
                break;
            default:
                modeDescriptionText.text = "Story Mode: balanced room clearing, salvage rewards, and final boss progression.";
                break;
        }
    }

    private string GetModeStartMessage()
    {
        switch (selectedMode)
        {
            case GameMode.Challenge:
                return "Challenge Mode: survive heavier machine pressure and secure the breaker core.";
            case GameMode.Training:
                return "Training Mode: test weapons, learn scrap skills, then clear the ruin.";
            default:
                return "Story Mode: clear rooms, collect 3 salvage cores, then survive the boss.";
        }
    }

    private static Image CreateMenuImage(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateMenuText(Transform parent, string name, string content, int size, TextAnchor alignment, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = alignment;
        text.text = content;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void CreateMenuDecoration(Transform parent, Vector2 position, Vector2 size, Color color, float rotation)
    {
        Image image = CreateMenuImage(parent, "Menu Scrap Accent", color);
        image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.rectTransform.rotation = Quaternion.Euler(0f, 0f, rotation);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public Bullet GetPlayerBullet()
    {
        return playerBullets.Get().GetComponent<Bullet>();
    }

    public Bullet GetEnemyBullet()
    {
        return enemyBullets.Get().GetComponent<Bullet>();
    }

    public void ReleaseBullet(Bullet bullet)
    {
        bullet.gameObject.SetActive(false);
    }

    public void ReleaseEnemy(EnemyController enemy)
    {
        activeEnemies.Remove(enemy);
        enemy.gameObject.SetActive(false);
    }

    public void SpawnScrap(Vector2 position, int value)
    {
        ScrapPickup pickup = scraps.Get().GetComponent<ScrapPickup>();
        pickup.Configure(position + Random.insideUnitCircle * 0.35f, value);
    }

    public void TrySpawnWeaponDrop(Vector2 position, float chance)
    {
        if (weaponTable == null || weaponTable.Length == 0 || Random.value > chance)
        {
            return;
        }

        WeaponPickup pickup = weaponPickups.Get().GetComponent<WeaponPickup>();
        WeaponStats stats = weaponTable[Random.Range(0, weaponTable.Length)];
        pickup.Configure(ToNearestFloorPoint(position + Random.insideUnitCircle * 0.75f), stats);
    }

    public void RewardEnemyDefeat(EnemyKind kind, Vector2 position)
    {
        defeatedMachines++;
        int bonusScrap = kind == EnemyKind.Boss ? 10 : kind == EnemyKind.Support ? 4 : 2;
        for (int i = 0; i < bonusScrap; i++)
        {
            SpawnScrap(position, 1);
        }

        if (defeatedMachines % 6 == 0 && weaponTable != null && weaponTable.Length > 0)
        {
            WeaponPickup pickup = weaponPickups.Get().GetComponent<WeaponPickup>();
            WeaponStats reward = weaponTable[Mathf.Clamp(1 + roomsCleared, 0, weaponTable.Length - 1)];
            pickup.Configure(ToNearestFloorPoint(position + Random.insideUnitCircle * 1.2f), reward);
            ShowStatus($"Scavenged upgrade: {reward.Name}", 2.2f);
        }
    }

    public void SpawnRoomWeaponDrops(int count)
    {
        if (weaponTable == null || weaponTable.Length == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            WeaponPickup pickup = weaponPickups.Get().GetComponent<WeaponPickup>();
            pickup.Configure(PickNearbyFloorPoint(2.2f + i * 0.75f, 3.8f), weaponTable[Random.Range(0, weaponTable.Length)]);
        }
    }

    public void SpawnStarterWeaponDrops()
    {
        SpawnWeaponAtPlayerOffset(new Vector2(-2.35f, -1.25f), 1);
        SpawnWeaponAtPlayerOffset(new Vector2(2.35f, -1.25f), 2);
        SpawnWeaponAtPlayerOffset(new Vector2(0f, 2.25f), 3);
    }

    public void SpawnVisibleStarterEnemies()
    {
        RoomDefinition room = GetFirstCombatRoom();
        SpawnRoomEnemySet(room, false);
    }

    public void SpawnGuaranteedRoomEnemies()
    {
        if (rooms.Count == 0)
        {
            // Hard fallback: if room data is unavailable, force visible spawns near the player.
            SpawnEnemy(EnemyKind.Chaser, PickSpawnPoint(3.2f, 4.8f));
            SpawnEnemy(EnemyKind.Chaser, PickSpawnPoint(3.2f, 4.8f));
            SpawnEnemy(EnemyKind.Drone, PickSpawnPoint(3.8f, 5.8f));
            return;
        }

        int combatRoomIndex = 0;
        foreach (RoomDefinition room in rooms)
        {
            if (!room.CombatRoom)
            {
                continue;
            }

            combatRoomIndex++;
            SpawnRoomEnemySet(room, combatRoomIndex >= 2);
        }
    }

    private void SpawnRoomEnemySet(RoomDefinition room, bool includeSupport)
    {
        float rx = Mathf.Max(1.6f, room.Width * 0.28f);
        float ry = Mathf.Max(1.4f, room.Height * 0.24f);

        SpawnEnemy(EnemyKind.Chaser, PickRoomFloorPoint(room, new Vector2(-rx, -ry)));
        SpawnEnemy(EnemyKind.Chaser, PickRoomFloorPoint(room, new Vector2(rx, -ry)));
        SpawnEnemy(EnemyKind.Drone, PickRoomFloorPoint(room, new Vector2(0f, ry)));

        if (includeSupport || room.Difficulty >= 2)
        {
            SpawnEnemy(EnemyKind.Support, PickRoomFloorPoint(room, new Vector2(0f, -ry * 0.15f)));
        }

        if (room.Difficulty >= 3)
        {
            SpawnEnemy(EnemyKind.Drone, PickRoomFloorPoint(room, new Vector2(-rx * 0.35f, ry * 0.35f)));
            SpawnEnemy(EnemyKind.Chaser, PickRoomFloorPoint(room, new Vector2(rx * 0.35f, ry * 0.15f)));
        }
    }

    private void SpawnWeaponAtPlayerOffset(Vector2 offset, int weaponIndex)
    {
        WeaponPickup pickup = weaponPickups.Get().GetComponent<WeaponPickup>();
        Vector2 position = ToNearestFloorPoint((Vector2)player.transform.position + offset);
        pickup.Configure(position, weaponTable[Mathf.Clamp(weaponIndex, 0, weaponTable.Length - 1)]);
    }

    public void ShowStatus(string message, float seconds = 1.8f)
    {
        hud?.SetMessage(message);
        statusTimer = seconds;
    }

    private void CreateHudSafely()
    {
        try
        {
            hud = GameHud.Create(player, this);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"Gear Scavenger HUD failed to create, but gameplay spawning will continue. {exception}");
        }
    }

    private void ValidateStartupSpawns()
    {
        if (ActiveWeaponPickupCount() == 0)
        {
            Debug.LogWarning("Gear Scavenger startup found zero weapon pickups. Rebuilding starter weapon drops.");
            SpawnStarterWeaponDrops();
        }

        if (activeEnemies.Count == 0)
        {
            Debug.LogWarning("Gear Scavenger startup found zero enemies. Rebuilding guaranteed enemy set.");
            SpawnGuaranteedRoomEnemies();
            if (activeEnemies.Count == 0 && rooms.Count > 0)
            {
                SpawnVisibleStarterEnemies();
            }
        }
    }

    public int ActiveWeaponPickupCount()
    {
        int count = 0;
        WeaponPickup[] allPickups = FindObjectsOfType<WeaponPickup>();
        foreach (WeaponPickup pickup in allPickups)
        {
            if (pickup.gameObject.activeInHierarchy)
            {
                count++;
            }
        }

        return count;
    }

    public int AlertEnemyCount()
    {
        int count = 0;
        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy && enemy.IsAwake)
            {
                count++;
            }
        }

        return count;
    }

    public void FlashDamage()
    {
        hud?.FlashDamage();
    }

    public void PlayerDied()
    {
        gameOver = true;
        hud?.SetMessage("Armor destroyed. Press Enter to rebuild the scavenger.");
    }

    public void PlayPurgeEffect(Vector2 position, float radius)
    {
        ShowStatus("PURGE BLAST", 1.1f);
        GameObject burst = hitBursts.Get();
        burst.transform.position = position;
        burst.transform.localScale = Vector3.one * radius * 2f;
        StartCoroutine(ReleaseAfter(burst, 0.18f));
    }

    private IEnumerator WaveRoutine()
    {
        wave = 1;
        yield return new WaitForSeconds(0.4f);
        while (activeEnemies.Count > 0 && !gameOver)
        {
            yield return null;
        }

        AwardRoomClearReward("First sweep complete: armor repaired and a salvage core recovered.");

        for (wave = 2; wave <= 5; wave++)
        {
            bool isBossWave = wave == 5;
            ShowStatus(isBossWave ? "Boss wave: the breaker unit guards the final core" : $"Threat level {wave}: rooms reconstruct with tougher machines", 2.2f);
            if (isBossWave)
            {
                SpawnBossWave();
            }
            else
            {
                SpawnGuaranteedRoomEnemies();
                SpawnExtraWavePressure(wave);
            }

            while (activeEnemies.Count > 0 && !gameOver)
            {
                yield return null;
            }

            if (gameOver)
            {
                yield break;
            }

            if (wave < 5)
            {
                AwardRoomClearReward("Room clear: salvage core secured, armor repaired, weapon cache opened.");
                SpawnRoomWeaponDrops(1);
                yield return new WaitForSeconds(2f);
            }
        }

        gameOver = true;
        hud?.SetMessage("Prototype complete. You escaped the mechanical ruin with the salvage core. Press Enter to replay.");
    }

    private void AwardRoomClearReward(string message)
    {
        roomsCleared++;
        salvageCores = Mathf.Min(3, salvageCores + 1);
        player?.RepairArmor(18 + roomsCleared * 4);
        ShowStatus($"{message}  Cores {salvageCores}/3", 2.4f);
    }

    private void SpawnEnemy(EnemyKind kind, Vector2 position)
    {
        EnemyController enemy = enemies.Get().GetComponent<EnemyController>();
        enemy.Configure(this, player, kind, position);
        RoomDefinition homeRoom = GetCombatRoomForPosition(position);
        enemy.SetHomeRoom(homeRoom.Center, homeRoom.Width, homeRoom.Height, homeRoom.Name);
        activeEnemies.Add(enemy);
        Debug.Log($"Gear Scavenger spawned {kind} enemy at {position}. Active enemies: {activeEnemies.Count}");
    }

    private void SpawnExtraWavePressure(int waveNumber)
    {
        int extraChasers = Mathf.Clamp(waveNumber, 1, 4);
        int extraDrones = Mathf.Clamp(waveNumber - 1, 0, 3);
        for (int i = 0; i < extraChasers; i++)
        {
            SpawnEnemy(EnemyKind.Chaser, PickCombatSpawnPoint(waveNumber + i));
        }

        for (int i = 0; i < extraDrones; i++)
        {
            SpawnEnemy(EnemyKind.Drone, PickCombatSpawnPoint(waveNumber + extraChasers + i));
        }
    }

    private void SpawnBossWave()
    {
        RoomDefinition room = GetBossRoom();
        SpawnEnemy(EnemyKind.Boss, PickRoomFloorPoint(room, Vector2.zero));
        SpawnEnemy(EnemyKind.Support, PickRoomFloorPoint(room, new Vector2(-3f, -1.2f)));
        SpawnEnemy(EnemyKind.Support, PickRoomFloorPoint(room, new Vector2(3f, -1.2f)));
        SpawnEnemy(EnemyKind.Drone, PickRoomFloorPoint(room, new Vector2(-2.6f, 1.6f)));
        SpawnEnemy(EnemyKind.Drone, PickRoomFloorPoint(room, new Vector2(2.6f, 1.6f)));
        SpawnEnemy(EnemyKind.Chaser, PickRoomFloorPoint(room, new Vector2(-3.2f, 0f)));
        SpawnEnemy(EnemyKind.Chaser, PickRoomFloorPoint(room, new Vector2(3.2f, 0f)));
    }

    private Vector2 PickSpawnPoint(float minDistance, float maxDistance)
    {
        return PickCombatSpawnPoint(Mathf.RoundToInt(minDistance + maxDistance));
    }

    private Vector2 PickNearbyFloorPoint(float minDistance, float maxDistance)
    {
        Vector2 playerPosition = player != null ? (Vector2)player.transform.position : Vector2.zero;
        for (int i = 0; i < 64; i++)
        {
            float angle = (360f / 8f) * (i % 8) + Random.Range(-16f, 16f);
            float distance = Random.Range(minDistance, maxDistance);
            Vector2 candidate = playerPosition + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * distance;
            Vector2 position = ToNearestFloorPoint(candidate);
            float actualDistance = Vector2.Distance(position, playerPosition);
            if (actualDistance >= minDistance * 0.75f && actualDistance <= maxDistance + 0.75f)
            {
                return position;
            }
        }

        return ToNearestFloorPoint(playerPosition + Vector2.right * minDistance);
    }

    private Vector2 ToNearestFloorPoint(Vector2 candidate)
    {
        Vector2Int rounded = Vector2Int.RoundToInt(candidate);
        if (IsFreeFloorCell(rounded))
        {
            return new Vector2(rounded.x, rounded.y);
        }

        Vector2 best = Vector2.zero;
        float bestDistance = float.MaxValue;
        foreach (Vector2Int cell in floorCells)
        {
            if (!IsFreeFloorCell(cell))
            {
                continue;
            }

            float distance = Vector2.SqrMagnitude(candidate - new Vector2(cell.x, cell.y));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = new Vector2(cell.x, cell.y);
            }
        }

        return best;
    }

    private RoomDefinition GetFirstCombatRoom()
    {
        foreach (RoomDefinition room in rooms)
        {
            if (room.CombatRoom)
            {
                return room;
            }
        }

        return rooms.Count > 0 ? rooms[0] : new RoomDefinition
        {
            Center = Vector2.zero,
            Width = 10,
            Height = 8,
            CombatRoom = false,
            Difficulty = 0,
            Name = "Fallback Room"
        };
    }

    private RoomDefinition GetBossRoom()
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].Difficulty >= 3)
            {
                return rooms[i];
            }
        }

        return GetFirstCombatRoom();
    }

    private RoomDefinition GetCombatRoomForPosition(Vector2 position)
    {
        Vector2Int rounded = Vector2Int.RoundToInt(position);
        RoomDefinition nearest = GetFirstCombatRoom();
        float nearestDistance = float.MaxValue;
        foreach (RoomDefinition room in rooms)
        {
            if (!room.CombatRoom)
            {
                continue;
            }

            if (IsInsideRoom(room, rounded))
            {
                return room;
            }

            float distance = Vector2.SqrMagnitude(position - room.Center);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = room;
            }
        }

        return nearest;
    }

    private Vector2 PickCombatSpawnPoint(int seedOffset)
    {
        RoomDefinition room = GetFirstCombatRoom();
        int combatRoomCount = 0;
        foreach (RoomDefinition candidate in rooms)
        {
            if (candidate.CombatRoom)
            {
                combatRoomCount++;
            }
        }

        if (combatRoomCount > 0)
        {
            int targetIndex = Mathf.Abs(seedOffset + wave + roomsCleared) % combatRoomCount;
            int currentIndex = 0;
            foreach (RoomDefinition candidate in rooms)
            {
                if (!candidate.CombatRoom)
                {
                    continue;
                }

                if (currentIndex == targetIndex)
                {
                    room = candidate;
                    break;
                }

                currentIndex++;
            }
        }

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float rx = Random.Range(-room.Width * 0.28f, room.Width * 0.28f);
        float ry = Random.Range(-room.Height * 0.24f, room.Height * 0.24f);
        Vector2 offset = new Vector2(rx + Mathf.Cos(angle) * 0.7f, ry + Mathf.Sin(angle) * 0.7f);
        return PickRoomFloorPoint(room, offset);
    }

    private Vector2 PickRoomFloorPoint(RoomDefinition room, Vector2 offset)
    {
        Vector2 candidate = room.Center + offset;
        Vector2Int rounded = Vector2Int.RoundToInt(candidate);
        if (IsFreeFloorCell(rounded) && IsInsideRoom(room, rounded))
        {
            return new Vector2(rounded.x, rounded.y);
        }

        int searchRadius = Mathf.Max(room.Width, room.Height);
        for (int radius = 1; radius <= searchRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    Vector2Int cell = rounded + new Vector2Int(dx, dy);
                    if (IsInsideRoom(room, cell) && IsFreeFloorCell(cell))
                    {
                        return new Vector2(cell.x, cell.y);
                    }
                }
            }
        }

        return ToNearestFloorPoint(room.Center);
    }

    private bool IsInsideRoom(RoomDefinition room, Vector2Int cell)
    {
        int minX = Mathf.RoundToInt(room.Center.x) - room.Width / 2;
        int maxX = Mathf.RoundToInt(room.Center.x) + room.Width / 2;
        int minY = Mathf.RoundToInt(room.Center.y) - room.Height / 2;
        int maxY = Mathf.RoundToInt(room.Center.y) + room.Height / 2;
        return cell.x >= minX && cell.x <= maxX && cell.y >= minY && cell.y <= maxY;
    }

    private bool IsFreeFloorCell(Vector2Int cell)
    {
        return walkable.Contains(cell) && !blockedCells.Contains(cell);
    }

    private void BuildSprites()
    {
        floorSprites = new[]
        {
            LoadSprite("floor_a", SpriteFactory.Square(new Color(0.18f, 0.2f, 0.23f), new Color(0.1f, 0.12f, 0.15f))),
            LoadSprite("floor_b", SpriteFactory.Square(new Color(0.2f, 0.22f, 0.25f), new Color(0.1f, 0.12f, 0.15f)))
        };
        propSprites = new[]
        {
            LoadSprite("prop_a", SpriteFactory.Square(new Color(0.28f, 0.35f, 0.38f), new Color(0.05f, 0.07f, 0.08f))),
            LoadSprite("prop_b", SpriteFactory.Square(new Color(0.3f, 0.32f, 0.36f), new Color(0.05f, 0.07f, 0.08f)))
        };

        FloorSprite = floorSprites[0];
        WallSprite = LoadSprite("wall", SpriteFactory.Square(new Color(0.38f, 0.42f, 0.47f), new Color(0.11f, 0.13f, 0.16f)));
        PlayerSprite = LoadSprite("player", SpriteFactory.Diamond(new Color(0.26f, 0.9f, 0.86f), new Color(0.04f, 0.18f, 0.2f)));
        ChaserSprite = LoadSprite("enemy_chaser", SpriteFactory.Circle(new Color(0.96f, 0.33f, 0.3f), new Color(0.35f, 0.05f, 0.04f)));
        DroneSprite = LoadSprite("enemy_drone", SpriteFactory.Circle(new Color(1f, 0.68f, 0.2f), new Color(0.32f, 0.16f, 0.02f)));
        SupportSprite = LoadSprite("enemy_support", SpriteFactory.Diamond(new Color(0.55f, 0.9f, 0.34f), new Color(0.09f, 0.24f, 0.05f)));
        BossSprite = LoadSprite("enemy_boss", SpriteFactory.Diamond(new Color(0.9f, 0.2f, 0.95f), new Color(0.2f, 0.03f, 0.22f)));
        BulletSprite = SpriteFactory.Bolt(new Color(0.8f, 1f, 0.96f), new Color(0.1f, 0.45f, 0.95f));
        EnemyBulletSprite = SpriteFactory.Bolt(new Color(1f, 0.35f, 0.18f), new Color(0.45f, 0.04f, 0.02f));
        FloorDetailSprite = SpriteFactory.Square(new Color(0.08f, 0.09f, 0.1f, 0.45f), Color.clear);
        ScrapSprite = LoadSprite("scrap", SpriteFactory.Diamond(new Color(0.55f, 0.85f, 1f), new Color(0.04f, 0.13f, 0.22f)));
        DefaultWeaponSprite = LoadSprite("weapon_rifle", SpriteFactory.Diamond(new Color(0.85f, 0.55f, 1f), new Color(0.12f, 0.04f, 0.18f)));
        SupplyStationSprite = SpriteFactory.Circle(new Color(0.25f, 0.95f, 1f, 0.82f), new Color(0.02f, 0.22f, 0.28f, 0.95f));
        CrateSprite = LoadSprite("crate", SpriteFactory.Square(new Color(0.48f, 0.34f, 0.18f), new Color(0.16f, 0.1f, 0.04f)));
        BarrelSprite = LoadSprite("barrel", SpriteFactory.Circle(new Color(0.55f, 0.17f, 0.12f), new Color(0.12f, 0.06f, 0.05f)));
        TerminalSprite = LoadSprite("terminal", SpriteFactory.Diamond(new Color(0.12f, 0.88f, 0.62f), new Color(0.02f, 0.16f, 0.13f)));

        weaponTable = new[]
        {
            new WeaponStats("Rust Rifle", LoadSprite("weapon_rifle", DefaultWeaponSprite), 0.14f, 9.5f, 18, 13f, 3.5f, 1, 1.6f),
            new WeaponStats("Scatter Core", LoadSprite("weapon_scatter", DefaultWeaponSprite), 0.28f, 17f, 12, 11f, 12f, 5, 1.15f),
            new WeaponStats("Beam Needle", LoadSprite("weapon_beam", DefaultWeaponSprite), 0.08f, 6.5f, 9, 17f, 1.2f, 1, 1.25f),
            new WeaponStats("Scrap Cannon", LoadSprite("weapon_cannon", DefaultWeaponSprite), 0.48f, 24f, 42, 9.5f, 4f, 1, 1.9f),
            new WeaponStats("Coil Ripper", LoadSprite("weapon_coil", SpriteFactory.Bolt(new Color(0.4f, 0.95f, 1f), new Color(0.02f, 0.18f, 0.28f))), 0.055f, 4.8f, 7, 18f, 5.5f, 1, 1.05f),
            new WeaponStats("Arc Splitter", LoadSprite("weapon_arc", SpriteFactory.Diamond(new Color(0.95f, 0.75f, 1f), new Color(0.22f, 0.04f, 0.28f))), 0.24f, 15f, 14, 12f, 18f, 3, 1.35f),
            new WeaponStats("Heat Lance", LoadSprite("weapon_lance", SpriteFactory.Bolt(new Color(1f, 0.35f, 0.12f), new Color(0.38f, 0.04f, 0.02f))), 0.68f, 34f, 70, 20f, 0.6f, 1, 1.1f),
            new WeaponStats("Nanite Swarm", LoadSprite("weapon_swarm", SpriteFactory.Circle(new Color(0.35f, 1f, 0.5f), new Color(0.02f, 0.2f, 0.08f))), 0.18f, 11f, 8, 10f, 28f, 6, 1.65f),
            new WeaponStats("Rail Spike", LoadSprite("weapon_rail", SpriteFactory.Bolt(new Color(0.95f, 0.98f, 1f), new Color(0.08f, 0.1f, 0.13f))), 0.38f, 20f, 52, 24f, 0.35f, 1, 1.35f),
            new WeaponStats("Pulse Sprayer", LoadSprite("weapon_pulse", SpriteFactory.Circle(new Color(0.55f, 0.85f, 1f), new Color(0.02f, 0.08f, 0.24f))), 0.1f, 7.2f, 11, 14f, 15f, 2, 1.4f)
        };
    }

    private Sprite LoadSprite(string assetName, Sprite fallback)
    {
        Sprite loaded = Resources.Load<Sprite>($"GearScavenger/{assetName}");
        if (loaded != null)
        {
            return loaded;
        }

        Texture2D texture = Resources.Load<Texture2D>($"GearScavenger/{assetName}");
        if (texture == null)
        {
            return fallback;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height));
    }

    private void ConfigureCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 6.2f;
        mainCamera.backgroundColor = new Color(0.05f, 0.07f, 0.09f);
        mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        mainCamera.gameObject.AddComponent<CameraFollow>();
    }

    private void BuildLevel()
    {
        RemoveSceneObject("Generated Mechanical Rooms");
        GameObject levelRoot = new GameObject("Generated Mechanical Rooms");
        walkable.Clear();
        floorCells.Clear();
        blockedCells.Clear();
        rooms.Clear();

        AddRoom(0, 0, 10, 8, false, 0, "Start Workshop");
        AddRoom(-13, 0, 9, 8, true, 1, "Scrap Yard");
        AddRoom(13, 0, 9, 8, true, 2, "Assembly Maze");
        AddRoom(0, 10, 8, 7, true, 2, "Cache Room");
        AddRoom(0, -10, 10, 7, true, 3, "Breaker Vault");
        AddCorridor(-6, 0, 4, 3);
        AddCorridor(6, 0, 4, 3);
        AddCorridor(0, 5, 3, 4);
        AddCorridor(0, -5, 3, 4);

        foreach (Vector2Int cell in walkable)
        {
            GameObject tile = new GameObject("Floor");
            tile.transform.SetParent(levelRoot.transform);
            tile.transform.position = new Vector3(cell.x, cell.y, 1f);
            tile.transform.localScale = Vector3.one * 1.04f;
            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = floorSprites[Mathf.Abs(cell.x + cell.y) % floorSprites.Length];
            renderer.color = Color.white;
            renderer.sortingOrder = -10;

            if (Random.value < 0.12f)
            {
                GameObject detail = new GameObject("Floor Detail");
                detail.transform.SetParent(levelRoot.transform);
                detail.transform.position = new Vector3(cell.x + Random.Range(-0.25f, 0.25f), cell.y + Random.Range(-0.25f, 0.25f), 0.9f);
                detail.transform.localScale = new Vector3(Random.Range(0.25f, 0.55f), Random.Range(0.04f, 0.12f), 1f);
                detail.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
                SpriteRenderer detailRenderer = detail.AddComponent<SpriteRenderer>();
                detailRenderer.sprite = FloorDetailSprite;
                detailRenderer.sortingOrder = -9;
            }
        }

        HashSet<Vector2Int> wallCells = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in walkable)
        {
            AddWallIfNeeded(wallCells, cell + Vector2Int.up);
            AddWallIfNeeded(wallCells, cell + Vector2Int.down);
            AddWallIfNeeded(wallCells, cell + Vector2Int.left);
            AddWallIfNeeded(wallCells, cell + Vector2Int.right);
        }

        foreach (Vector2Int cell in wallCells)
        {
            GameObject wall = new GameObject("Wall");
            wall.transform.SetParent(levelRoot.transform);
            wall.transform.position = new Vector3(cell.x, cell.y, 0f);
            wall.transform.localScale = Vector3.one * 1.08f;
            SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = WallSprite;
            renderer.sortingOrder = -1;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            wall.AddComponent<WallMarker>();
        }

        PlaceRoomDressing(levelRoot.transform);
        PlaceFloorFeature(levelRoot.transform, -13, 0, 2.2f, 0.95f, new Color(0.18f, 0.5f, 0.52f, 0.5f));
        PlaceFloorFeature(levelRoot.transform, 0, 10, 2.6f, 1.1f, new Color(0.16f, 0.75f, 0.95f, 0.42f));
        PlaceFloorFeature(levelRoot.transform, 13, 0, 2.4f, 0.9f, new Color(0.35f, 0.1f, 0.08f, 0.42f));
        PlaceFloorFeature(levelRoot.transform, 0, -10, 2.7f, 1.15f, new Color(0.42f, 0.12f, 0.5f, 0.38f));
        PlaceSupplyStation(levelRoot.transform, -2, 0, "Repair Station");
        PlaceSupplyStation(levelRoot.transform, 0, 9, "Cooling Station");
    }

    private void AddRoom(int centerX, int centerY, int width, int height, bool combatRoom, int difficulty, string roomName)
    {
        rooms.Add(new RoomDefinition
        {
            Center = new Vector2(centerX, centerY),
            Width = width,
            Height = height,
            CombatRoom = combatRoom,
            Difficulty = difficulty,
            Name = roomName
        });

        AddWalkableRect(centerX, centerY, width, height);
    }

    private void AddCorridor(int centerX, int centerY, int width, int height)
    {
        AddWalkableRect(centerX, centerY, width, height);
    }

    private void AddWalkableRect(int centerX, int centerY, int width, int height)
    {
        int minX = centerX - width / 2;
        int maxX = centerX + width / 2;
        int minY = centerY - height / 2;
        int maxY = centerY + height / 2;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (walkable.Add(cell))
                {
                    floorCells.Add(cell);
                }
            }
        }
    }

    private void AddWallIfNeeded(HashSet<Vector2Int> wallCells, Vector2Int cell)
    {
        if (!walkable.Contains(cell))
        {
            wallCells.Add(cell);
        }
    }

    private void PlaceRoomDressing(Transform root)
    {
        PlaceTerminal(root, 0, 3, "Mission Terminal");
        PlaceCrateCluster(root, -4, 3, 2, 1);
        PlaceCrateCluster(root, 3, 3, 2, 1);
        PlaceCrateCluster(root, -4, -3, 1, 2);
        PlaceCrateCluster(root, 4, -3, 1, 2);
        PlaceBarrel(root, -2, -3, false);
        PlaceBarrel(root, 2, -3, false);

        PlaceDecoration(root, -16, 2, 1.15f);
        PlaceDecoration(root, -11, -2, 0.9f);
        PlaceCrateLine(root, -15, -1, 3, true);
        PlaceCrateLine(root, -12, 2, 3, true);
        PlaceCrateCluster(root, -16, -3, 2, 1);
        PlaceBarrel(root, -10, 3, true);
        PlaceBarrel(root, -13, -3, false);

        PlaceDecoration(root, 10, 2, 1.0f);
        PlaceDecoration(root, 15, -2, 1.2f);
        PlaceCrateLine(root, 11, -2, 3, false);
        PlaceCrateLine(root, 15, 1, 3, false);
        PlaceCrateCluster(root, 13, -3, 2, 1);
        PlaceBarrel(root, 16, 3, true);
        PlaceTerminal(root, 13, 3, "Factory Console");

        PlaceCrateCluster(root, -3, 12, 2, 1);
        PlaceCrateCluster(root, 2, 12, 2, 1);
        PlaceCrateLine(root, -3, 8, 2, false);
        PlaceCrateLine(root, 3, 8, 2, false);
        PlaceDecoration(root, -2, 11, 1.05f);
        PlaceDecoration(root, 2, 9, 0.9f);
        PlaceTerminal(root, 0, 12, "Cache Uplink");

        PlaceDecoration(root, -4, -12, 1.2f);
        PlaceDecoration(root, 4, -12, 1.2f);
        PlaceDecoration(root, -4, -8, 1.05f);
        PlaceDecoration(root, 4, -8, 1.05f);
        PlaceCrateLine(root, -2, -11, 2, true);
        PlaceCrateLine(root, 2, -9, 2, true);
        PlaceBarrel(root, -1, -12, true);
        PlaceBarrel(root, 2, -8, true);
        PlaceTerminal(root, 0, -13, "Boss Core");
    }

    private void PlaceCrateCluster(Transform root, int startX, int startY, int columns, int rows)
    {
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                PlaceCrate(root, startX + x, startY + y);
            }
        }
    }

    private void PlaceCrateLine(Transform root, int startX, int startY, int count, bool horizontal)
    {
        for (int i = 0; i < count; i++)
        {
            PlaceCrate(root, startX + (horizontal ? i : 0), startY + (horizontal ? 0 : i));
        }
    }

    private void PlaceCrate(Transform root, int x, int y)
    {
        GameObject crate = CreateBlockingProp(root, "Breakable Crate", x, y, 0.6f, CrateSprite, -2, new Vector2(0.38f, 0.38f));
        crate.AddComponent<DestructibleProp>().Configure(this, 36, 3, 0.08f, false);
    }

    private void PlaceBarrel(Transform root, int x, int y, bool volatileBarrel)
    {
        GameObject barrel = CreateBlockingProp(root, volatileBarrel ? "Volatile Fuel Barrel" : "Scrap Barrel", x, y, 0.58f, BarrelSprite, -2, new Vector2(0.42f, 0.42f));
        barrel.AddComponent<DestructibleProp>().Configure(this, volatileBarrel ? 28 : 34, volatileBarrel ? 4 : 2, volatileBarrel ? 0.14f : 0.04f, volatileBarrel);
    }

    private void PlaceTerminal(Transform root, int x, int y, string terminalName)
    {
        GameObject terminal = CreateBlockingProp(root, terminalName, x, y, 0.72f, TerminalSprite, -2, new Vector2(0.46f, 0.46f));
        terminal.AddComponent<DestructibleProp>().Configure(this, 58, 5, 0.16f, false);
    }

    private GameObject CreateBlockingProp(Transform root, string name, int x, int y, float scale, Sprite sprite, int sortingOrder, Vector2 colliderSize)
    {
        GameObject prop = new GameObject(name);
        prop.transform.SetParent(root);
        prop.transform.position = new Vector3(x, y, -0.05f);
        prop.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
        collider.size = colliderSize;
        prop.AddComponent<WallMarker>();
        RegisterBlockedCell(x, y);
        return prop;
    }

    private void PlaceDecoration(Transform root, int x, int y, float scale)
    {
        CreateBlockingProp(root, "Scrap Machinery", x, y, scale * 0.62f, propSprites[Random.Range(0, propSprites.Length)], -2, Vector2.one * 0.42f);
    }

    private void RegisterBlockedCell(int x, int y)
    {
        Vector2Int cell = new Vector2Int(x, y);
        if (walkable.Contains(cell))
        {
            blockedCells.Add(cell);
        }
    }

    private void PlaceFloorFeature(Transform root, int x, int y, float width, float height, Color color)
    {
        GameObject feature = new GameObject("Coolant/Oil Floor Feature");
        feature.transform.SetParent(root);
        feature.transform.position = new Vector3(x, y, 0.82f);
        feature.transform.localScale = new Vector3(width, height, 1f);
        SpriteRenderer renderer = feature.AddComponent<SpriteRenderer>();
        renderer.sprite = FloorDetailSprite;
        renderer.color = color;
        renderer.sortingOrder = -8;
    }

    private void PlaceSupplyStation(Transform root, int x, int y, string stationName)
    {
        GameObject station = new GameObject(stationName);
        station.transform.SetParent(root);
        station.transform.position = new Vector3(x, y, -0.08f);
        station.transform.localScale = Vector3.one * 1.35f;
        SpriteRenderer renderer = station.AddComponent<SpriteRenderer>();
        renderer.sprite = SupplyStationSprite;
        renderer.sortingOrder = 2;
        CircleCollider2D collider = station.AddComponent<CircleCollider2D>();
        collider.radius = 0.44f;
        collider.isTrigger = true;
        station.AddComponent<SupplyStation>().Configure(this, stationName.Contains("Cooling"));
    }

    private void RemoveEditorPreviewMap()
    {
        RemoveSceneObject("Gear Scavenger Editor Preview");
    }

    private void RemoveSceneObject(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing == null)
        {
            return;
        }

        Destroy(existing);
    }

    private void BuildPools()
    {
        playerBullets = new ObjectPool("Player Bullets", 40, CreatePlayerBullet);
        enemyBullets = new ObjectPool("Enemy Bullets", 32, CreateEnemyBullet);
        enemies = new ObjectPool("Enemies", 24, CreateEnemy);
        scraps = new ObjectPool("Scraps", 60, CreateScrap);
        weaponPickups = new ObjectPool("Weapon Pickups", 12, CreateWeaponPickup);
        hitBursts = new ObjectPool("Purge Bursts", 8, CreateBurst);
    }

    private GameObject CreatePlayerBullet()
    {
        GameObject bullet = CreateBulletObject("Player Bullet", BulletSprite, 0.18f);
        bullet.GetComponent<Bullet>().SetPoolOwner(this, BulletOwner.Player);
        return bullet;
    }

    private GameObject CreateEnemyBullet()
    {
        GameObject bullet = CreateBulletObject("Enemy Bullet", EnemyBulletSprite, 0.2f);
        bullet.GetComponent<Bullet>().SetPoolOwner(this, BulletOwner.Enemy);
        return bullet;
    }

    private GameObject CreateBulletObject(string name, Sprite sprite, float radius)
    {
        GameObject bullet = new GameObject(name);
        SpriteRenderer renderer = bullet.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 8;
        bullet.transform.localScale = name.Contains("Enemy") ? new Vector3(1.3f, 0.55f, 1f) : new Vector3(1.65f, 0.55f, 1f);
        CircleCollider2D collider = bullet.AddComponent<CircleCollider2D>();
        collider.radius = radius;
        collider.isTrigger = true;
        Rigidbody2D rb = bullet.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        TrailRenderer trail = bullet.AddComponent<TrailRenderer>();
        trail.time = 0.12f;
        trail.startWidth = name.Contains("Enemy") ? 0.18f : 0.22f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = name.Contains("Enemy") ? new Color(1f, 0.35f, 0.18f, 0.85f) : new Color(0.6f, 1f, 1f, 0.9f);
        trail.endColor = Color.clear;
        bullet.AddComponent<Bullet>();
        return bullet;
    }

    private GameObject CreateEnemy()
    {
        GameObject enemy = new GameObject("Enemy");
        CircleCollider2D collider = enemy.AddComponent<CircleCollider2D>();
        collider.radius = 0.42f;
        Rigidbody2D rb = enemy.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        enemy.AddComponent<EnemyController>();
        return enemy;
    }

    private GameObject CreateScrap()
    {
        GameObject pickup = new GameObject("Armor Scrap");
        SpriteRenderer renderer = pickup.AddComponent<SpriteRenderer>();
        renderer.sprite = ScrapSprite;
        renderer.sortingOrder = 4;
        CircleCollider2D collider = pickup.AddComponent<CircleCollider2D>();
        collider.radius = 0.22f;
        collider.isTrigger = true;
        pickup.AddComponent<ScrapPickup>();
        return pickup;
    }

    private GameObject CreateWeaponPickup()
    {
        GameObject pickup = new GameObject("Weapon Pickup");
        SpriteRenderer renderer = pickup.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 6;
        CircleCollider2D collider = pickup.AddComponent<CircleCollider2D>();
        collider.radius = 0.45f;
        collider.isTrigger = true;
        pickup.AddComponent<WeaponPickup>();
        return pickup;
    }

    private GameObject CreateBurst()
    {
        GameObject burst = new GameObject("Purge Blast");
        SpriteRenderer renderer = burst.AddComponent<SpriteRenderer>();
        renderer.sprite = SpriteFactory.Circle(new Color(0.55f, 0.95f, 1f, 0.35f), Color.clear);
        renderer.sortingOrder = 12;
        return burst;
    }

    private void SpawnPlayer()
    {
        GameObject playerObject = FindExistingPlayerObject();
        if (playerObject == null)
        {
            playerObject = new GameObject("Player Scavenger");
        }

        Vector2 spawnPoint = ToNearestFloorPoint(Vector2.zero);
        playerObject.transform.position = new Vector3(spawnPoint.x, spawnPoint.y, 0f);
        playerObject.transform.rotation = Quaternion.identity;
        playerObject.transform.localScale = Vector3.one;

        Transform visualRoot = playerObject.transform.Find("Visual Root");
        if (visualRoot == null)
        {
            GameObject visual = new GameObject("Visual Root");
            visual.transform.SetParent(playerObject.transform, false);
            visualRoot = visual.transform;
        }

        SpriteRenderer rootRenderer = playerObject.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }

        Transform bodyTransform = visualRoot.Find("Player Body");
        if (bodyTransform == null)
        {
            GameObject body = new GameObject("Player Body");
            body.transform.SetParent(visualRoot, false);
            bodyTransform = body.transform;
        }

        SpriteRenderer bodyRenderer = bodyTransform.GetComponent<SpriteRenderer>();
        if (bodyRenderer == null)
        {
            bodyRenderer = bodyTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        bodyRenderer.enabled = true;
        bodyRenderer.sprite = PlayerSprite;
        bodyRenderer.sortingOrder = 5;
        bodyTransform.localPosition = new Vector3(0f, -0.02f, 0f);
        bodyTransform.localRotation = Quaternion.identity;
        bodyTransform.localScale = Vector3.one * 3.85f;

        Rigidbody2D rb = playerObject.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = playerObject.AddComponent<Rigidbody2D>();
        }

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D collider = playerObject.GetComponent<CircleCollider2D>();
        if (collider == null)
        {
            collider = playerObject.AddComponent<CircleCollider2D>();
        }

        collider.radius = 0.34f;

        WeaponController weapon = playerObject.GetComponent<WeaponController>();
        if (weapon == null)
        {
            weapon = playerObject.AddComponent<WeaponController>();
        }

        player = playerObject.GetComponent<PlayerController>();
        if (player == null)
        {
            player = playerObject.AddComponent<PlayerController>();
        }

        weapon.Initialize(this, player);
        weapon.ApplyWeapon(weaponTable[0]);
        player.SetBodyRenderer(bodyRenderer);
        player.Initialize(this, weapon);
        mainCamera.GetComponent<CameraFollow>().Target = playerObject.transform;
    }

    private GameObject FindExistingPlayerObject()
    {
        PlayerController existingController = FindObjectOfType<PlayerController>();
        if (existingController != null)
        {
            return existingController.gameObject;
        }

        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            string lowerName = obj.name.ToLowerInvariant();
            bool looksLikeScenePlayer = obj.scene.IsValid()
                && obj.transform.parent == null
                && lowerName.Contains("player")
                && !lowerName.Contains("bullet")
                && !lowerName.Contains("pool");

            if (looksLikeScenePlayer)
            {
                return obj;
            }
        }

        return null;
    }

    private IEnumerator ReleaseAfter(GameObject target, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        target.SetActive(false);
    }
}

public enum BulletOwner
{
    Player,
    Enemy
}

public class Bullet : MonoBehaviour
{
    private GameDirector director;
    private BulletOwner owner;
    private TrailRenderer trail;
    private Vector2 velocity;
    private float lifeTimer;
    private int damage;

    public void SetPoolOwner(GameDirector ownerDirector, BulletOwner bulletOwner)
    {
        director = ownerDirector;
        owner = bulletOwner;
    }

    public void Fire(Vector2 position, Vector2 direction, float speed, int bulletDamage, float lifetime)
    {
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        if (trail == null)
        {
            trail = GetComponent<TrailRenderer>();
        }

        trail?.Clear();
        velocity = direction.normalized * speed;
        damage = bulletDamage;
        lifeTimer = lifetime;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            director.ReleaseBullet(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == BulletOwner.Player)
        {
            DestructibleProp prop = other.GetComponent<DestructibleProp>();
            if (prop != null)
            {
                prop.TakeDamage(damage, velocity.normalized);
                director.ReleaseBullet(this);
                return;
            }

            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, velocity.normalized * 2.5f);
                director.ReleaseBullet(this);
                return;
            }
        }
        else
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
                director.ReleaseBullet(this);
                return;
            }
        }

        if (other.GetComponent<WallMarker>() != null)
        {
            director.ReleaseBullet(this);
        }
    }
}

public class WeaponController : MonoBehaviour
{
    [SerializeField] private WeaponCoreModule core;
    [SerializeField] private WeaponAmmoModule ammo;
    [SerializeField] private WeaponStockModule stock;

    private GameDirector director;
    private PlayerController player;
    private SpriteRenderer weaponRenderer;
    private SpriteRenderer muzzleRenderer;
    private Transform weaponPivot;
    private Transform muzzlePoint;
    private WeaponStats equippedStats;
    private float nextFireTime;
    private Vector2 lastAimDirection = Vector2.right;

    public string CurrentWeaponName => equippedStats != null ? equippedStats.Name : "Rust Rifle";
    private float FireDelay => equippedStats != null ? equippedStats.FireDelay : core != null ? core.FireDelay : 0.14f;
    private float HeatPerShot => equippedStats != null ? equippedStats.HeatPerShot : core != null ? core.HeatPerShot : 9.5f;
    private int Damage => equippedStats != null ? equippedStats.Damage : ammo != null ? ammo.Damage : 18;
    private float BulletSpeed => equippedStats != null ? equippedStats.BulletSpeed : ammo != null ? ammo.BulletSpeed : 13f;
    private float Spread => equippedStats != null ? equippedStats.SpreadDegrees : stock != null ? stock.SpreadDegrees : 3.5f;
    private int PelletCount => equippedStats != null ? equippedStats.PelletCount : 1;
    private float BulletLifetime => equippedStats != null ? equippedStats.BulletLifetime : 1.6f;

    public void Initialize(GameDirector owner, PlayerController playerController)
    {
        director = owner;
        player = playerController;
        EnsureWeaponRenderer();
    }

    public void ApplyWeapon(WeaponStats stats)
    {
        equippedStats = stats;
        EnsureWeaponRenderer();
        if (weaponRenderer != null && stats != null)
        {
            weaponRenderer.sprite = stats.Sprite;
            weaponRenderer.transform.localPosition = new Vector3(0.38f, 0f, -0.05f);
            weaponRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            weaponRenderer.transform.localScale = new Vector3(4.8f, 4.8f, 1f);
        }
    }

    private void EnsureWeaponRenderer()
    {
        if (weaponRenderer != null)
        {
            return;
        }

        Transform existingPivot = transform.Find("Weapon Pivot");
        GameObject pivotObject = existingPivot != null ? existingPivot.gameObject : new GameObject("Weapon Pivot");
        pivotObject.transform.SetParent(transform, false);
        weaponPivot = pivotObject.transform;

        Transform existing = weaponPivot.Find("Equipped Weapon");
        GameObject weaponObject = existing != null ? existing.gameObject : new GameObject("Equipped Weapon");
        weaponObject.transform.SetParent(weaponPivot, false);
        weaponRenderer = weaponObject.GetComponent<SpriteRenderer>();
        if (weaponRenderer == null)
        {
            weaponRenderer = weaponObject.AddComponent<SpriteRenderer>();
        }

        weaponRenderer.sortingOrder = 7;
        if (director != null)
        {
            weaponRenderer.sprite = director.DefaultWeaponSprite;
        }

        weaponRenderer.transform.localPosition = new Vector3(0.38f, 0f, -0.05f);
        weaponRenderer.transform.localScale = new Vector3(4.8f, 4.8f, 1f);

        Transform existingMuzzle = weaponPivot.Find("Muzzle");
        GameObject muzzleObject = existingMuzzle != null ? existingMuzzle.gameObject : new GameObject("Muzzle");
        muzzleObject.transform.SetParent(weaponPivot, false);
        muzzlePoint = muzzleObject.transform;
        muzzlePoint.localPosition = new Vector3(1.18f, 0f, -0.12f);
        muzzlePoint.localRotation = Quaternion.identity;
        muzzlePoint.localScale = Vector3.one;

        muzzleRenderer = muzzleObject.GetComponent<SpriteRenderer>();
        if (muzzleRenderer == null)
        {
            muzzleRenderer = muzzleObject.AddComponent<SpriteRenderer>();
        }

        muzzleRenderer.sprite = SpriteFactory.Circle(new Color(0.5f, 1f, 0.95f, 0.9f), Color.clear);
        muzzleRenderer.sortingOrder = 10;
        muzzleRenderer.transform.localScale = Vector3.one * 0.18f;
    }

    public void TryFire(Vector2 direction)
    {
        if (Time.time < nextFireTime || player.IsDead)
        {
            return;
        }

        nextFireTime = Time.time + FireDelay;
        player.AddHeat(HeatPerShot);

        Vector2 muzzle = GetMuzzleWorldPosition(direction);
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        for (int i = 0; i < PelletCount; i++)
        {
            float angle = baseAngle + Random.Range(-Spread, Spread);
            Vector2 shotDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Bullet bullet = director.GetPlayerBullet();
            bullet.Fire(muzzle, shotDirection, BulletSpeed, Damage, BulletLifetime);
        }
    }

    private void LateUpdate()
    {
        if (weaponPivot == null || weaponRenderer == null || player == null)
        {
            return;
        }

        Vector2 aim = player.AimDirection.sqrMagnitude > 0.01f ? player.AimDirection : Vector2.right;
        lastAimDirection = aim.normalized;
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        weaponPivot.localPosition = new Vector3(0f, 0.05f, -0.05f);
        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        weaponRenderer.flipY = aim.x < 0f;
        if (muzzleRenderer != null)
        {
            muzzleRenderer.enabled = true;
        }
    }

    private Vector2 GetMuzzleWorldPosition(Vector2 direction)
    {
        Vector2 aim = direction.sqrMagnitude > 0.01f ? direction.normalized : lastAimDirection;
        if (muzzlePoint != null)
        {
            return muzzlePoint.position;
        }

        if (weaponRenderer != null)
        {
            return (Vector2)weaponRenderer.transform.position + aim * 0.8f;
        }

        return (Vector2)transform.position + aim * 1.1f;
    }
}

public class WeaponStats
{
    public readonly string Name;
    public readonly Sprite Sprite;
    public readonly float FireDelay;
    public readonly float HeatPerShot;
    public readonly int Damage;
    public readonly float BulletSpeed;
    public readonly float SpreadDegrees;
    public readonly int PelletCount;
    public readonly float BulletLifetime;

    public WeaponStats(string name, Sprite sprite, float fireDelay, float heatPerShot, int damage, float bulletSpeed, float spreadDegrees, int pelletCount, float bulletLifetime)
    {
        Name = name;
        Sprite = sprite;
        FireDelay = fireDelay;
        HeatPerShot = heatPerShot;
        Damage = damage;
        BulletSpeed = bulletSpeed;
        SpreadDegrees = spreadDegrees;
        PelletCount = pelletCount;
        BulletLifetime = bulletLifetime;
    }
}

public enum EnemyKind
{
    Chaser,
    Drone,
    Support,
    Boss
}

public class EnemyController : MonoBehaviour
{
    private const float PlayerRoomEntryMargin = -0.45f;
    private const float EnemyRoomLeashMargin = -0.05f;

    private GameDirector director;
    private PlayerController player;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer markerRenderer;
    private SpriteRenderer healthBackRenderer;
    private SpriteRenderer healthFillRenderer;
    private EnemyKind kind;
    private int health;
    private int maxHealth;
    private float speed;
    private float healthBarWidth;
    private float contactTimer;
    private float shootTimer;
    private float supportPulseTimer;
    private float wakeDistance;
    private Vector2 spawnPosition;
    private Vector2 homeCenter;
    private int homeWidth = 10;
    private int homeHeight = 8;
    private string homeName = "Combat Room";
    private Color baseTint = Color.white;
    private bool active;
    private bool awakened;

    public bool IsAwake => active && awakened;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        EnsureVisuals();
    }

    public void Configure(GameDirector owner, PlayerController target, EnemyKind enemyKind, Vector2 position)
    {
        director = owner;
        player = target;
        kind = enemyKind;
        EnsureVisuals();
        transform.position = new Vector3(position.x, position.y, 0f);
        transform.localScale = Vector3.one;
        spawnPosition = position;
        body.velocity = Vector2.zero;
        active = true;
        awakened = false;
        contactTimer = 0.25f;
        shootTimer = Random.Range(0.45f, 1.1f);
        supportPulseTimer = Random.Range(0f, 1f);

        switch (kind)
        {
            case EnemyKind.Chaser:
                health = maxHealth = 52;
                speed = 3.15f;
                spriteRenderer.sprite = director.ChaserSprite;
                spriteRenderer.transform.localScale = Vector3.one * 2.75f;
                baseTint = new Color(1f, 0.92f, 0.9f, 1f);
                healthBarWidth = 0.95f;
                break;
            case EnemyKind.Drone:
                health = maxHealth = 42;
                speed = 2.1f;
                spriteRenderer.sprite = director.DroneSprite;
                spriteRenderer.transform.localScale = Vector3.one * 2.55f;
                baseTint = new Color(0.95f, 0.98f, 1f, 1f);
                healthBarWidth = 0.9f;
                break;
            case EnemyKind.Support:
                health = maxHealth = 64;
                speed = 1.9f;
                spriteRenderer.sprite = director.SupportSprite;
                spriteRenderer.transform.localScale = Vector3.one * 2.65f;
                baseTint = new Color(0.88f, 1f, 0.9f, 1f);
                healthBarWidth = 1f;
                break;
            default:
                health = maxHealth = 240;
                speed = 2f;
                spriteRenderer.sprite = director.BossSprite;
                spriteRenderer.transform.localScale = Vector3.one * 3.7f;
                baseTint = new Color(1f, 0.88f, 0.86f, 1f);
                healthBarWidth = 1.45f;
                break;
        }

        wakeDistance = kind == EnemyKind.Boss ? 9.2f : kind == EnemyKind.Drone ? 7.2f : 6.6f;
        spriteRenderer.enabled = true;
        spriteRenderer.color = baseTint * 0.62f;
        shadowRenderer.enabled = true;
        markerRenderer.gameObject.SetActive(true);
        markerRenderer.color = new Color(0.35f, 0.75f, 1f, 0.88f);
        healthBackRenderer.gameObject.SetActive(true);
        healthFillRenderer.gameObject.SetActive(true);
        RefreshHealthBar();
    }

    public void SetHomeRoom(Vector2 center, int width, int height, string roomName)
    {
        homeCenter = center;
        homeWidth = Mathf.Max(1, width);
        homeHeight = Mathf.Max(1, height);
        homeName = string.IsNullOrEmpty(roomName) ? "Combat Room" : roomName;
    }

    private void EnsureVisuals()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (spriteRenderer == null)
        {
            Transform visual = transform.Find("Robot Visual");
            GameObject visualObject = visual != null ? visual.gameObject : new GameObject("Robot Visual");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            visualObject.transform.localRotation = Quaternion.identity;
            spriteRenderer = visualObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingOrder = 8;
        }

        if (shadowRenderer == null)
        {
            Transform shadow = transform.Find("Robot Shadow");
            GameObject shadowObject = shadow != null ? shadow.gameObject : new GameObject("Robot Shadow");
            shadowObject.transform.SetParent(transform, false);
            shadowObject.transform.localPosition = new Vector3(0f, -0.36f, 0.05f);
            shadowObject.transform.localRotation = Quaternion.identity;
            shadowObject.transform.localScale = new Vector3(1.15f, 0.28f, 1f);
            shadowRenderer = shadowObject.GetComponent<SpriteRenderer>();
            if (shadowRenderer == null)
            {
                shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
            }

            shadowRenderer.sprite = SpriteFactory.Circle(new Color(0f, 0f, 0f, 0.42f), Color.clear);
            shadowRenderer.sortingOrder = 6;
        }

        if (markerRenderer == null)
        {
            GameObject marker = new GameObject("Red Enemy Beacon");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.62f, -0.08f);
            marker.transform.localScale = Vector3.one * 0.22f;
            markerRenderer = marker.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = SpriteFactory.Circle(new Color(1f, 0.08f, 0.05f, 0.95f), Color.clear);
            markerRenderer.sortingOrder = 12;
        }

        if (healthBackRenderer == null)
        {
            GameObject healthBack = new GameObject("Enemy Health Back");
            healthBack.transform.SetParent(transform, false);
            healthBack.transform.localPosition = new Vector3(0f, 0.94f, -0.08f);
            healthBackRenderer = healthBack.AddComponent<SpriteRenderer>();
            healthBackRenderer.sprite = SpriteFactory.Square(new Color(0.05f, 0.05f, 0.05f, 0.9f), Color.clear);
            healthBackRenderer.sortingOrder = 13;
        }

        if (healthFillRenderer == null)
        {
            GameObject healthFill = new GameObject("Enemy Health Fill");
            healthFill.transform.SetParent(transform, false);
            healthFill.transform.localPosition = new Vector3(0f, 0.94f, -0.09f);
            healthFillRenderer = healthFill.AddComponent<SpriteRenderer>();
            healthFillRenderer.sprite = SpriteFactory.Square(new Color(1f, 0.18f, 0.08f, 0.95f), Color.clear);
            healthFillRenderer.sortingOrder = 14;
        }
    }

    private void FixedUpdate()
    {
        if (!active || player == null || player.IsDead)
        {
            body.velocity = Vector2.zero;
            return;
        }

        if (!awakened)
        {
            body.velocity = Vector2.zero;
            TryWakeUp();
            return;
        }

        Vector2 enemyPosition = transform.position;
        Vector2 playerPosition = player.transform.position;
        bool playerInsideHome = IsInsideHomeRoom(playerPosition, PlayerRoomEntryMargin);
        bool enemyInsideHome = IsInsideHomeRoom(enemyPosition, EnemyRoomLeashMargin);
        Vector2 target = playerInsideHome && enemyInsideHome ? playerPosition : GetHomeAnchor(enemyPosition);
        Vector2 toTarget = target - enemyPosition;
        float distance = toTarget.magnitude;
        Vector2 direction = distance > 0.01f ? toTarget / distance : Vector2.zero;
        float currentSpeed = IsSupported() ? speed * 1.35f : speed;

        if (!playerInsideHome && distance < 0.45f)
        {
            body.velocity = Vector2.zero;
        }
        else if (kind == EnemyKind.Drone && playerInsideHome && distance < 4.2f)
        {
            body.velocity = -direction * currentSpeed * 0.65f;
        }
        else if (kind == EnemyKind.Support && playerInsideHome && distance < 3.5f)
        {
            body.velocity = -direction * currentSpeed * 0.45f;
        }
        else
        {
            body.velocity = direction * currentSpeed;
        }

        if (direction.sqrMagnitude > 0.01f)
        {
            spriteRenderer.flipX = direction.x < -0.05f;
        }
    }

    private void Update()
    {
        if (!active || player == null || player.IsDead)
        {
            return;
        }

        if (!awakened)
        {
            TryWakeUp();
            float dormantPulse = 0.5f + Mathf.Sin(Time.time * 3.2f + spawnPosition.x) * 0.08f;
            spriteRenderer.color = new Color(baseTint.r * dormantPulse, baseTint.g * dormantPulse, baseTint.b * (dormantPulse + 0.2f), 0.9f);
            markerRenderer.color = new Color(0.25f, 0.75f, 1f, 0.75f + Mathf.Sin(Time.time * 4f) * 0.18f);
            return;
        }

        contactTimer -= Time.deltaTime;

        bool playerInsideHome = IsInsideHomeRoom(player.transform.position, PlayerRoomEntryMargin);
        if (playerInsideHome && (kind == EnemyKind.Drone || kind == EnemyKind.Boss))
        {
            shootTimer -= Time.deltaTime;
            if (shootTimer <= 0f)
            {
                FireAtPlayer();
                shootTimer = kind == EnemyKind.Boss ? 0.75f : 1.45f;
            }
        }
        else
        {
            shootTimer = Mathf.Min(shootTimer, 0.45f);
        }

        if (kind == EnemyKind.Support)
        {
            supportPulseTimer += Time.deltaTime;
            float pulse = 0.8f + Mathf.Sin(supportPulseTimer * 8f) * 0.2f;
            spriteRenderer.color = new Color(pulse, 1f, pulse, 1f);
        }
        else
        {
            float chasePulse = 0.9f + Mathf.Sin(Time.time * 6f) * 0.08f;
            spriteRenderer.color = IsSupported() ? new Color(0.8f, 1f, 0.8f, 1f) : new Color(1f, chasePulse, chasePulse, 1f);
        }
    }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (!active)
        {
            return;
        }

        WakeUp();
        health -= amount;
        body.AddForce(knockback, ForceMode2D.Impulse);
        RefreshHealthBar();
        if (health <= 0)
        {
            Die();
        }
    }

    private void RefreshHealthBar()
    {
        if (healthBackRenderer == null || healthFillRenderer == null)
        {
            return;
        }

        float healthRatio = maxHealth > 0 ? Mathf.Clamp01((float)health / maxHealth) : 0f;
        float barHeight = kind == EnemyKind.Boss ? 0.09f : 0.075f;
        healthBackRenderer.transform.localScale = new Vector3(healthBarWidth, barHeight, 1f);
        healthFillRenderer.transform.localScale = new Vector3(Mathf.Max(0.04f, healthBarWidth * healthRatio), barHeight * 0.62f, 1f);
        healthFillRenderer.transform.localPosition = new Vector3(-(healthBarWidth - healthBarWidth * healthRatio) * 0.5f, 0.94f, -0.09f);
    }

    private void FireAtPlayer()
    {
        Vector2 direction = (player.transform.position - transform.position).normalized;
        Bullet bullet = director.GetEnemyBullet();
        bullet.Fire((Vector2)transform.position + direction * 0.55f, direction, kind == EnemyKind.Boss ? 8.5f : 7.2f, kind == EnemyKind.Boss ? 12 : 8, 2.2f);

        if (kind == EnemyKind.Boss)
        {
            Vector2 left = Quaternion.Euler(0f, 0f, 18f) * direction;
            Vector2 right = Quaternion.Euler(0f, 0f, -18f) * direction;
            director.GetEnemyBullet().Fire((Vector2)transform.position + left * 0.55f, left, 7.5f, 8, 2.1f);
            director.GetEnemyBullet().Fire((Vector2)transform.position + right * 0.55f, right, 7.5f, 8, 2.1f);
        }
    }

    private void TryWakeUp()
    {
        if (player == null)
        {
            return;
        }

        float playerDistance = Vector2.Distance(player.transform.position, transform.position);
        float spawnDistance = Vector2.Distance(player.transform.position, spawnPosition);
        if (IsInsideHomeRoom(player.transform.position, PlayerRoomEntryMargin) && (playerDistance <= wakeDistance || spawnDistance <= wakeDistance))
        {
            WakeUp();
        }
    }

    private void WakeUp()
    {
        if (awakened)
        {
            return;
        }

        awakened = true;
        contactTimer = 0.35f;
        shootTimer = Random.Range(0.35f, 0.9f);
        spriteRenderer.color = baseTint;
        markerRenderer.color = new Color(1f, 0.08f, 0.05f, 0.95f);
        director?.ShowStatus($"{kind} machine awakened in {homeName}", 0.9f);
    }

    private bool IsInsideHomeRoom(Vector2 point, float margin)
    {
        float halfWidth = Mathf.Max(0.5f, homeWidth * 0.5f + margin);
        float halfHeight = Mathf.Max(0.5f, homeHeight * 0.5f + margin);
        return point.x >= homeCenter.x - halfWidth
            && point.x <= homeCenter.x + halfWidth
            && point.y >= homeCenter.y - halfHeight
            && point.y <= homeCenter.y + halfHeight;
    }

    private Vector2 GetHomeAnchor(Vector2 enemyPosition)
    {
        float halfWidth = Mathf.Max(0.5f, homeWidth * 0.5f - 1.8f);
        float halfHeight = Mathf.Max(0.5f, homeHeight * 0.5f - 1.8f);
        Vector2 clamped = new Vector2(
            Mathf.Clamp(enemyPosition.x, homeCenter.x - halfWidth, homeCenter.x + halfWidth),
            Mathf.Clamp(enemyPosition.y, homeCenter.y - halfHeight, homeCenter.y + halfHeight));

        return Vector2.Distance(clamped, enemyPosition) > 0.1f ? clamped : homeCenter;
    }

    private bool IsSupported()
    {
        if (kind == EnemyKind.Support)
        {
            return false;
        }

        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        foreach (EnemyController enemy in enemies)
        {
            if (enemy != this && enemy.gameObject.activeInHierarchy && enemy.kind == EnemyKind.Support)
            {
                if (Vector2.Distance(transform.position, enemy.transform.position) < 4f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        PlayerController hitPlayer = collision.collider.GetComponent<PlayerController>();
        if (hitPlayer != null && contactTimer <= 0f && IsInsideHomeRoom(hitPlayer.transform.position, PlayerRoomEntryMargin))
        {
            WakeUp();
            hitPlayer.TakeDamage(kind == EnemyKind.Boss ? 18 : 10);
            contactTimer = 0.7f;
        }
    }

    private void Die()
    {
        active = false;
        Vector2 deathPosition = transform.position;
        if (markerRenderer != null)
        {
            markerRenderer.gameObject.SetActive(false);
        }

        if (healthBackRenderer != null)
        {
            healthBackRenderer.gameObject.SetActive(false);
        }

        if (healthFillRenderer != null)
        {
            healthFillRenderer.gameObject.SetActive(false);
        }

        if (shadowRenderer != null)
        {
            shadowRenderer.enabled = false;
        }

        int scrapValue = kind == EnemyKind.Boss ? 12 : Mathf.Max(2, maxHealth / 24);
        for (int i = 0; i < scrapValue; i++)
        {
            director.SpawnScrap(deathPosition, 1);
        }

        director.RewardEnemyDefeat(kind, deathPosition);
        director.TrySpawnWeaponDrop(deathPosition, kind == EnemyKind.Boss ? 1f : 0.18f);
        director.ReleaseEnemy(this);
    }
}

public class DestructibleProp : MonoBehaviour
{
    private GameDirector director;
    private SpriteRenderer spriteRenderer;
    private int health;
    private int scrapDrops;
    private float weaponDropChance;
    private bool volatileProp;
    private bool destroyed;
    private Color baseColor = Color.white;

    public void Configure(GameDirector owner, int hitPoints, int scrapCount, float dropChance, bool explosive)
    {
        director = owner;
        health = hitPoints;
        scrapDrops = scrapCount;
        weaponDropChance = dropChance;
        volatileProp = explosive;
        destroyed = false;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }
    }

    public void TakeDamage(int amount, Vector2 hitDirection)
    {
        if (destroyed)
        {
            return;
        }

        health -= amount;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(baseColor, Color.white, 0.45f);
        }

        if (health <= 0)
        {
            BreakApart(hitDirection);
        }
    }

    private void LateUpdate()
    {
        if (!destroyed && spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(spriteRenderer.color, baseColor, Time.deltaTime * 8f);
        }
    }

    private void BreakApart(Vector2 hitDirection)
    {
        destroyed = true;
        Vector2 origin = transform.position;
        int finalScrap = volatileProp ? scrapDrops + 2 : scrapDrops;
        for (int i = 0; i < finalScrap; i++)
        {
            Vector2 scatter = Random.insideUnitCircle * 0.8f + hitDirection.normalized * 0.15f;
            director.SpawnScrap(origin + scatter, 1);
        }

        director.TrySpawnWeaponDrop(origin, weaponDropChance);
        if (volatileProp)
        {
            Explode(origin);
        }

        Destroy(gameObject);
    }

    private void Explode(Vector2 origin)
    {
        const float radius = 2.4f;
        director.PlayPurgeEffect(origin, radius);
        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        foreach (EnemyController enemy in enemies)
        {
            if (!enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 offset = (Vector2)enemy.transform.position - origin;
            if (offset.magnitude <= radius)
            {
                enemy.TakeDamage(30, offset.normalized * 5.5f);
            }
        }

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null && Vector2.Distance(player.transform.position, origin) <= radius * 0.85f)
        {
            player.TakeDamage(8);
        }

        director.ShowStatus("Fuel barrel detonated", 1f);
    }
}

public class ScrapPickup : MonoBehaviour
{
    private int value;

    public void Configure(Vector2 position, int scrapValue)
    {
        transform.position = position;
        transform.localScale = Vector3.one * Random.Range(0.75f, 1.1f);
        value = scrapValue;
        gameObject.SetActive(true);
    }

    public void DriftToward(Vector2 target)
    {
        transform.position = Vector2.MoveTowards(transform.position, target, 5.5f * Time.deltaTime);
    }

    public void Collect(PlayerController player)
    {
        player.AddScrap(value);
        gameObject.SetActive(false);
    }
}

public class WeaponPickup : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer glowRenderer;
    private WeaponStats stats;
    private float bobSeed;

    public WeaponStats Stats => stats;
    public string DisplayName => stats != null ? stats.Name : "Weapon";

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        bobSeed = Random.Range(0f, 10f);
    }

    public void Configure(Vector2 position, WeaponStats weaponStats)
    {
        stats = weaponStats;
        transform.position = position;
        transform.localScale = Vector3.one * 4.25f;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        EnsureGlow();

        spriteRenderer.sprite = stats.Sprite;
        spriteRenderer.sortingOrder = 9;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        float bob = Mathf.Sin((Time.time + bobSeed) * 2.8f) * 0.08f;
        transform.localScale = Vector3.one * (4.25f + bob);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin((Time.time + bobSeed) * 2.8f) * 6f);
    }

    public void Collect(PlayerController player)
    {
        player.EquipWeapon(stats);
        gameObject.SetActive(false);
    }

    private void EnsureGlow()
    {
        if (glowRenderer != null)
        {
            return;
        }

        GameObject glow = new GameObject("Pickup Glow");
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        glow.transform.localScale = new Vector3(1.15f, 0.45f, 1f);
        glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = SpriteFactory.Circle(new Color(0.35f, 0.85f, 1f, 0.32f), Color.clear);
        glowRenderer.sortingOrder = 5;
    }
}

public class SupplyStation : MonoBehaviour
{
    private GameDirector director;
    private bool coolingStation;
    private float nextUseTime;

    public void Configure(GameDirector owner, bool coolsWeapon)
    {
        director = owner;
        coolingStation = coolsWeapon;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time < nextUseTime)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            return;
        }

        nextUseTime = Time.time + 1.25f;
        if (coolingStation)
        {
            player.CoolWeapon(24f);
            director.ShowStatus("Cooling station vented weapon heat", 1.2f);
        }
        else
        {
            player.RepairArmor(10);
            director.ShowStatus("Repair station restored armor", 1.2f);
        }
    }
}

public class GameHud
{
    private readonly PlayerController player;
    private readonly GameDirector director;
    private readonly Image armorFill;
    private readonly Image heatFill;
    private readonly Image damageOverlay;
    private readonly Text statusText;
    private readonly Text statsText;
    private float damageFlash;

    private GameHud(PlayerController playerController, GameDirector owner, Image armor, Image heat, Image damage, Text status, Text stats)
    {
        player = playerController;
        director = owner;
        armorFill = armor;
        heatFill = heat;
        damageOverlay = damage;
        statusText = status;
        statsText = stats;
    }

    public static GameHud Create(PlayerController player, GameDirector owner)
    {
        GameObject canvasObject = new GameObject("HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        Image damage = CreatePanel(canvasObject.transform, "Damage Flash", Anchor.Stretch, Vector2.zero, new Vector2(0f, 0f), new Color(1f, 0f, 0f, 0f));

        Text title = CreateText(canvasObject.transform, "Title", "GEAR SCAVENGER", 24, TextAnchor.UpperLeft);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(24f, -18f);
        title.rectTransform.sizeDelta = new Vector2(420f, 42f);
        title.color = new Color(0.73f, 1f, 0.95f);

        Text stats = CreateText(canvasObject.transform, "Stats", "", 16, TextAnchor.UpperLeft);
        stats.rectTransform.anchorMin = new Vector2(0f, 1f);
        stats.rectTransform.anchorMax = new Vector2(0f, 1f);
        stats.rectTransform.anchoredPosition = new Vector2(24f, -52f);
        stats.rectTransform.sizeDelta = new Vector2(520f, 80f);

        Image armorBack = CreatePanel(canvasObject.transform, "Armor Bar", Anchor.TopLeft, new Vector2(24f, -122f), new Vector2(260f, 18f), new Color(0.02f, 0.05f, 0.06f, 0.85f));
        Image armorFill = CreatePanel(armorBack.transform, "Armor Fill", Anchor.FillLeft, Vector2.zero, Vector2.zero, new Color(0.3f, 0.9f, 1f, 0.95f));

        Image heatBack = CreatePanel(canvasObject.transform, "Heat Bar", Anchor.TopLeft, new Vector2(24f, -148f), new Vector2(260f, 18f), new Color(0.06f, 0.035f, 0.02f, 0.85f));
        Image heatFill = CreatePanel(heatBack.transform, "Heat Fill", Anchor.FillLeft, Vector2.zero, Vector2.zero, new Color(1f, 0.45f, 0.14f, 0.95f));

        Text status = CreateText(canvasObject.transform, "Status", "WASD move  |  Mouse fire  |  Space dash  |  Q Scrap Nova  |  F Magnetic Guard  |  R purge heat", 18, TextAnchor.LowerCenter);
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.anchoredPosition = new Vector2(0f, 34f);
        status.rectTransform.sizeDelta = new Vector2(-40f, 50f);
        status.color = new Color(0.92f, 0.96f, 1f);

        return new GameHud(player, owner, armorFill, heatFill, damage, status, stats);
    }

    public void Refresh(int wave, int awakeEnemyCount, int totalEnemyCount)
    {
        armorFill.fillAmount = Mathf.Clamp01((float)player.Armor / player.MaxArmor);
        heatFill.fillAmount = Mathf.Clamp01(player.Heat / player.MaxHeat);
        heatFill.color = player.IsOverheated ? new Color(1f, 0.08f, 0.05f) : new Color(1f, 0.45f, 0.14f);
        int pickupCount = director != null ? director.ActiveWeaponPickupCount() : 0;

        int cores = director != null ? director.SalvageCores : 0;
        int kills = director != null ? director.DefeatedMachines : 0;
        statsText.text = $"Armor {player.Armor}/{player.MaxArmor}   Heat {Mathf.RoundToInt(player.Heat)}%   Scrap {player.Scrap}   Nova {player.NovaScrapCost}   Guard {player.GuardScrapCost}   Cores {cores}/3   Kills {kills}   Weapon {player.WeaponName}   Wave {wave}/5   Awake {awakeEnemyCount}/{totalEnemyCount}   Pickups {pickupCount}";

        if (damageFlash > 0f)
        {
            damageFlash = Mathf.Max(0f, damageFlash - Time.deltaTime * 2.8f);
            damageOverlay.color = new Color(1f, 0.05f, 0.02f, damageFlash * 0.28f);
        }
    }

    public void SetMessage(string message)
    {
        statusText.text = message;
    }

    public void FlashDamage()
    {
        damageFlash = 1f;
    }

    private static Text CreateText(Transform parent, string name, string content, int size, TextAnchor alignment)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = alignment;
        text.text = content;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Image CreatePanel(Transform parent, string name, Anchor anchor, Vector2 position, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;

        RectTransform rect = image.rectTransform;
        switch (anchor)
        {
            case Anchor.Stretch:
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                break;
            case Anchor.TopLeft:
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                break;
            case Anchor.FillLeft:
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                break;
        }

        return image;
    }

    private enum Anchor
    {
        Stretch,
        TopLeft,
        FillLeft
    }
}

public class ObjectPool
{
    private readonly Transform root;
    private readonly List<GameObject> pooled = new List<GameObject>();
    private readonly System.Func<GameObject> factory;

    public ObjectPool(string name, int preload, System.Func<GameObject> create)
    {
        factory = create;
        root = new GameObject(name).transform;

        for (int i = 0; i < preload; i++)
        {
            GameObject obj = factory();
            obj.transform.SetParent(root);
            obj.SetActive(false);
            pooled.Add(obj);
        }
    }

    public GameObject Get()
    {
        GameObject obj = null;
        for (int i = 0; i < pooled.Count; i++)
        {
            if (!pooled[i].activeInHierarchy)
            {
                obj = pooled[i];
                break;
            }
        }

        if (obj == null)
        {
            obj = factory();
            pooled.Add(obj);
        }

        obj.transform.SetParent(root);
        obj.SetActive(true);
        return obj;
    }
}

public class CameraFollow : MonoBehaviour
{
    public Transform Target { get; set; }

    private void LateUpdate()
    {
        if (Target == null)
        {
            return;
        }

        Vector3 target = new Vector3(Target.position.x, Target.position.y, -10f);
        transform.position = Vector3.Lerp(transform.position, target, 8f * Time.deltaTime);
    }
}

public class WallMarker : MonoBehaviour
{
}

public abstract class WeaponModule : ScriptableObject
{
    public string DisplayName = "Prototype Module";
}

[CreateAssetMenu(menuName = "Gear Scavenger/Weapon Core")]
public class WeaponCoreModule : WeaponModule
{
    public float FireDelay = 0.14f;
    public float HeatPerShot = 9.5f;
}

[CreateAssetMenu(menuName = "Gear Scavenger/Weapon Ammo")]
public class WeaponAmmoModule : WeaponModule
{
    public int Damage = 18;
    public float BulletSpeed = 13f;
}

[CreateAssetMenu(menuName = "Gear Scavenger/Weapon Stock")]
public class WeaponStockModule : WeaponModule
{
    public float SpreadDegrees = 3.5f;
}

public static class SpriteFactory
{
    private const int Size = 32;

    public static Sprite Square(Color fill, Color border)
    {
        return Create(fill, border, Shape.Square);
    }

    public static Sprite Circle(Color fill, Color border)
    {
        return Create(fill, border, Shape.Circle);
    }

    public static Sprite Diamond(Color fill, Color border)
    {
        return Create(fill, border, Shape.Diamond);
    }

    public static Sprite Bolt(Color fill, Color border)
    {
        return Create(fill, border, Shape.Bolt);
    }

    private static Sprite Create(Color fill, Color border, Shape shape)
    {
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Vector2 center = new Vector2((Size - 1) * 0.5f, (Size - 1) * 0.5f);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 pixel = new Vector2(x, y);
                bool inside = IsInside(pixel, center, shape, 13.5f);
                bool edge = inside && !IsInside(pixel, center, shape, 10.8f);
                texture.SetPixel(x, y, inside ? (edge && border.a > 0f ? border : fill) : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
    }

    private static bool IsInside(Vector2 pixel, Vector2 center, Shape shape, float radius)
    {
        Vector2 offset = pixel - center;
        switch (shape)
        {
            case Shape.Circle:
                return offset.sqrMagnitude <= radius * radius;
            case Shape.Diamond:
                return Mathf.Abs(offset.x) + Mathf.Abs(offset.y) <= radius * 1.25f;
            case Shape.Bolt:
                return offset.x > -radius * 0.9f
                    && offset.x < radius * 0.95f
                    && Mathf.Abs(offset.y) < Mathf.Lerp(radius * 0.22f, radius * 0.42f, Mathf.InverseLerp(-radius, radius, offset.x));
            default:
                return Mathf.Abs(offset.x) <= radius && Mathf.Abs(offset.y) <= radius;
        }
    }

    private enum Shape
    {
        Square,
        Circle,
        Diamond,
        Bolt
    }
}
