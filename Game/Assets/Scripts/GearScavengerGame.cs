using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private readonly List<Vector2Int> floorCells = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> walkable = new HashSet<Vector2Int>();

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
    private int wave;
    private bool gameOver;
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

    private void Start()
    {
        Time.timeScale = 1f;
        Random.InitState(System.DateTime.Now.Millisecond);
        BuildSprites();
        ConfigureCamera();
        BuildLevel();
        BuildPools();
        SpawnPlayer();
        hud = GameHud.Create(player);
        SpawnRoomWeaponDrops(3);
        StartCoroutine(WaveRoutine());
    }

    private void Update()
    {
        hud?.Refresh(wave, activeEnemies.Count);

        if (statusTimer > 0f)
        {
            statusTimer -= Time.deltaTime;
            if (statusTimer <= 0f && !gameOver)
            {
                hud.SetMessage("WASD move  |  Mouse aim/fire  |  Space dash  |  R purge heat");
            }
        }

        if (gameOver && Input.GetKeyDown(KeyCode.Return))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
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
        pickup.Configure(position + Random.insideUnitCircle * 0.75f, stats);
    }

    public void SpawnRoomWeaponDrops(int count)
    {
        if (weaponTable == null || weaponTable.Length == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2Int cell = floorCells[Random.Range(0, floorCells.Count)];
            if (Vector2.Distance(cell, player.transform.position) < 3.5f)
            {
                i--;
                continue;
            }

            WeaponPickup pickup = weaponPickups.Get().GetComponent<WeaponPickup>();
            pickup.Configure(new Vector2(cell.x, cell.y), weaponTable[Random.Range(0, weaponTable.Length)]);
        }
    }

    public void ShowStatus(string message, float seconds = 1.8f)
    {
        hud?.SetMessage(message);
        statusTimer = seconds;
    }

    public void FlashDamage()
    {
        hud?.FlashDamage();
    }

    public void PlayerDied()
    {
        gameOver = true;
        hud.SetMessage("Armor destroyed. Press Enter to rebuild the scavenger.");
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
        yield return new WaitForSeconds(0.6f);

        for (wave = 1; wave <= 4; wave++)
        {
            ShowStatus(wave < 4 ? $"Wave {wave}: hostile machines closing in" : "Boss wave: survive the breaker unit", 2.2f);
            SpawnWave(wave);

            while (activeEnemies.Count > 0 && !gameOver)
            {
                yield return null;
            }

            if (gameOver)
            {
                yield break;
            }

            if (wave < 4)
            {
                ShowStatus("Room clear. Scrap collected into armor.", 2f);
                SpawnRoomWeaponDrops(Random.value < 0.65f ? 1 : 0);
                yield return new WaitForSeconds(2f);
            }
        }

        gameOver = true;
        hud.SetMessage("Prototype complete. You cleared Gear Scavenger! Press Enter to replay.");
    }

    private void SpawnWave(int waveNumber)
    {
        if (waveNumber == 4)
        {
            SpawnEnemy(EnemyKind.Boss, PickSpawnPoint());
            SpawnEnemy(EnemyKind.Chaser, PickSpawnPoint());
            SpawnEnemy(EnemyKind.Drone, PickSpawnPoint());
            return;
        }

        int chasers = 3 + waveNumber * 2;
        int drones = waveNumber >= 2 ? waveNumber : 0;
        int supports = waveNumber >= 3 ? 1 : 0;

        for (int i = 0; i < chasers; i++)
        {
            SpawnEnemy(EnemyKind.Chaser, PickSpawnPoint());
        }

        for (int i = 0; i < drones; i++)
        {
            SpawnEnemy(EnemyKind.Drone, PickSpawnPoint());
        }

        for (int i = 0; i < supports; i++)
        {
            SpawnEnemy(EnemyKind.Support, PickSpawnPoint());
        }
    }

    private void SpawnEnemy(EnemyKind kind, Vector2 position)
    {
        EnemyController enemy = enemies.Get().GetComponent<EnemyController>();
        enemy.Configure(this, player, kind, position);
        activeEnemies.Add(enemy);
    }

    private Vector2 PickSpawnPoint()
    {
        for (int i = 0; i < 80; i++)
        {
            Vector2Int cell = floorCells[Random.Range(0, floorCells.Count)];
            Vector2 position = new Vector2(cell.x, cell.y);
            if (Vector2.Distance(position, player.transform.position) > 6f)
            {
                return position;
            }
        }

        return new Vector2(11f, 0f);
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
        BulletSprite = SpriteFactory.Circle(new Color(0.8f, 1f, 0.96f), Color.clear);
        EnemyBulletSprite = SpriteFactory.Circle(new Color(1f, 0.3f, 0.18f), Color.clear);
        ScrapSprite = LoadSprite("scrap", SpriteFactory.Diamond(new Color(0.55f, 0.85f, 1f), new Color(0.04f, 0.13f, 0.22f)));
        DefaultWeaponSprite = LoadSprite("weapon_rifle", SpriteFactory.Diamond(new Color(0.85f, 0.55f, 1f), new Color(0.12f, 0.04f, 0.18f)));

        weaponTable = new[]
        {
            new WeaponStats("Rust Rifle", LoadSprite("weapon_rifle", DefaultWeaponSprite), 0.14f, 9.5f, 18, 13f, 3.5f, 1, 1.6f),
            new WeaponStats("Scatter Core", LoadSprite("weapon_scatter", DefaultWeaponSprite), 0.28f, 17f, 12, 11f, 12f, 5, 1.15f),
            new WeaponStats("Beam Needle", LoadSprite("weapon_beam", DefaultWeaponSprite), 0.08f, 6.5f, 9, 17f, 1.2f, 1, 1.25f),
            new WeaponStats("Scrap Cannon", LoadSprite("weapon_cannon", DefaultWeaponSprite), 0.48f, 24f, 42, 9.5f, 4f, 1, 1.9f)
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
        mainCamera.orthographicSize = 7.2f;
        mainCamera.backgroundColor = new Color(0.05f, 0.07f, 0.09f);
        mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        mainCamera.gameObject.AddComponent<CameraFollow>();
    }

    private void BuildLevel()
    {
        GameObject levelRoot = new GameObject("Generated Mechanical Rooms");
        AddRoom(-12, 0, 10, 8);
        AddRoom(0, 0, 10, 8);
        AddRoom(12, 0, 10, 8);
        AddRoom(-6, 0, 4, 3);
        AddRoom(6, 0, 4, 3);

        foreach (Vector2Int cell in walkable)
        {
            GameObject tile = new GameObject("Floor");
            tile.transform.SetParent(levelRoot.transform);
            tile.transform.position = new Vector3(cell.x, cell.y, 1f);
            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = floorSprites[Mathf.Abs(cell.x + cell.y) % floorSprites.Length];
            renderer.color = Color.white;
            renderer.sortingOrder = -10;
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
            SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = WallSprite;
            renderer.sortingOrder = -1;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            wall.AddComponent<WallMarker>();
        }

        PlaceDecoration(levelRoot.transform, -13, 2, 1.25f);
        PlaceDecoration(levelRoot.transform, -9, -2, 0.9f);
        PlaceDecoration(levelRoot.transform, 0, 2, 1.1f);
        PlaceDecoration(levelRoot.transform, 10, 2, 1.0f);
        PlaceDecoration(levelRoot.transform, 14, -2, 1.35f);
    }

    private void AddRoom(int centerX, int centerY, int width, int height)
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

    private void PlaceDecoration(Transform root, int x, int y, float scale)
    {
        GameObject prop = new GameObject("Scrap Machinery");
        prop.transform.SetParent(root);
        prop.transform.position = new Vector3(x, y, -0.05f);
        prop.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = propSprites[Random.Range(0, propSprites.Length)];
        renderer.sortingOrder = -2;
        BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * 0.8f;
        prop.AddComponent<WallMarker>();
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
        CircleCollider2D collider = bullet.AddComponent<CircleCollider2D>();
        collider.radius = radius;
        collider.isTrigger = true;
        Rigidbody2D rb = bullet.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        bullet.AddComponent<Bullet>();
        return bullet;
    }

    private GameObject CreateEnemy()
    {
        GameObject enemy = new GameObject("Enemy");
        SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 3;
        CircleCollider2D collider = enemy.AddComponent<CircleCollider2D>();
        collider.radius = 0.43f;
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
            playerObject.transform.position = Vector3.zero;
        }

        SpriteRenderer renderer = playerObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = playerObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = PlayerSprite;
        renderer.sortingOrder = 5;

        if (playerObject.GetComponent<Rigidbody2D>() == null)
        {
            playerObject.AddComponent<Rigidbody2D>();
        }

        if (playerObject.GetComponent<CircleCollider2D>() == null)
        {
            playerObject.AddComponent<CircleCollider2D>();
        }

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
        if (other.GetComponent<WallMarker>() != null)
        {
            director.ReleaseBullet(this);
            return;
        }

        if (owner == BulletOwner.Player)
        {
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, velocity.normalized * 2.5f);
                director.ReleaseBullet(this);
            }
        }
        else
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
                director.ReleaseBullet(this);
            }
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
    private WeaponStats equippedStats;
    private float nextFireTime;

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
            weaponRenderer.transform.localPosition = new Vector3(0.38f, -0.08f, -0.05f);
            weaponRenderer.transform.localRotation = Quaternion.identity;
            weaponRenderer.transform.localScale = Vector3.one * 1.6f;
        }
    }

    private void EnsureWeaponRenderer()
    {
        if (weaponRenderer != null)
        {
            return;
        }

        Transform existing = transform.Find("Equipped Weapon");
        GameObject weaponObject = existing != null ? existing.gameObject : new GameObject("Equipped Weapon");
        weaponObject.transform.SetParent(transform, false);
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
    }

    public void TryFire(Vector2 direction)
    {
        if (Time.time < nextFireTime || player.IsDead)
        {
            return;
        }

        nextFireTime = Time.time + FireDelay;
        player.AddHeat(HeatPerShot);

        Vector2 muzzle = (Vector2)transform.position + direction.normalized * 0.62f;
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        for (int i = 0; i < PelletCount; i++)
        {
            float angle = baseAngle + Random.Range(-Spread, Spread);
            Vector2 shotDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Bullet bullet = director.GetPlayerBullet();
            bullet.Fire(muzzle, shotDirection, BulletSpeed, Damage, BulletLifetime);
        }
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
    private GameDirector director;
    private PlayerController player;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private EnemyKind kind;
    private int health;
    private int maxHealth;
    private float speed;
    private float contactTimer;
    private float shootTimer;
    private float supportPulseTimer;
    private bool active;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Configure(GameDirector owner, PlayerController target, EnemyKind enemyKind, Vector2 position)
    {
        director = owner;
        player = target;
        kind = enemyKind;
        transform.position = position;
        transform.localScale = Vector3.one;
        body.velocity = Vector2.zero;
        active = true;

        switch (kind)
        {
            case EnemyKind.Chaser:
                health = maxHealth = 52;
                speed = 2.4f;
                spriteRenderer.sprite = director.ChaserSprite;
                transform.localScale = Vector3.one * 1.45f;
                break;
            case EnemyKind.Drone:
                health = maxHealth = 42;
                speed = 1.75f;
                spriteRenderer.sprite = director.DroneSprite;
                transform.localScale = Vector3.one * 1.55f;
                break;
            case EnemyKind.Support:
                health = maxHealth = 64;
                speed = 1.55f;
                spriteRenderer.sprite = director.SupportSprite;
                transform.localScale = Vector3.one * 1.55f;
                break;
            default:
                health = maxHealth = 240;
                speed = 1.75f;
                spriteRenderer.sprite = director.BossSprite;
                transform.localScale = Vector3.one * 2.4f;
                break;
        }
    }

    private void FixedUpdate()
    {
        if (!active || player == null || player.IsDead)
        {
            body.velocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = player.transform.position - transform.position;
        float distance = toPlayer.magnitude;
        Vector2 direction = distance > 0.01f ? toPlayer / distance : Vector2.zero;
        float currentSpeed = IsSupported() ? speed * 1.35f : speed;

        if (kind == EnemyKind.Drone && distance < 4.2f)
        {
            body.velocity = -direction * currentSpeed * 0.65f;
        }
        else if (kind == EnemyKind.Support && distance < 3.5f)
        {
            body.velocity = -direction * currentSpeed * 0.45f;
        }
        else
        {
            body.velocity = direction * currentSpeed;
        }

        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
    }

    private void Update()
    {
        if (!active || player == null || player.IsDead)
        {
            return;
        }

        contactTimer -= Time.deltaTime;

        if (kind == EnemyKind.Drone || kind == EnemyKind.Boss)
        {
            shootTimer -= Time.deltaTime;
            if (shootTimer <= 0f)
            {
                FireAtPlayer();
                shootTimer = kind == EnemyKind.Boss ? 0.75f : 1.45f;
            }
        }

        if (kind == EnemyKind.Support)
        {
            supportPulseTimer += Time.deltaTime;
            float pulse = 0.8f + Mathf.Sin(supportPulseTimer * 8f) * 0.2f;
            spriteRenderer.color = new Color(pulse, 1f, pulse, 1f);
        }
        else
        {
            spriteRenderer.color = IsSupported() ? new Color(1f, 1.25f, 1f, 1f) : Color.white;
        }
    }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (!active)
        {
            return;
        }

        health -= amount;
        body.AddForce(knockback, ForceMode2D.Impulse);
        if (health <= 0)
        {
            Die();
        }
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
        if (hitPlayer != null && contactTimer <= 0f)
        {
            hitPlayer.TakeDamage(kind == EnemyKind.Boss ? 18 : 10);
            contactTimer = 0.7f;
        }
    }

    private void Die()
    {
        active = false;
        int scrapValue = kind == EnemyKind.Boss ? 12 : Mathf.Max(2, maxHealth / 24);
        for (int i = 0; i < scrapValue; i++)
        {
            director.SpawnScrap(transform.position, 1);
        }

        director.TrySpawnWeaponDrop(transform.position, kind == EnemyKind.Boss ? 1f : 0.18f);
        director.ReleaseEnemy(this);
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
        transform.localScale = Vector3.one * 2.1f;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = stats.Sprite;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin((Time.time + bobSeed) * 2.8f) * 8f);
    }

    public void Collect(PlayerController player)
    {
        player.EquipWeapon(stats);
        gameObject.SetActive(false);
    }
}

public class GameHud
{
    private readonly PlayerController player;
    private readonly Image armorFill;
    private readonly Image heatFill;
    private readonly Image damageOverlay;
    private readonly Text statusText;
    private readonly Text statsText;
    private float damageFlash;

    private GameHud(PlayerController playerController, Image armor, Image heat, Image damage, Text status, Text stats)
    {
        player = playerController;
        armorFill = armor;
        heatFill = heat;
        damageOverlay = damage;
        statusText = status;
        statsText = stats;
    }

    public static GameHud Create(PlayerController player)
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

        Text status = CreateText(canvasObject.transform, "Status", "WASD move  |  Mouse aim/fire  |  Space dash  |  R purge heat", 18, TextAnchor.LowerCenter);
        status.rectTransform.anchorMin = new Vector2(0f, 0f);
        status.rectTransform.anchorMax = new Vector2(1f, 0f);
        status.rectTransform.anchoredPosition = new Vector2(0f, 34f);
        status.rectTransform.sizeDelta = new Vector2(-40f, 50f);
        status.color = new Color(0.92f, 0.96f, 1f);

        return new GameHud(player, armorFill, heatFill, damage, status, stats);
    }

    public void Refresh(int wave, int enemyCount)
    {
        armorFill.fillAmount = Mathf.Clamp01((float)player.Armor / player.MaxArmor);
        heatFill.fillAmount = Mathf.Clamp01(player.Heat / player.MaxHeat);
        heatFill.color = player.IsOverheated ? new Color(1f, 0.08f, 0.05f) : new Color(1f, 0.45f, 0.14f);
        statsText.text = $"Armor {player.Armor}/{player.MaxArmor}   Heat {Mathf.RoundToInt(player.Heat)}%   Scrap {player.Scrap}   Weapon {player.WeaponName}   Wave {wave}/4   Enemies {enemyCount}";

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
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
            default:
                return Mathf.Abs(offset.x) <= radius && Mathf.Abs(offset.y) <= radius;
        }
    }

    private enum Shape
    {
        Square,
        Circle,
        Diamond
    }
}
