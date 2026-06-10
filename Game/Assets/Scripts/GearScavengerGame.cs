using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    private readonly Dictionary<GameMode, Button> modeButtons = new Dictionary<GameMode, Button>();

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
    private GameObject pauseCanvas;
    private GameObject resultCanvas;
    private Text modeDescriptionText;
    private GameMode selectedMode = GameMode.Story;
    private int wave;
    private int salvageCores;
    private int defeatedMachines;
    private int roomsCleared;
    private bool gameOver;
    private bool gameStarted;
    private bool isPaused;
    private float statusTimer;

    public Sprite FloorSprite { get; private set; }
    public Sprite WallSprite { get; private set; }
    public Sprite PlayerSprite { get; private set; }
    public Sprite ChaserSprite { get; private set; }
    public Sprite DroneSprite { get; private set; }
    public Sprite SupportSprite { get; private set; }
    public Sprite BulwarkSprite { get; private set; }
    public Sprite ArtillerySprite { get; private set; }
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
    public Sprite SkillCoreSprite { get; private set; }
    public Sprite ShockFieldSprite { get; private set; }
    public Sprite CoolantZoneSprite { get; private set; }
    public Sprite DefenseTurretSprite { get; private set; }
    public int SalvageCores => salvageCores;
    public int DefeatedMachines => defeatedMachines;
    public int RoomsCleared => roomsCleared;
    public bool UsingCandidateArt { get; private set; }
    public bool IsGameplayPaused => isPaused || gameOver;

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

        wave = 1;
        salvageCores = 0;
        defeatedMachines = 0;
        roomsCleared = 0;
        gameOver = false;
        isPaused = false;
        Time.timeScale = 1f;
        statusTimer = 0f;
        BuildLevel(false);
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
        if (Input.GetKeyDown(KeyCode.F11))
        {
            Screen.fullScreen = !Screen.fullScreen;
            if (gameStarted)
            {
                ShowStatus(Screen.fullScreen ? "Fullscreen enabled" : "Windowed display enabled", 1.2f);
            }
        }

        if (!gameStarted)
        {
            return;
        }

        if (!gameOver && Input.GetKeyDown(KeyCode.Escape))
        {
            SetPaused(!isPaused);
        }

        if (isPaused)
        {
            return;
        }

        hud?.Refresh(wave, AlertEnemyCount(), activeEnemies.Count);

        if (statusTimer > 0f)
        {
            statusTimer -= Time.deltaTime;
            if (statusTimer <= 0f && !gameOver)
            {
                hud?.SetMessage(wave < 4
                    ? "Clear all four combat rooms to generate the next wave map"
                    : "Boss Wave 4: destroy the three Boss machines and their escorts");
            }
        }

    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
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

        Text subtitle = CreateMenuText(mainMenuCanvas.transform, "Subtitle", "Rebuild a scavenger robot, clear three sectors, then destroy the Wave 4 Boss trio.  F11: fullscreen", 22, TextAnchor.MiddleCenter, new Color(0.86f, 0.92f, 0.9f));
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
        CreateModeButton(modePanel.transform, "Training Mode", "Short five-minute rehearsal", new Vector2(215f, -242f), GameMode.Training);

        modeDescriptionText = CreateMenuText(modePanel.transform, "Mode Description", "", 20, TextAnchor.UpperLeft, new Color(0.86f, 0.94f, 0.9f));
        modeDescriptionText.rectTransform.anchorMin = new Vector2(0f, 0f);
        modeDescriptionText.rectTransform.anchorMax = new Vector2(1f, 0f);
        modeDescriptionText.rectTransform.anchoredPosition = new Vector2(0f, 46f);
        modeDescriptionText.rectTransform.sizeDelta = new Vector2(-54f, 64f);
        modeDescriptionText.rectTransform.offsetMin = new Vector2(28f, modeDescriptionText.rectTransform.offsetMin.y);
        modeDescriptionText.rectTransform.offsetMax = new Vector2(-28f, modeDescriptionText.rectTransform.offsetMax.y);
        UpdateModeDescription();
        UpdateModeButtonVisuals();

        Image actionPanel = CreateMenuImage(mainMenuCanvas.transform, "Action Panel", new Color(0.018f, 0.03f, 0.034f, 0.94f));
        actionPanel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        actionPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        actionPanel.rectTransform.anchoredPosition = new Vector2(250f, -48f);
        actionPanel.rectTransform.sizeDelta = new Vector2(430f, 370f);

        Text brief = CreateMenuText(actionPanel.transform, "Briefing", "MISSION BRIEFING\n\nClear three normal Waves while preserving weapons and upgrades. After Wave 3, enter the isolated arena and destroy three different Boss machines.\n\nSTART GAME: launch selected mode\nTUTORIAL: controls and rules\nQUIT: exit built game", 20, TextAnchor.UpperLeft, new Color(0.92f, 0.96f, 0.94f));
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
            UpdateModeButtonVisuals();
        });
        modeButtons[mode] = button;

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

        string tutorial = "WASD: move the robot\nMouse: aim and fire\nSpace: dash through danger\nE: equip nearby weapon drops\nQ: spend 10 scrap to release Scrap Nova\nF: spend 6 scrap for Magnetic Guard\nR: purge weapon heat\nEsc: pause or resume\nF11: toggle fullscreen\n\nEntering a combat room alerts every machine in that room. Damage removes armor before core integrity. Touch Skill Cores to install upgrades. Complete Wave 3 to enter the isolated three-Boss arena in Wave 4.";
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

    private void SetPaused(bool paused)
    {
        if (!gameStarted || gameOver)
        {
            return;
        }

        isPaused = paused;
        if (pauseCanvas == null)
        {
            CreatePauseMenu();
        }

        pauseCanvas.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
    }

    private void CreatePauseMenu()
    {
        EnsureEventSystem();
        pauseCanvas = new GameObject("Pause Menu");
        Canvas canvas = pauseCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = pauseCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        pauseCanvas.AddComponent<GraphicRaycaster>();

        Image shade = CreateMenuImage(pauseCanvas.transform, "Pause Shade", new Color(0f, 0f, 0f, 0.78f));
        Stretch(shade.rectTransform);
        Image panel = CreateMenuImage(pauseCanvas.transform, "Pause Panel", new Color(0.02f, 0.035f, 0.04f, 0.98f));
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        panel.rectTransform.sizeDelta = new Vector2(520f, 360f);

        Text title = CreateMenuText(panel.transform, "Pause Title", "SYSTEM PAUSED", 42, TextAnchor.MiddleCenter, new Color(0.68f, 1f, 0.94f));
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        title.rectTransform.sizeDelta = new Vector2(460f, 70f);
        Text help = CreateMenuText(panel.transform, "Pause Help", "Combat simulation suspended\nPress Esc or select RESUME to continue", 22, TextAnchor.MiddleCenter, new Color(0.88f, 0.94f, 0.94f));
        help.rectTransform.anchorMin = help.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        help.rectTransform.anchoredPosition = new Vector2(0f, -135f);
        help.rectTransform.sizeDelta = new Vector2(440f, 70f);

        CreateMenuButton(panel.transform, "RESUME", new Vector2(260f, -230f), new Vector2(360f, 58f), new Color(0.08f, 0.52f, 0.45f, 0.98f), () => SetPaused(false));
        CreateMenuButton(panel.transform, "RETURN TO MAIN MENU", new Vector2(260f, -302f), new Vector2(360f, 54f), new Color(0.34f, 0.2f, 0.14f, 0.98f), ReturnToMainMenu);
        pauseCanvas.SetActive(false);
    }

    private void FinishMission(bool victory)
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;
        isPaused = false;
        Time.timeScale = 0f;
        CreateResultScreen(victory);
    }

    private void CreateResultScreen(bool victory)
    {
        EnsureEventSystem();
        resultCanvas = new GameObject(victory ? "Mission Complete Screen" : "Mission Failed Screen");
        Canvas canvas = resultCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        CanvasScaler scaler = resultCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        resultCanvas.AddComponent<GraphicRaycaster>();

        Image shade = CreateMenuImage(resultCanvas.transform, "Result Shade", new Color(0f, 0f, 0f, 0.86f));
        Stretch(shade.rectTransform);
        Image panel = CreateMenuImage(resultCanvas.transform, "Result Panel", new Color(0.018f, 0.035f, 0.04f, 0.99f));
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        panel.rectTransform.sizeDelta = new Vector2(680f, 470f);

        string titleText = victory ? "MISSION COMPLETE" : "SCAVENGER DESTROYED";
        Color titleColor = victory ? new Color(0.55f, 1f, 0.78f) : new Color(1f, 0.28f, 0.2f);
        Text title = CreateMenuText(panel.transform, "Result Title", titleText, 50, TextAnchor.MiddleCenter, titleColor);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -68f);
        title.rectTransform.sizeDelta = new Vector2(620f, 78f);

        string summary = victory
            ? "The Breaker, Siege Titan, and Reactor Warden have been destroyed.\nThe mechanical sector is secure."
            : "Core integrity reached zero before the Boss sector was secured.";
        Text body = CreateMenuText(panel.transform, "Result Summary", $"{summary}\n\nWaves reached: {wave} / 4\nMachines defeated: {defeatedMachines}\nSalvage cores recovered: {salvageCores} / 3\nFinal weapon: {(player != null ? player.WeaponName : "None")}", 24, TextAnchor.MiddleCenter, new Color(0.9f, 0.96f, 0.94f));
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        body.rectTransform.anchoredPosition = new Vector2(0f, -225f);
        body.rectTransform.sizeDelta = new Vector2(590f, 230f);

        CreateMenuButton(panel.transform, "RETURN TO MAIN MENU", new Vector2(340f, -410f), new Vector2(420f, 60f), new Color(0.08f, 0.52f, 0.45f, 0.98f), ReturnToMainMenu);
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
                modeDescriptionText.text = "Challenge Mode: the normal mission plus an extra opening attack group. More enemies immediately; the three-Boss arena still arrives in Wave 4.";
                break;
            case GameMode.Training:
                modeDescriptionText.text = "Training Mode: reduced room squads and extra starting weapons. Best for a five-minute release check or presentation rehearsal.";
                break;
            default:
                modeDescriptionText.text = "Story Mode: standard enemy counts and rewards. Clear Waves 1-3, then fight three different Boss machines in Wave 4.";
                break;
        }
    }

    private void UpdateModeButtonVisuals()
    {
        foreach (KeyValuePair<GameMode, Button> entry in modeButtons)
        {
            bool selected = entry.Key == selectedMode;
            Color color = selected
                ? new Color(0.08f, 0.48f, 0.42f, 0.98f)
                : new Color(0.1f, 0.18f, 0.2f, 0.96f);
            Image image = entry.Value.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            ColorBlock colors = entry.Value.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(Mathf.Min(color.r + 0.12f, 1f), Mathf.Min(color.g + 0.12f, 1f), Mathf.Min(color.b + 0.12f, 1f), color.a);
            entry.Value.colors = colors;
        }
    }

    private string GetModeStartMessage()
    {
        switch (selectedMode)
        {
            case GameMode.Challenge:
                return "Challenge Mode: extra opening enemies. Clear three normal Waves, then defeat the Wave 4 Boss trio.";
            case GameMode.Training:
                return "Training Mode: reduced squads and extra weapons. Rehearse the full menu-to-Boss flow in about five minutes.";
            default:
                return "Story Mode: clear three normal Waves, collect 3 salvage cores, then defeat the Wave 4 Boss trio.";
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
        int bonusScrap = IsBossKind(kind) ? 10
            : kind == EnemyKind.Bulwark ? 6
            : kind == EnemyKind.Support || kind == EnemyKind.Artillery ? 4
            : 2;
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

    private static bool IsBossKind(EnemyKind kind)
    {
        return kind == EnemyKind.Boss || kind == EnemyKind.SiegeBoss || kind == EnemyKind.ReactorBoss;
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
        SpawnEnemy(EnemyKind.Drone, PickRoomFloorPoint(room, new Vector2(0f, ry)));

        if (selectedMode == GameMode.Training)
        {
            return;
        }

        SpawnEnemy(EnemyKind.Chaser, PickRoomFloorPoint(room, new Vector2(rx, -ry)));

        if (includeSupport || room.Difficulty >= 2)
        {
            SpawnEnemy(EnemyKind.Support, PickRoomFloorPoint(room, new Vector2(0f, -ry * 0.15f)));
            SpawnEnemy(EnemyKind.Artillery, PickRoomFloorPoint(room, new Vector2(-rx * 0.45f, ry * 0.45f)));
        }

        if (room.Difficulty >= 3)
        {
            SpawnEnemy(EnemyKind.Bulwark, PickRoomFloorPoint(room, new Vector2(rx * 0.35f, ry * 0.15f)));
            SpawnEnemy(EnemyKind.Artillery, PickRoomFloorPoint(room, new Vector2(rx * 0.45f, ry * 0.45f)));
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

    public void ShowWeaponComparison(WeaponStats current, WeaponStats candidate)
    {
        hud?.ShowWeaponComparison(current, candidate);
    }

    public void HideWeaponComparison()
    {
        hud?.HideWeaponComparison();
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
        hud?.SetMessage("Core integrity destroyed.");
        FinishMission(false);
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
        yield return new WaitForSeconds(0.4f);
        while (!gameOver)
        {
            while (activeEnemies.Count > 0 && !gameOver)
            {
                yield return null;
            }

            if (gameOver)
            {
                yield break;
            }

            if (wave >= 4)
            {
                hud?.SetMessage("BOSS TRIO DESTROYED - mission complete.");
                FinishMission(true);
                yield break;
            }

            AwardRoomClearReward(wave == 1
                ? "First sweep complete: four combat rooms cleared."
                : $"Wave {wave} complete: four combat rooms cleared.");

            wave++;
            bool isBossWave = wave == 4;
            ShowStatus(isBossWave
                ? "Wave 3 complete. Building the isolated Breaker Boss arena..."
                : $"Wave {wave - 1} complete. Generating a new four-room sector for Wave {wave}...", 1.4f);
            yield return new WaitForSeconds(0.35f);

            PrepareNextWaveMap(isBossWave);
            if (isBossWave)
            {
                SpawnBossWave();
                ShowStatus("BOSS WAVE 4: destroy the three Boss machines and their escorts", 3f);
            }
            else
            {
                SpawnGuaranteedRoomEnemies();
                if (selectedMode != GameMode.Training)
                {
                    SpawnExtraWavePressure(wave);
                }

                SpawnRoomWeaponDrops(1);
                ShowStatus($"WAVE {wave}: clear all four combat rooms", 2.5f);
            }
        }
    }

    private void AwardRoomClearReward(string message)
    {
        roomsCleared++;
        salvageCores = Mathf.Min(3, salvageCores + 1);
        player?.RepairArmor(18 + roomsCleared * 4);
        player?.RepairHealth(12 + roomsCleared * 3);
        ShowStatus($"{message}  Cores {salvageCores}/3", 2.4f);
    }

    private void PrepareNextWaveMap(bool bossArena)
    {
        DeactivateTransientObjects();
        BuildLevel(bossArena);
        Vector2 spawn = ToNearestFloorPoint(bossArena ? new Vector2(0f, -3.2f) : Vector2.zero);
        player?.MoveToNewMap(spawn);
    }

    private void DeactivateTransientObjects()
    {
        foreach (WeaponPickup pickup in FindObjectsOfType<WeaponPickup>())
        {
            pickup.gameObject.SetActive(false);
        }

        foreach (ScrapPickup pickup in FindObjectsOfType<ScrapPickup>())
        {
            pickup.gameObject.SetActive(false);
        }

        foreach (Bullet bullet in FindObjectsOfType<Bullet>())
        {
            bullet.gameObject.SetActive(false);
        }
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

        if (waveNumber >= 3)
        {
            SpawnEnemy(EnemyKind.Artillery, PickCombatSpawnPoint(waveNumber + 10));
        }

        if (waveNumber >= 4)
        {
            SpawnEnemy(EnemyKind.Bulwark, PickCombatSpawnPoint(waveNumber + 20));
        }
    }

    private void SpawnBossWave()
    {
        RoomDefinition room = GetBossRoom();
        SpawnEnemy(EnemyKind.Boss, PickRoomFloorPoint(room, new Vector2(0f, 2.2f)));
        SpawnEnemy(EnemyKind.SiegeBoss, PickRoomFloorPoint(room, new Vector2(-5f, 0f)));
        SpawnEnemy(EnemyKind.ReactorBoss, PickRoomFloorPoint(room, new Vector2(5f, 0f)));
        SpawnEnemy(EnemyKind.Support, PickRoomFloorPoint(room, new Vector2(0f, -1.8f)));
        SpawnEnemy(EnemyKind.Artillery, PickRoomFloorPoint(room, new Vector2(-5f, 3.8f)));
        SpawnEnemy(EnemyKind.Artillery, PickRoomFloorPoint(room, new Vector2(5f, 3.8f)));
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
        BulwarkSprite = LoadSprite("enemy_bulwark", SpriteFactory.Square(new Color(0.3f, 0.55f, 0.95f), new Color(0.04f, 0.1f, 0.24f)));
        ArtillerySprite = LoadSprite("enemy_artillery", SpriteFactory.Diamond(new Color(0.82f, 0.35f, 0.95f), new Color(0.18f, 0.04f, 0.24f)));
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
        SkillCoreSprite = SpriteFactory.Diamond(new Color(0.95f, 0.9f, 0.35f), new Color(0.18f, 0.08f, 0.02f));
        ShockFieldSprite = SpriteFactory.Circle(new Color(1f, 0.18f, 0.08f, 0.34f), new Color(1f, 0.68f, 0.12f, 0.72f));
        CoolantZoneSprite = SpriteFactory.Circle(new Color(0.1f, 0.72f, 0.92f, 0.28f), new Color(0.35f, 1f, 0.96f, 0.62f));
        DefenseTurretSprite = SpriteFactory.Diamond(new Color(0.28f, 0.92f, 0.76f), new Color(0.04f, 0.18f, 0.16f));

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
        Sprite candidate = LoadCandidateSprite(assetName);
        if (candidate != null)
        {
            UsingCandidateArt = true;
            return candidate;
        }

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

    private Sprite LoadCandidateSprite(string assetName)
    {
        string candidatePath = Path.Combine(Application.dataPath, "ArtCandidates", "PreparedRuntime", $"{assetName}.png");
        if (!File.Exists(candidatePath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(candidatePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            Destroy(texture);
            return null;
        }

        texture.name = $"Candidate {assetName}";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
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

    private void BuildLevel(bool bossArena)
    {
        RemoveSceneObject("Generated Mechanical Rooms");
        GameObject levelRoot = new GameObject("Generated Mechanical Rooms");
        walkable.Clear();
        floorCells.Clear();
        blockedCells.Clear();
        rooms.Clear();

        if (bossArena)
        {
            AddRoom(0, 0, 18, 12, true, 3, "Isolated Boss Arena");
        }
        else
        {
            AddRoom(0, 0, 10, 8, false, 0, "Start Workshop");
            AddRoom(-13, 0, 9, 8, true, 1, "Scrap Yard");
            AddRoom(13, 0, 9, 8, true, 2, "Assembly Maze");
            AddRoom(0, 10, 8, 7, true, 2, "Cache Room");
            AddRoom(0, -10, 10, 7, true, 3, "Reactor Yard");
            AddCorridor(-6, 0, 4, 3);
            AddCorridor(6, 0, 4, 3);
            AddCorridor(0, 5, 3, 4);
            AddCorridor(0, -5, 3, 4);
        }

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

        if (bossArena)
        {
            PlaceBossArenaDressing(levelRoot.transform);
        }
        else
        {
            PlaceRoomDressing(levelRoot.transform);
            PlaceRoomFeatures(levelRoot.transform);
            PlaceFloorFeature(levelRoot.transform, -13, 0, 2.2f, 0.95f, new Color(0.18f, 0.5f, 0.52f, 0.5f));
            PlaceFloorFeature(levelRoot.transform, 0, 10, 2.6f, 1.1f, new Color(0.16f, 0.75f, 0.95f, 0.42f));
            PlaceFloorFeature(levelRoot.transform, 13, 0, 2.4f, 0.9f, new Color(0.35f, 0.1f, 0.08f, 0.42f));
            PlaceFloorFeature(levelRoot.transform, 0, -10, 2.7f, 1.15f, new Color(0.42f, 0.12f, 0.5f, 0.38f));
            PlaceSupplyStation(levelRoot.transform, -2, 0, "Repair Station");
            PlaceSupplyStation(levelRoot.transform, 0, 9, "Cooling Station");
        }
    }

    private void PlaceBossArenaDressing(Transform root)
    {
        PlaceTerminal(root, 0, 4, "BREAKER CORE");
        PlaceReinforcedBarricade(root, -4, 1, false);
        PlaceReinforcedBarricade(root, 4, 1, false);
        PlaceReinforcedBarricade(root, -3, -2, true);
        PlaceReinforcedBarricade(root, 3, -2, true);
        PlaceBarrel(root, -5, 3, true);
        PlaceBarrel(root, 5, 3, true);
        PlaceShockField(root, -4, 0, 1.15f);
        PlaceShockField(root, 4, 0, 1.15f);
        PlaceSupplyStation(root, 0, -4, "Boss Arena Repair Station");
        PlaceFloorFeature(root, 0, 0, 4.5f, 2.3f, new Color(0.55f, 0.08f, 0.12f, 0.42f));
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
        PlaceTerminal(root, 0, -13, "Reactor Core");
    }

    private void PlaceRoomFeatures(Transform root)
    {
        PlaceSkillCore(root, -15, 3, RoomSkillType.KineticOverdrive);
        PlaceSkillCore(root, 10, 3, RoomSkillType.CoolantMatrix);
        PlaceSkillCore(root, 3, 10, RoomSkillType.NaniteShell);
        PlaceSkillCore(root, -3, -10, RoomSkillType.SalvageMagnet);

        PlaceShockField(root, -13, 0, 1.25f);
        PlaceShockField(root, 13, 0, 1.15f);
        PlaceShockField(root, -3, -9, 1f);
        PlaceShockField(root, 3, -11, 1f);

        PlaceCoolantZone(root, 0, 10, 1.55f);
        PlaceDefenseTurret(root, -3, 10);

        PlaceReinforcedBarricade(root, -11, 1, false);
        PlaceReinforcedBarricade(root, 13, 2, true);
        PlaceReinforcedBarricade(root, 13, -2, true);
        PlaceReinforcedBarricade(root, -2, -8, false);
        PlaceReinforcedBarricade(root, 2, -12, false);
    }

    private void PlaceSkillCore(Transform root, int x, int y, RoomSkillType skillType)
    {
        GameObject core = new GameObject($"{skillType} Skill Core");
        core.transform.SetParent(root);
        core.transform.position = new Vector3(x, y, -0.12f);
        core.transform.localScale = Vector3.one * 1.15f;
        SpriteRenderer renderer = core.AddComponent<SpriteRenderer>();
        renderer.sprite = SkillCoreSprite;
        renderer.sortingOrder = 10;
        CircleCollider2D collider = core.AddComponent<CircleCollider2D>();
        collider.radius = 0.48f;
        collider.isTrigger = true;
        AddReadabilityMarker(core, $"{GetSkillCoreName(skillType)}\nTOUCH: {GetSkillCoreDescription(skillType)}", GetSkillCoreColor(skillType), 1.7f, 0.9f);
        core.AddComponent<SkillCorePickup>().Configure(this, skillType);
    }

    private void PlaceShockField(Transform root, int x, int y, float radius)
    {
        GameObject field = new GameObject("Unstable Shock Field");
        field.transform.SetParent(root);
        field.transform.position = new Vector3(x, y, 0.72f);
        SpriteRenderer renderer = field.AddComponent<SpriteRenderer>();
        renderer.sprite = ShockFieldSprite;
        renderer.sortingOrder = -6;
        field.AddComponent<ShockField>().Configure(this, radius);
    }

    private void PlaceCoolantZone(Transform root, int x, int y, float radius)
    {
        GameObject zone = new GameObject("Coolant Recovery Zone");
        zone.transform.SetParent(root);
        zone.transform.position = new Vector3(x, y, 0.7f);
        zone.transform.localScale = Vector3.one * radius * 2f;
        SpriteRenderer renderer = zone.AddComponent<SpriteRenderer>();
        renderer.sprite = CoolantZoneSprite;
        renderer.sortingOrder = -7;
        CircleCollider2D collider = zone.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f;
        collider.isTrigger = true;
        zone.AddComponent<CoolantZone>().Configure(this);
    }

    private void PlaceDefenseTurret(Transform root, int x, int y)
    {
        GameObject turret = new GameObject("Recovered Defense Turret");
        turret.transform.SetParent(root);
        turret.transform.position = new Vector3(x, y, -0.1f);
        turret.transform.localScale = Vector3.one * 1.6f;
        SpriteRenderer renderer = turret.AddComponent<SpriteRenderer>();
        renderer.sprite = DefenseTurretSprite;
        renderer.sortingOrder = 5;
        CircleCollider2D collider = turret.AddComponent<CircleCollider2D>();
        collider.radius = 0.34f;
        turret.AddComponent<WallMarker>();
        AddReadabilityMarker(turret, "ALLY TURRET", new Color(0.22f, 1f, 0.72f), 1.5f, 0.85f);
        turret.AddComponent<DefenseTurret>().Configure(this);
        RegisterBlockedCell(x, y);
    }

    private void PlaceReinforcedBarricade(Transform root, int x, int y, bool horizontal)
    {
        GameObject barricade = CreateBlockingProp(root, "Reinforced Barricade", x, y, 1f, propSprites[0], -1, new Vector2(0.82f, 0.42f));
        barricade.transform.localScale = new Vector3(1.9f, 0.9f, 1f);
        barricade.transform.rotation = Quaternion.Euler(0f, 0f, horizontal ? 0f : 90f);
        barricade.GetComponent<SpriteRenderer>().color = new Color(0.58f, 0.66f, 0.7f, 1f);
        AddReadabilityMarker(barricade, "BARRIER", new Color(0.35f, 0.82f, 1f), 1.8f, 0.72f);
        barricade.AddComponent<DestructibleProp>().Configure(this, 92, 4, 0.06f, false);
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
        GameObject crate = CreateBlockingProp(root, "Breakable Crate", x, y, 1.05f, CrateSprite, -2, new Vector2(0.72f, 0.72f));
        crate.GetComponent<SpriteRenderer>().color = new Color(0.82f, 0.62f, 0.3f, 1f);
        AddReadabilityMarker(crate, "CRATE", new Color(1f, 0.72f, 0.18f), 1.25f, 0.72f);
        crate.AddComponent<DestructibleProp>().Configure(this, 36, 3, 0.08f, false);
    }

    private void PlaceBarrel(Transform root, int x, int y, bool volatileBarrel)
    {
        GameObject barrel = CreateBlockingProp(root, volatileBarrel ? "Volatile Fuel Barrel" : "Scrap Barrel", x, y, 1.08f, BarrelSprite, -2, new Vector2(0.58f, 0.58f));
        barrel.GetComponent<SpriteRenderer>().color = volatileBarrel
            ? new Color(0.86f, 0.38f, 0.28f, 1f)
            : new Color(0.66f, 0.58f, 0.46f, 1f);
        AddReadabilityMarker(
            barrel,
            volatileBarrel ? "EXPLOSIVE" : "SCRAP BARREL",
            volatileBarrel ? new Color(1f, 0.2f, 0.08f) : new Color(0.95f, 0.7f, 0.3f),
            1.3f,
            0.74f);
        barrel.AddComponent<DestructibleProp>().Configure(this, volatileBarrel ? 28 : 34, volatileBarrel ? 4 : 2, volatileBarrel ? 0.14f : 0.04f, volatileBarrel);
    }

    private void PlaceTerminal(Transform root, int x, int y, string terminalName)
    {
        GameObject terminal = CreateBlockingProp(root, terminalName, x, y, 1.28f, TerminalSprite, -2, new Vector2(0.56f, 0.56f));
        terminal.GetComponent<SpriteRenderer>().color = new Color(0.42f, 0.74f, 0.68f, 1f);
        AddReadabilityMarker(terminal, "TERMINAL", new Color(0.18f, 1f, 0.82f), 1.4f, 0.82f);
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
        GameObject machinery = CreateBlockingProp(root, "Scrap Machinery", x, y, scale * 1.05f, propSprites[Random.Range(0, propSprites.Length)], -2, Vector2.one * 0.58f);
        machinery.GetComponent<SpriteRenderer>().color = new Color(0.58f, 0.62f, 0.66f, 1f);
        AddReadabilityMarker(machinery, "SOLID MACHINE", new Color(0.7f, 0.84f, 1f), 1.35f, 0.78f);
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
        renderer.color = stationName.Contains("Cooling")
            ? new Color(0.42f, 0.68f, 0.76f, 0.92f)
            : new Color(0.42f, 0.72f, 0.5f, 0.92f);
        renderer.sortingOrder = 2;
        CircleCollider2D collider = station.AddComponent<CircleCollider2D>();
        collider.radius = 0.38f;
        collider.isTrigger = true;
        AddReadabilityMarker(
            station,
            stationName.Contains("Cooling") ? "COOLING STATION" : "REPAIR STATION",
            stationName.Contains("Cooling") ? new Color(0.25f, 0.85f, 1f) : new Color(0.3f, 1f, 0.48f),
            1.65f,
            0.86f);
        station.AddComponent<SupplyStation>().Configure(this, stationName.Contains("Cooling"));
    }

    private void AddReadabilityMarker(GameObject target, string label, Color color, float plateScale, float labelHeight)
    {
        GameObject labelObject = new GameObject("World Label");
        labelObject.transform.SetParent(target.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, labelHeight, -0.32f);
        TextMesh text = labelObject.AddComponent<TextMesh>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.characterSize = 0.045f;
        text.anchor = TextAnchor.LowerCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        MeshRenderer mesh = labelObject.GetComponent<MeshRenderer>();
        mesh.sharedMaterial = text.font.material;
        mesh.sortingOrder = 24;
        GameObject labelBack = new GameObject("Label Backplate");
        labelBack.transform.SetParent(labelObject.transform, false);
        labelBack.transform.localPosition = new Vector3(0f, 0.18f, 0.12f);
        int longestLine = 0;
        foreach (string line in label.Split('\n'))
        {
            longestLine = Mathf.Max(longestLine, line.Length);
        }

        labelBack.transform.localScale = new Vector3(Mathf.Clamp(longestLine * 0.058f, 0.72f, 2.65f), label.Contains("\n") ? 0.5f : 0.3f, 1f);
        SpriteRenderer labelBackRenderer = labelBack.AddComponent<SpriteRenderer>();
        labelBackRenderer.sprite = SpriteFactory.Square(new Color(0.01f, 0.015f, 0.02f, 0.78f), new Color(color.r, color.g, color.b, 0.5f));
        labelBackRenderer.sortingOrder = 23;
        float revealDistance = label.Contains("\n")
            ? 3f
            : label.Contains("STATION") || label.Contains("TURRET") ? 2f : 1.45f;
        labelObject.AddComponent<WorldObjectLabel>().Configure(0.58f, revealDistance);

        Collider2D targetCollider = target.GetComponent<Collider2D>();
        if (targetCollider != null && !targetCollider.isTrigger)
        {
            target.AddComponent<ObstacleIdentity>().Configure(this, label.Split('\n')[0]);
        }
    }

    private static string GetSkillCoreName(RoomSkillType skillType)
    {
        switch (skillType)
        {
            case RoomSkillType.KineticOverdrive:
                return "OVERDRIVE CORE";
            case RoomSkillType.CoolantMatrix:
                return "COOLANT CORE";
            case RoomSkillType.NaniteShell:
                return "NANITE CORE";
            default:
                return "MAGNET CORE";
        }
    }

    private static Color GetSkillCoreColor(RoomSkillType skillType)
    {
        switch (skillType)
        {
            case RoomSkillType.KineticOverdrive:
                return new Color(1f, 0.55f, 0.16f);
            case RoomSkillType.CoolantMatrix:
                return new Color(0.22f, 0.88f, 1f);
            case RoomSkillType.NaniteShell:
                return new Color(0.38f, 1f, 0.48f);
            default:
                return new Color(0.88f, 0.48f, 1f);
        }
    }

    private static string GetSkillCoreDescription(RoomSkillType skillType)
    {
        switch (skillType)
        {
            case RoomSkillType.KineticOverdrive:
                return "SPEED + FIRE RATE";
            case RoomSkillType.CoolantMatrix:
                return "LESS WEAPON HEAT";
            case RoomSkillType.NaniteShell:
                return "MAX ARMOR +25";
            default:
                return "LARGER SCRAP MAGNET";
        }
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

        existing.SetActive(false);
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
        bodyTransform.localScale = Vector3.one * (UsingCandidateArt ? 1.55f : 3.85f);

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

public enum RoomSkillType
{
    KineticOverdrive,
    CoolantMatrix,
    NaniteShell,
    SalvageMagnet
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
    public WeaponStats CurrentStats => equippedStats;
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
            weaponRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            ApplyWeaponVisualTransform();
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

        ApplyWeaponVisualTransform();

        Transform existingMuzzle = weaponPivot.Find("Muzzle");
        GameObject muzzleObject = existingMuzzle != null ? existingMuzzle.gameObject : new GameObject("Muzzle");
        muzzleObject.transform.SetParent(weaponPivot, false);
        muzzlePoint = muzzleObject.transform;
        muzzlePoint.localPosition = new Vector3(IsPreparedWeapon(weaponRenderer.sprite) ? 0.92f : 1.18f, 0f, -0.12f);
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

    private void ApplyWeaponVisualTransform()
    {
        if (weaponRenderer == null)
        {
            return;
        }

        bool preparedWeapon = IsPreparedWeapon(weaponRenderer.sprite);
        float scale = preparedWeapon ? 0.82f : 4.8f;
        weaponRenderer.transform.localPosition = new Vector3(preparedWeapon ? 0.42f : 0.38f, 0f, -0.05f);
        weaponRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        if (muzzlePoint != null)
        {
            muzzlePoint.localPosition = new Vector3(preparedWeapon ? 0.92f : 1.18f, 0f, -0.12f);
        }
    }

    private bool IsPreparedWeapon(Sprite sprite)
    {
        return sprite != null && sprite.rect.width >= 48f;
    }

    public void TryFire(Vector2 direction)
    {
        if (Time.time < nextFireTime || player.IsDead)
        {
            return;
        }

        nextFireTime = Time.time + FireDelay * player.FireDelayMultiplier;
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

        player.NotifyWeaponFired();
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

    public int VolleyDamage => Damage * Mathf.Max(1, PelletCount);
    public float ShotsPerSecond => FireDelay > 0f ? 1f / FireDelay : 0f;
    public float EffectiveRange => BulletSpeed * BulletLifetime;

    public string ProjectileDescription()
    {
        if (PelletCount > 1)
        {
            return $"{PelletCount} pellets / {SpreadDegrees:0.#} deg spread / {EffectiveRange:0.#} range";
        }

        return SpreadDegrees >= 5f
            ? $"single unstable shot / {SpreadDegrees:0.#} deg spread / {EffectiveRange:0.#} range"
            : $"single precision shot / {SpreadDegrees:0.#} deg spread / {EffectiveRange:0.#} range";
    }
}

public enum EnemyKind
{
    Chaser,
    Drone,
    Support,
    Bulwark,
    Artillery,
    Boss,
    SiegeBoss,
    ReactorBoss
}

public class EnemyController : MonoBehaviour
{
    private const float PlayerRoomEntryMargin = -0.45f;
    private const float EnemyRoomLeashMargin = -0.05f;

    private GameDirector director;
    private PlayerController player;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
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
    private float abilityTimer;
    private float chargeTimer;
    private float environmentSlowTimer;
    private float environmentSlowMultiplier = 1f;
    private int attackPattern;
    private Vector2 spawnPosition;
    private Vector2 homeCenter;
    private int homeWidth = 10;
    private int homeHeight = 8;
    private string homeName = "Combat Room";
    private Color baseTint = Color.white;
    private bool active;
    private bool awakened;
    private bool dying;
    private float hitFlashTimer;
    private Vector3 visualBasePosition;
    private Vector3 visualBaseScale = Vector3.one;

    public bool IsAwake => active && awakened;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        EnsureVisuals();
    }

    public void Configure(GameDirector owner, PlayerController target, EnemyKind enemyKind, Vector2 position)
    {
        director = owner;
        player = target;
        kind = enemyKind;
        StopAllCoroutines();
        EnsureVisuals();
        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = true;
        }

        transform.position = new Vector3(position.x, position.y, 0f);
        transform.localScale = Vector3.one;
        spawnPosition = position;
        body.velocity = Vector2.zero;
        active = true;
        awakened = false;
        dying = false;
        hitFlashTimer = 0f;
        contactTimer = 0.25f;
        shootTimer = Random.Range(0.45f, 1.1f);
        supportPulseTimer = Random.Range(0f, 1f);
        abilityTimer = Random.Range(1.5f, 2.5f);
        chargeTimer = 0f;
        environmentSlowTimer = 0f;
        environmentSlowMultiplier = 1f;
        attackPattern = 0;

        switch (kind)
        {
            case EnemyKind.Chaser:
                health = maxHealth = 64;
                speed = 3.35f;
                spriteRenderer.sprite = director.ChaserSprite;
                spriteRenderer.transform.localScale = Vector3.one * (director.UsingCandidateArt ? 1.35f : 2.75f);
                baseTint = new Color(1f, 0.92f, 0.9f, 1f);
                healthBarWidth = 0.95f;
                break;
            case EnemyKind.Drone:
                health = maxHealth = 46;
                speed = 2.25f;
                spriteRenderer.sprite = director.DroneSprite;
                spriteRenderer.transform.localScale = Vector3.one * (director.UsingCandidateArt ? 1.25f : 2.55f);
                baseTint = new Color(0.95f, 0.98f, 1f, 1f);
                healthBarWidth = 0.9f;
                break;
            case EnemyKind.Support:
                health = maxHealth = 82;
                speed = 1.75f;
                spriteRenderer.sprite = director.SupportSprite;
                spriteRenderer.transform.localScale = Vector3.one * (director.UsingCandidateArt ? 1.4f : 2.65f);
                baseTint = new Color(0.88f, 1f, 0.9f, 1f);
                healthBarWidth = 1.05f;
                break;
            case EnemyKind.Bulwark:
                health = maxHealth = 180;
                speed = 1.35f;
                spriteRenderer.sprite = director.BulwarkSprite;
                spriteRenderer.transform.localScale = Vector3.one * (director.UsingCandidateArt ? 1.65f : 3.1f);
                baseTint = new Color(0.78f, 0.88f, 1f, 1f);
                healthBarWidth = 1.3f;
                break;
            case EnemyKind.Artillery:
                health = maxHealth = 58;
                speed = 1.55f;
                spriteRenderer.sprite = director.ArtillerySprite;
                spriteRenderer.transform.localScale = Vector3.one * (director.UsingCandidateArt ? 1.3f : 2.7f);
                baseTint = new Color(0.96f, 0.82f, 1f, 1f);
                healthBarWidth = 1f;
                break;
            case EnemyKind.Boss:
                health = maxHealth = 620;
                speed = 1.75f;
                spriteRenderer.sprite = director.BossSprite;
                spriteRenderer.transform.localScale = Vector3.one * (director.UsingCandidateArt ? 2.2f : 3.7f);
                baseTint = new Color(1f, 0.88f, 0.86f, 1f);
                healthBarWidth = 1.8f;
                break;
            case EnemyKind.SiegeBoss:
                health = maxHealth = 780;
                speed = 1.3f;
                spriteRenderer.sprite = director.BulwarkSprite;
                spriteRenderer.transform.localScale = Vector3.one * (director.UsingCandidateArt ? 2.35f : 4.25f);
                baseTint = new Color(0.58f, 0.76f, 1f, 1f);
                healthBarWidth = 2.2f;
                break;
            case EnemyKind.ReactorBoss:
                health = maxHealth = 540;
                speed = 2.1f;
                spriteRenderer.sprite = director.ArtillerySprite;
                spriteRenderer.transform.localScale = Vector3.one * (director.UsingCandidateArt ? 2.15f : 3.9f);
                baseTint = new Color(1f, 0.5f, 0.92f, 1f);
                healthBarWidth = 1.9f;
                break;
            default:
                health = maxHealth = 64;
                speed = 3.35f;
                spriteRenderer.sprite = director.ChaserSprite;
                spriteRenderer.transform.localScale = Vector3.one * (director.UsingCandidateArt ? 1.35f : 2.75f);
                baseTint = Color.white;
                healthBarWidth = 0.95f;
                break;
        }

        gameObject.name = GetDisplayName();
        spriteRenderer.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        visualBasePosition = spriteRenderer.transform.localPosition;
        visualBaseScale = spriteRenderer.transform.localScale;
        spriteRenderer.transform.localRotation = Quaternion.identity;
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
        if (!active || player == null || player.IsDead || Time.timeScale <= 0f)
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
        if (environmentSlowTimer > 0f)
        {
            environmentSlowTimer = Mathf.Max(0f, environmentSlowTimer - Time.fixedDeltaTime);
            currentSpeed *= environmentSlowMultiplier;
        }

        if (IsBossKind() && health <= maxHealth / 2)
        {
            currentSpeed *= kind == EnemyKind.SiegeBoss ? 1.15f : 1.25f;
        }

        if ((kind == EnemyKind.Bulwark || kind == EnemyKind.SiegeBoss) && chargeTimer > 0f && playerInsideHome)
        {
            body.velocity = direction * currentSpeed * (kind == EnemyKind.SiegeBoss ? 4.4f : 3.8f);
        }
        else if (!playerInsideHome && distance < 0.45f)
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
        else if (kind == EnemyKind.Support && playerInsideHome && distance < 5f)
        {
            body.velocity = Vector2.zero;
        }
        else if (kind == EnemyKind.Artillery && playerInsideHome && distance < 5.8f)
        {
            body.velocity = -direction * currentSpeed;
        }
        else if (kind == EnemyKind.Artillery && playerInsideHome && distance < 7.8f)
        {
            body.velocity = Vector2.zero;
        }
        else if (kind == EnemyKind.ReactorBoss && playerInsideHome && distance < 5.2f)
        {
            body.velocity = -direction * currentSpeed * 0.8f;
        }
        else if (kind == EnemyKind.ReactorBoss && playerInsideHome && distance < 7.2f)
        {
            Vector2 strafe = new Vector2(-direction.y, direction.x);
            body.velocity = strafe * currentSpeed * 0.75f;
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
        if (!active || player == null || player.IsDead || Time.timeScale <= 0f)
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
        abilityTimer -= Time.deltaTime;
        chargeTimer -= Time.deltaTime;

        bool playerInsideHome = IsInsideHomeRoom(player.transform.position, PlayerRoomEntryMargin);
        if (playerInsideHome)
        {
            if (kind == EnemyKind.Drone || kind == EnemyKind.Artillery || IsBossKind())
            {
                shootTimer -= Time.deltaTime;
                if (shootTimer <= 0f)
                {
                    if (kind == EnemyKind.Drone)
                    {
                        FirePrecisionShot();
                        shootTimer = 1.35f;
                    }
                    else if (kind == EnemyKind.Artillery)
                    {
                        FireArtilleryBarrage();
                        shootTimer = 2.45f;
                    }
                    else if (kind == EnemyKind.Boss)
                    {
                        FireBossPattern();
                        shootTimer = health <= maxHealth / 2 ? 0.72f : 1.05f;
                    }
                    else if (kind == EnemyKind.SiegeBoss)
                    {
                        FireSiegeBossPattern();
                        shootTimer = health <= maxHealth / 2 ? 0.9f : 1.35f;
                    }
                    else
                    {
                        FireReactorBossPattern();
                        shootTimer = health <= maxHealth / 2 ? 0.26f : 0.38f;
                    }
                }
            }

            if (kind == EnemyKind.Support && abilityTimer <= 0f)
            {
                RepairNearbyAllies();
                abilityTimer = 3.2f;
            }

            if (kind == EnemyKind.Bulwark && abilityTimer <= 0f && Vector2.Distance(player.transform.position, transform.position) <= 6f)
            {
                chargeTimer = 0.65f;
                abilityTimer = 3.8f;
            }

            if (kind == EnemyKind.SiegeBoss && abilityTimer <= 0f && Vector2.Distance(player.transform.position, transform.position) <= 9f)
            {
                chargeTimer = health <= maxHealth / 2 ? 0.85f : 0.7f;
                abilityTimer = health <= maxHealth / 2 ? 2.4f : 3.2f;
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
        else if ((kind == EnemyKind.Bulwark || kind == EnemyKind.SiegeBoss) && chargeTimer > 0f)
        {
            spriteRenderer.color = Color.Lerp(baseTint, Color.white, 0.55f);
        }
        else
        {
            float chasePulse = 0.9f + Mathf.Sin(Time.time * 6f) * 0.08f;
            spriteRenderer.color = IsSupported()
                ? Color.Lerp(baseTint, new Color(0.65f, 1f, 0.65f, 1f), 0.45f)
                : new Color(baseTint.r * chasePulse, baseTint.g * chasePulse, baseTint.b * chasePulse, 1f);
        }

        AnimateCombatVisuals(playerInsideHome);
    }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (!active)
        {
            return;
        }

        WakeUp();
        float knockbackResistance = 1f;
        if (kind == EnemyKind.Bulwark)
        {
            amount = Mathf.Max(1, Mathf.CeilToInt(amount * 0.48f));
            knockbackResistance = 0.2f;
        }
        else if (IsBossKind())
        {
            knockbackResistance = kind == EnemyKind.SiegeBoss ? 0.12f : 0.35f;
        }

        health -= amount;
        hitFlashTimer = 0.16f;
        body.AddForce(knockback * knockbackResistance, ForceMode2D.Impulse);
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
        float barHeight = IsBossKind() ? 0.1f : 0.075f;
        healthBackRenderer.transform.localScale = new Vector3(healthBarWidth, barHeight, 1f);
        healthFillRenderer.transform.localScale = new Vector3(Mathf.Max(0.04f, healthBarWidth * healthRatio), barHeight * 0.62f, 1f);
        healthFillRenderer.transform.localPosition = new Vector3(-(healthBarWidth - healthBarWidth * healthRatio) * 0.5f, 0.94f, -0.09f);
    }

    public bool RestoreHealth(int amount)
    {
        if (!active || health >= maxHealth)
        {
            return false;
        }

        health = Mathf.Min(maxHealth, health + amount);
        RefreshHealthBar();
        spriteRenderer.color = Color.Lerp(baseTint, Color.green, 0.45f);
        return true;
    }

    public void ApplyEnvironmentalSlow(float duration, float multiplier)
    {
        if (!active)
        {
            return;
        }

        environmentSlowTimer = Mathf.Max(environmentSlowTimer, duration);
        environmentSlowMultiplier = Mathf.Clamp(multiplier, 0.25f, 1f);
    }

    private void FirePrecisionShot()
    {
        Vector2 direction = (player.transform.position - transform.position).normalized;
        FireEnemyBullet(direction, 8.8f, 9, 2.1f);
    }

    private void AnimateCombatVisuals(bool playerInsideHome)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.deltaTime);
        float pulse = 0f;
        Vector3 scaleMultiplier = Vector3.one;
        float rotation = 0f;
        Color targetColor = spriteRenderer.color;
        float playerDistance = player != null ? Vector2.Distance(transform.position, player.transform.position) : float.MaxValue;

        if (kind == EnemyKind.Chaser && playerInsideHome && playerDistance <= 2.2f && contactTimer <= 0.34f)
        {
            pulse = 0.5f + Mathf.Sin(Time.time * 22f) * 0.5f;
            scaleMultiplier = new Vector3(1.12f, 0.88f, 1f);
            rotation = spriteRenderer.flipX ? 7f : -7f;
            targetColor = Color.Lerp(baseTint, new Color(1f, 0.28f, 0.18f, 1f), 0.45f + pulse * 0.25f);
        }
        else if (kind == EnemyKind.Drone && playerInsideHome && shootTimer <= 0.3f)
        {
            pulse = Mathf.Clamp01(1f - shootTimer / 0.3f);
            scaleMultiplier = new Vector3(0.9f, 1.12f + pulse * 0.08f, 1f);
            targetColor = Color.Lerp(baseTint, new Color(1f, 0.72f, 0.18f, 1f), pulse);
        }
        else if (kind == EnemyKind.Support && playerInsideHome && abilityTimer <= 0.5f)
        {
            pulse = Mathf.Clamp01(1f - abilityTimer / 0.5f);
            float supportScale = 1f + pulse * 0.18f;
            scaleMultiplier = new Vector3(supportScale, supportScale, 1f);
            rotation = Mathf.Sin(Time.time * 18f) * 3f;
            targetColor = Color.Lerp(baseTint, new Color(0.35f, 1f, 0.55f, 1f), pulse);
        }
        else if ((kind == EnemyKind.Bulwark || kind == EnemyKind.SiegeBoss) && chargeTimer > 0f)
        {
            scaleMultiplier = new Vector3(1.16f, 0.86f, 1f);
            targetColor = Color.Lerp(baseTint, Color.white, 0.65f);
        }
        else if ((kind == EnemyKind.Artillery || IsBossKind()) && playerInsideHome && shootTimer <= 0.28f)
        {
            pulse = Mathf.Clamp01(1f - shootTimer / 0.28f);
            scaleMultiplier = new Vector3(0.92f, 1.08f + pulse * 0.08f, 1f);
            targetColor = Color.Lerp(baseTint, Color.white, pulse * 0.75f);
        }

        if (hitFlashTimer > 0f)
        {
            float hit = hitFlashTimer / 0.16f;
            scaleMultiplier = new Vector3(1f + hit * 0.13f, 1f - hit * 0.08f, 1f);
            rotation += Mathf.Sin(hit * Mathf.PI) * (spriteRenderer.flipX ? -7f : 7f);
            targetColor = Color.Lerp(baseTint, Color.white, hit);
        }

        Transform visual = spriteRenderer.transform;
        Vector3 targetScale = Vector3.Scale(visualBaseScale, scaleMultiplier);
        Vector3 targetPosition = visualBasePosition + new Vector3(0f, pulse * 0.035f, 0f);
        visual.localScale = Vector3.Lerp(visual.localScale, targetScale, Time.deltaTime * 20f);
        visual.localPosition = Vector3.Lerp(visual.localPosition, targetPosition, Time.deltaTime * 20f);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(0f, 0f, rotation), Time.deltaTime * 20f);
        spriteRenderer.color = Color.Lerp(spriteRenderer.color, targetColor, Time.deltaTime * 24f);
    }

    private void FireArtilleryBarrage()
    {
        Vector2 direction = (player.transform.position - transform.position).normalized;
        for (int angle = -32; angle <= 32; angle += 16)
        {
            Vector2 shotDirection = Quaternion.Euler(0f, 0f, angle) * direction;
            FireEnemyBullet(shotDirection, 5.4f, 6, 2.7f);
        }
    }

    private void FireBossPattern()
    {
        Vector2 direction = (player.transform.position - transform.position).normalized;
        bool secondPhase = health <= maxHealth / 2;
        if (attackPattern % 2 == 0)
        {
            FireEnemyBullet(direction, secondPhase ? 10.5f : 9f, 13, 2.25f);
            for (int angle = -24; angle <= 24; angle += 48)
            {
                Vector2 side = Quaternion.Euler(0f, 0f, angle) * direction;
                FireEnemyBullet(side, 8f, 9, 2.3f);
            }
        }
        else
        {
            int projectileCount = secondPhase ? 12 : 8;
            float angleOffset = secondPhase ? Time.time * 50f : 0f;
            for (int i = 0; i < projectileCount; i++)
            {
                float angle = angleOffset + i * (360f / projectileCount);
                Vector2 radialDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                FireEnemyBullet(radialDirection, secondPhase ? 7.2f : 6.2f, 7, 2.5f);
            }
        }

        attackPattern++;
    }

    private void FireSiegeBossPattern()
    {
        Vector2 direction = (player.transform.position - transform.position).normalized;
        bool secondPhase = health <= maxHealth / 2;
        if (attackPattern % 2 == 0)
        {
            for (int angle = -36; angle <= 36; angle += 18)
            {
                Vector2 shotDirection = Quaternion.Euler(0f, 0f, angle) * direction;
                FireEnemyBullet(shotDirection, secondPhase ? 8.8f : 7.6f, secondPhase ? 12 : 10, 2.8f);
            }
        }
        else
        {
            int projectileCount = secondPhase ? 16 : 12;
            for (int i = 0; i < projectileCount; i++)
            {
                float angle = i * (360f / projectileCount);
                Vector2 radialDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                FireEnemyBullet(radialDirection, 5.5f, 9, 3.2f);
            }
        }

        attackPattern++;
    }

    private void FireReactorBossPattern()
    {
        bool secondPhase = health <= maxHealth / 2;
        int arms = secondPhase ? 4 : 2;
        float angleOffset = Time.time * (secondPhase ? 150f : 105f) + attackPattern * 17f;
        for (int i = 0; i < arms; i++)
        {
            float angle = angleOffset + i * (360f / arms);
            Vector2 spiralDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            FireEnemyBullet(spiralDirection, secondPhase ? 8.2f : 7.2f, 7, 3f);
        }

        if (attackPattern % (secondPhase ? 5 : 7) == 0)
        {
            Vector2 aimed = (player.transform.position - transform.position).normalized;
            for (int angle = -18; angle <= 18; angle += 18)
            {
                FireEnemyBullet(Quaternion.Euler(0f, 0f, angle) * aimed, 10.5f, 10, 2.4f);
            }
        }

        attackPattern++;
    }

    private void FireEnemyBullet(Vector2 direction, float bulletSpeed, int damage, float lifetime)
    {
        Vector2 normalized = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.right;
        Bullet bullet = director.GetEnemyBullet();
        bullet.Fire((Vector2)transform.position + normalized * 0.55f, normalized, bulletSpeed, damage, lifetime);
    }

    private void RepairNearbyAllies()
    {
        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        int repaired = 0;
        foreach (EnemyController enemy in enemies)
        {
            if (enemy != this && enemy.gameObject.activeInHierarchy && Vector2.Distance(transform.position, enemy.transform.position) <= 4.5f)
            {
                if (enemy.RestoreHealth(14))
                {
                    repaired++;
                }
            }
        }

        if (repaired > 0)
        {
            director?.ShowStatus($"Support repair pulse restored {repaired} machines", 0.8f);
        }
    }

    private void TryWakeUp()
    {
        if (player == null)
        {
            return;
        }

        if (IsInsideHomeRoom(player.transform.position, PlayerRoomEntryMargin))
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
        director?.ShowStatus($"{GetDisplayName()} awakened in {homeName}", 0.9f);
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

    private bool IsBossKind()
    {
        return kind == EnemyKind.Boss || kind == EnemyKind.SiegeBoss || kind == EnemyKind.ReactorBoss;
    }

    private string GetDisplayName()
    {
        switch (kind)
        {
            case EnemyKind.Boss:
                return "Breaker Boss";
            case EnemyKind.SiegeBoss:
                return "Siege Titan";
            case EnemyKind.ReactorBoss:
                return "Reactor Warden";
            case EnemyKind.Chaser:
                return "Ripper Chaser";
            case EnemyKind.Drone:
                return "Hornet Drone";
            case EnemyKind.Support:
                return "Scarab Support";
            case EnemyKind.Bulwark:
                return "Centipede Bulwark";
            case EnemyKind.Artillery:
                return "Wasp Artillery";
            default:
                return "Hostile Machine";
        }
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
            int contactDamage = kind == EnemyKind.SiegeBoss ? 30
                : kind == EnemyKind.ReactorBoss ? 18
                : kind == EnemyKind.Boss ? 24
                : kind == EnemyKind.Bulwark ? 22
                : kind == EnemyKind.Chaser ? 13
                : 8;
            hitPlayer.TakeDamage(contactDamage);
            contactTimer = kind == EnemyKind.Bulwark ? 1.15f : kind == EnemyKind.Chaser ? 0.55f : 0.8f;
        }
    }

    private void Die()
    {
        if (dying)
        {
            return;
        }

        dying = true;
        active = false;
        body.velocity = Vector2.zero;
        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

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

        StartCoroutine(DeathRoutine(deathPosition));
    }

    private IEnumerator DeathRoutine(Vector2 deathPosition)
    {
        Vector3 startScale = spriteRenderer != null ? spriteRenderer.transform.localScale : Vector3.one;
        float elapsed = 0f;
        const float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localScale = Vector3.Lerp(startScale * 1.18f, startScale * 0.18f, t);
                spriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, spriteRenderer.flipX ? -85f : 85f, t));
                spriteRenderer.color = Color.Lerp(Color.white, new Color(baseTint.r, baseTint.g * 0.25f, baseTint.b * 0.18f, 0f), t);
            }

            yield return null;
        }

        if (shadowRenderer != null)
        {
            shadowRenderer.enabled = false;
        }

        int scrapValue = IsBossKind() ? 12 : Mathf.Max(2, maxHealth / 24);
        for (int i = 0; i < scrapValue; i++)
        {
            director.SpawnScrap(deathPosition, 1);
        }

        director.RewardEnemyDefeat(kind, deathPosition);
        director.TrySpawnWeaponDrop(deathPosition, IsBossKind() ? 1f : 0.18f);
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
    private float displayScale = 4.25f;

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
        displayScale = stats.Sprite != null && stats.Sprite.rect.width >= 48f ? 1.35f : 4.25f;
        transform.localScale = Vector3.one * displayScale;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        float bob = Mathf.Sin((Time.time + bobSeed) * 2.8f) * 0.08f;
        transform.localScale = Vector3.one * (displayScale + bob);
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

public class SkillCorePickup : MonoBehaviour
{
    private GameDirector director;
    private RoomSkillType skillType;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer glowRenderer;
    private float bobSeed;
    private bool collected;

    public void Configure(GameDirector owner, RoomSkillType type)
    {
        director = owner;
        skillType = type;
        spriteRenderer = GetComponent<SpriteRenderer>();
        bobSeed = Random.Range(0f, 8f);

        Color skillColor = GetSkillColor(type);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = skillColor;
        }

        GameObject glow = new GameObject("Skill Core Glow");
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        glow.transform.localScale = Vector3.one * 1.65f;
        glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = SpriteFactory.Circle(new Color(skillColor.r, skillColor.g, skillColor.b, 0.12f), Color.clear);
        glowRenderer.sortingOrder = 7;
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin((Time.time + bobSeed) * 4f) * 0.12f;
        transform.localScale = Vector3.one * 1.15f * pulse;
        transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 42f + bobSeed * 12f);
        if (glowRenderer != null)
        {
            glowRenderer.transform.localScale = Vector3.one * (1.35f + Mathf.Sin((Time.time + bobSeed) * 3f) * 0.1f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (collected || player == null)
        {
            return;
        }

        collected = true;
        director.PlayPurgeEffect(transform.position, 1.25f);
        player.ApplyRoomSkill(skillType);
        Destroy(gameObject);
    }

    private static Color GetSkillColor(RoomSkillType type)
    {
        switch (type)
        {
            case RoomSkillType.KineticOverdrive:
                return new Color(1f, 0.55f, 0.16f, 1f);
            case RoomSkillType.CoolantMatrix:
                return new Color(0.22f, 0.88f, 1f, 1f);
            case RoomSkillType.NaniteShell:
                return new Color(0.38f, 1f, 0.48f, 1f);
            default:
                return new Color(0.88f, 0.48f, 1f, 1f);
        }
    }
}

public class ShockField : MonoBehaviour
{
    private GameDirector director;
    private SpriteRenderer spriteRenderer;
    private float radius;
    private float nextPulse;

    public void Configure(GameDirector owner, float fieldRadius)
    {
        director = owner;
        radius = fieldRadius;
        spriteRenderer = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.one * radius * 2f;
        nextPulse = Time.time + Random.Range(0.5f, 1.2f);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            return;
        }

        float warningPulse = 0.9f + Mathf.Sin(Time.time * 7f) * 0.1f;
        transform.localScale = Vector3.one * radius * 2f * warningPulse;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(0.82f, 0.24f + warningPulse * 0.08f, 0.06f, 0.24f);
        }

        if (Time.time < nextPulse)
        {
            return;
        }

        nextPulse = Time.time + 1.65f;
        Pulse();
    }

    private void Pulse()
    {
        bool hitPlayer = false;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (Collider2D hit in hits)
        {
            EnemyController enemy = hit.GetComponent<EnemyController>();
            if (enemy != null)
            {
                if (!enemy.IsAwake)
                {
                    continue;
                }

                Vector2 offset = enemy.transform.position - transform.position;
                enemy.TakeDamage(16, offset.normalized * 2.4f);
                continue;
            }

            PlayerController player = hit.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(7);
                hitPlayer = true;
            }
        }

        StartCoroutine(FlashPulse());
        if (hitPlayer)
        {
            director.ShowStatus("Unstable field discharged: lure machines into the next pulse", 1.15f);
        }
    }

    private IEnumerator FlashPulse()
    {
        if (spriteRenderer == null)
        {
            yield break;
        }

        spriteRenderer.color = new Color(1f, 0.72f, 0.28f, 0.58f);
        yield return new WaitForSeconds(0.12f);
    }
}

public class CoolantZone : MonoBehaviour
{
    private GameDirector director;
    private SpriteRenderer spriteRenderer;
    private float nextStatusTime;

    public void Configure(GameDirector owner)
    {
        director = owner;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (spriteRenderer != null)
        {
            float pulse = 0.14f + Mathf.Sin(Time.time * 3.5f) * 0.035f;
            spriteRenderer.color = new Color(0.12f, 0.62f, 0.78f, pulse);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.CoolWeapon(32f * Time.fixedDeltaTime);
            if (Time.time >= nextStatusTime)
            {
                nextStatusTime = Time.time + 2.4f;
                director.ShowStatus("Coolant pool: weapon heat venting rapidly", 0.9f);
            }

            return;
        }

        EnemyController enemy = other.GetComponent<EnemyController>();
        enemy?.ApplyEnvironmentalSlow(0.25f, 0.52f);
    }
}

public class DefenseTurret : MonoBehaviour
{
    private GameDirector director;
    private PlayerController player;
    private SpriteRenderer spriteRenderer;
    private float nextShotTime;

    public void Configure(GameDirector owner)
    {
        director = owner;
        spriteRenderer = GetComponent<SpriteRenderer>();
        nextShotTime = Time.time + 0.8f;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            return;
        }

        if (player == null)
        {
            player = FindObjectOfType<PlayerController>();
        }

        if (player == null || Vector2.Distance(transform.position, player.transform.position) > 5.6f)
        {
            SetIdleColor();
            return;
        }

        EnemyController target = FindTarget();
        if (target == null)
        {
            SetIdleColor();
            return;
        }

        Vector2 direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 45f);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(new Color(0.28f, 0.92f, 0.76f), Color.white, 0.22f);
        }

        if (Time.time < nextShotTime)
        {
            return;
        }

        nextShotTime = Time.time + 0.72f;
        Bullet bullet = director.GetPlayerBullet();
        bullet.Fire((Vector2)transform.position + direction * 0.85f, direction, 13f, 13, 1.3f);
    }

    private void SetIdleColor()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(0.55f, 0.72f, 0.7f, 1f);
        }
    }

    private EnemyController FindTarget()
    {
        EnemyController best = null;
        float bestDistance = 6.2f;
        foreach (EnemyController enemy in FindObjectsOfType<EnemyController>())
        {
            if (!enemy.gameObject.activeInHierarchy || !enemy.IsAwake)
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
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
    private readonly Image healthFill;
    private readonly Image armorFill;
    private readonly Image heatFill;
    private readonly Image damageOverlay;
    private readonly Text statusText;
    private readonly Text healthText;
    private readonly Text armorText;
    private readonly Text heatText;
    private readonly Text resourceText;
    private readonly Text progressText;
    private readonly GameObject weaponComparePanel;
    private readonly Text weaponCompareText;
    private float damageFlash;

    private GameHud(
        PlayerController playerController,
        GameDirector owner,
        Image health,
        Image armor,
        Image heat,
        Image damage,
        Text status,
        Text healthLabel,
        Text armorLabel,
        Text heatLabel,
        Text resources,
        Text progress,
        GameObject comparisonPanel,
        Text comparisonText)
    {
        player = playerController;
        director = owner;
        healthFill = health;
        armorFill = armor;
        heatFill = heat;
        damageOverlay = damage;
        statusText = status;
        healthText = healthLabel;
        armorText = armorLabel;
        heatText = heatLabel;
        resourceText = resources;
        progressText = progress;
        weaponComparePanel = comparisonPanel;
        weaponCompareText = comparisonText;
    }

    public static GameHud Create(PlayerController player, GameDirector owner)
    {
        GameObject canvasObject = new GameObject("HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Image damage = CreatePanel(canvasObject.transform, "Damage Flash", Anchor.Stretch, Vector2.zero, new Vector2(0f, 0f), new Color(1f, 0f, 0f, 0f));

        Image vitalsPanel = CreatePanel(canvasObject.transform, "Player Vitals Panel", Anchor.TopLeft, new Vector2(18f, -18f), new Vector2(410f, 238f), new Color(0.015f, 0.025f, 0.03f, 0.93f));
        Text title = CreateText(vitalsPanel.transform, "Title", "SCAVENGER STATUS", 24, TextAnchor.UpperLeft);
        PlaceTopLeft(title.rectTransform, new Vector2(16f, -12f), new Vector2(360f, 34f));
        title.color = new Color(0.73f, 1f, 0.95f);

        Text healthText = CreateText(vitalsPanel.transform, "Health Label", "CORE INTEGRITY", 18, TextAnchor.MiddleLeft);
        PlaceTopLeft(healthText.rectTransform, new Vector2(16f, -52f), new Vector2(370f, 24f));
        Image healthBack = CreatePanel(vitalsPanel.transform, "Health Bar", Anchor.TopLeft, new Vector2(16f, -78f), new Vector2(370f, 18f), new Color(0.12f, 0.025f, 0.035f, 1f));
        Image healthFill = CreatePanel(healthBack.transform, "Health Fill", Anchor.FillLeft, Vector2.zero, Vector2.zero, new Color(1f, 0.18f, 0.24f, 1f));

        Text armorText = CreateText(vitalsPanel.transform, "Armor Label", "ARMOR", 18, TextAnchor.MiddleLeft);
        PlaceTopLeft(armorText.rectTransform, new Vector2(16f, -102f), new Vector2(370f, 24f));
        Image armorBack = CreatePanel(vitalsPanel.transform, "Armor Bar", Anchor.TopLeft, new Vector2(16f, -128f), new Vector2(370f, 18f), new Color(0.02f, 0.07f, 0.09f, 1f));
        Image armorFill = CreatePanel(armorBack.transform, "Armor Fill", Anchor.FillLeft, Vector2.zero, Vector2.zero, new Color(0.2f, 0.86f, 1f, 1f));

        Text heatText = CreateText(vitalsPanel.transform, "Heat Label", "WEAPON HEAT", 18, TextAnchor.MiddleLeft);
        PlaceTopLeft(heatText.rectTransform, new Vector2(16f, -152f), new Vector2(370f, 24f));
        Image heatBack = CreatePanel(vitalsPanel.transform, "Heat Bar", Anchor.TopLeft, new Vector2(16f, -178f), new Vector2(370f, 18f), new Color(0.1f, 0.045f, 0.015f, 1f));
        Image heatFill = CreatePanel(heatBack.transform, "Heat Fill", Anchor.FillLeft, Vector2.zero, Vector2.zero, new Color(1f, 0.45f, 0.14f, 1f));

        Text resources = CreateText(vitalsPanel.transform, "Resources", "", 17, TextAnchor.MiddleLeft);
        PlaceTopLeft(resources.rectTransform, new Vector2(16f, -202f), new Vector2(380f, 28f));
        resources.color = new Color(1f, 0.9f, 0.56f);

        Image missionPanel = CreatePanel(canvasObject.transform, "Mission Panel", Anchor.TopRight, new Vector2(-18f, -18f), new Vector2(385f, 218f), new Color(0.015f, 0.025f, 0.03f, 0.93f));
        Text missionTitle = CreateText(missionPanel.transform, "Mission Title", "MISSION PROGRESS", 24, TextAnchor.UpperLeft);
        PlaceTopLeft(missionTitle.rectTransform, new Vector2(16f, -12f), new Vector2(350f, 34f));
        missionTitle.color = new Color(1f, 0.78f, 0.34f);

        Text progress = CreateText(missionPanel.transform, "Progress", "", 18, TextAnchor.UpperLeft);
        PlaceTopLeft(progress.rectTransform, new Vector2(16f, -52f), new Vector2(350f, 96f));

        Text coreHelp = CreateText(missionPanel.transform, "Skill Core Help", "SKILL CORES\nTouch glowing labeled diamonds to automatically install their upgrade.", 17, TextAnchor.UpperLeft);
        PlaceTopLeft(coreHelp.rectTransform, new Vector2(16f, -142f), new Vector2(350f, 66f));
        coreHelp.color = new Color(0.7f, 0.95f, 1f);

        Image comparisonPanel = CreatePanel(canvasObject.transform, "Weapon Comparison Panel", Anchor.TopRight, new Vector2(-18f, -252f), new Vector2(385f, 252f), new Color(0.015f, 0.025f, 0.03f, 0.95f));
        Text comparisonTitle = CreateText(comparisonPanel.transform, "Comparison Title", "WEAPON COMPARISON", 22, TextAnchor.UpperLeft);
        PlaceTopLeft(comparisonTitle.rectTransform, new Vector2(16f, -12f), new Vector2(350f, 30f));
        comparisonTitle.color = new Color(0.42f, 0.92f, 1f);
        Text comparison = CreateText(comparisonPanel.transform, "Comparison", "", 16, TextAnchor.UpperLeft);
        PlaceTopLeft(comparison.rectTransform, new Vector2(16f, -48f), new Vector2(350f, 192f));
        comparisonPanel.gameObject.SetActive(false);

        Image statusPanel = CreatePanel(canvasObject.transform, "Status Panel", Anchor.BottomStretch, new Vector2(0f, 16f), new Vector2(-36f, 58f), new Color(0.01f, 0.02f, 0.025f, 0.92f));
        Text status = CreateText(statusPanel.transform, "Status", "WASD move | Mouse fire | Space dash | E equip | Q Nova | F Guard | R purge | Esc pause", 20, TextAnchor.MiddleCenter);
        StretchRect(status.rectTransform, new Vector2(14f, 4f), new Vector2(-14f, -4f));
        status.color = new Color(0.92f, 0.96f, 1f);

        return new GameHud(player, owner, healthFill, armorFill, heatFill, damage, status, healthText, armorText, heatText, resources, progress, comparisonPanel.gameObject, comparison);
    }

    public void Refresh(int wave, int awakeEnemyCount, int totalEnemyCount)
    {
        healthFill.fillAmount = Mathf.Clamp01((float)player.Health / player.MaxHealth);
        armorFill.fillAmount = Mathf.Clamp01((float)player.Armor / player.MaxArmor);
        heatFill.fillAmount = Mathf.Clamp01(player.Heat / player.MaxHeat);
        heatFill.color = player.IsOverheated ? new Color(1f, 0.08f, 0.05f) : new Color(1f, 0.45f, 0.14f);
        int pickupCount = director != null ? director.ActiveWeaponPickupCount() : 0;

        int cores = director != null ? director.SalvageCores : 0;
        int kills = director != null ? director.DefeatedMachines : 0;
        healthText.text = $"CORE INTEGRITY        {player.Health} / {player.MaxHealth}";
        armorText.text = $"ARMOR SHIELD          {player.Armor} / {player.MaxArmor}";
        heatText.text = player.IsOverheated
            ? $"WEAPON HEAT            {Mathf.RoundToInt(player.Heat)} / {Mathf.RoundToInt(player.MaxHeat)}  JAMMED"
            : $"WEAPON HEAT            {Mathf.RoundToInt(player.Heat)} / {Mathf.RoundToInt(player.MaxHeat)}";
        resourceText.text = $"SCRAP {player.Scrap}   WEAPON: {player.WeaponName}";
        progressText.text = wave < 4
            ? $"WAVE {wave} / 4\nClear all four rooms: {totalEnemyCount} remain ({awakeEnemyCount} awake)\nBoss arrives after Wave 3\nCores {cores}/3   Kills {kills}   Weapon drops {pickupCount}"
            : $"BOSS WAVE 4 / 4\nBoss arena enemies remaining: {totalEnemyCount}\nDestroy all three Bosses and their escorts\nCores {cores}/3   Kills {kills}";

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

    public void ShowWeaponComparison(WeaponStats current, WeaponStats candidate)
    {
        if (weaponComparePanel == null || weaponCompareText == null || candidate == null)
        {
            return;
        }

        weaponComparePanel.SetActive(true);
        if (current == null)
        {
            weaponCompareText.text =
                $"CANDIDATE: {candidate.Name}\n" +
                $"Volley damage: {candidate.VolleyDamage}\n" +
                $"Fire rate: {candidate.ShotsPerSecond:0.0} shots/s\n" +
                $"Heat: {candidate.HeatPerShot:0.#} / shot\n" +
                $"Projectile: {candidate.ProjectileDescription()}\n\n" +
                "Press E to equip. Combat remains active.";
            return;
        }

        weaponCompareText.text =
            $"CURRENT: {current.Name}\n" +
            $"CANDIDATE: {candidate.Name}\n\n" +
            $"Volley damage: {current.VolleyDamage} -> {candidate.VolleyDamage}  {FormatDelta(candidate.VolleyDamage - current.VolleyDamage)}\n" +
            $"Fire rate: {current.ShotsPerSecond:0.0} -> {candidate.ShotsPerSecond:0.0}/s  {FormatDelta(candidate.ShotsPerSecond - current.ShotsPerSecond)}\n" +
            $"Heat / shot: {current.HeatPerShot:0.#} -> {candidate.HeatPerShot:0.#}  {FormatDelta(current.HeatPerShot - candidate.HeatPerShot)} efficiency\n" +
            $"Projectile: {candidate.ProjectileDescription()}\n\n" +
            "Press E to equip. Combat remains active.";
    }

    public void HideWeaponComparison()
    {
        weaponComparePanel?.SetActive(false);
    }

    private static string FormatDelta(float delta)
    {
        if (Mathf.Abs(delta) < 0.05f)
        {
            return "(same)";
        }

        return delta > 0f ? $"(+{delta:0.#})" : $"({delta:0.#})";
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

    private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void StretchRect(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
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
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                break;
            case Anchor.TopRight:
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                break;
            case Anchor.BottomStretch:
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
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
        TopRight,
        BottomStretch,
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

public class WorldObjectLabel : MonoBehaviour
{
    private float worldScale = 0.72f;
    private float revealDistance = 2.15f;
    private PlayerController player;
    private Renderer[] labelRenderers;

    public void Configure(float scale, float distance)
    {
        worldScale = scale;
        revealDistance = distance;
    }

    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            worldScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
            worldScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)),
            1f);

        if (player == null)
        {
            player = FindObjectOfType<PlayerController>();
        }

        if (labelRenderers == null)
        {
            labelRenderers = GetComponentsInChildren<Renderer>(true);
        }

        bool visible = player != null && Vector2.Distance(transform.position, player.transform.position) <= revealDistance;
        foreach (Renderer renderer in labelRenderers)
        {
            renderer.enabled = visible;
        }
    }
}

public class ObstacleIdentity : MonoBehaviour
{
    private GameDirector director;
    private string displayName;
    private float nextHintTime;

    public void Configure(GameDirector owner, string obstacleName)
    {
        director = owner;
        displayName = obstacleName;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time < nextHintTime || collision.collider.GetComponent<PlayerController>() == null)
        {
            return;
        }

        nextHintTime = Time.time + 1.2f;
        director?.ShowStatus($"Blocked by {displayName}", 1.1f);
    }
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
