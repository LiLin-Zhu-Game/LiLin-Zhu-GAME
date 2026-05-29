using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6.2f;
    [SerializeField] private float dashForce = 12f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private float dashCooldown = 0.9f;

    [Header("Survival")]
    [SerializeField] private int maxArmor = 100;
    [SerializeField] private float scrapPickupRadius = 1.45f;

    [Header("Heat")]
    [SerializeField] private float maxHeat = 100f;
    [SerializeField] private float heatCooldownRate = 18f;
    [SerializeField] private float overheatRecoveryHeat = 32f;
    [SerializeField] private float purgeHeatCost = 45f;
    [SerializeField] private float purgeRadius = 3.1f;
    [SerializeField] private int purgeDamage = 36;
    [SerializeField] private float purgeKnockback = 9f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private WeaponController weapon;
    private GameDirector director;
    private Camera mainCamera;
    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.right;
    private float dashTimer;
    private float nextDashTime;
    private WeaponPickup nearbyWeapon;
    private int armor;
    private int scrap;
    private bool overheated;
    private bool dead;

    public int Armor => armor;
    public int MaxArmor => maxArmor;
    public int Scrap => scrap;
    public float Heat { get; private set; }
    public float MaxHeat => maxHeat;
    public bool IsOverheated => overheated;
    public Vector2 AimDirection => aimDirection;
    public bool IsDead => dead;
    public string WeaponName => weapon != null ? weapon.CurrentWeaponName : "None";

    public void Initialize(GameDirector owner, WeaponController playerWeapon)
    {
        director = owner;
        weapon = playerWeapon;
        mainCamera = Camera.main;
        armor = maxArmor;
        scrap = 0;
        Heat = 0f;
        overheated = false;
        dead = false;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        spriteRenderer = GetComponent<SpriteRenderer>();

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        circle.radius = 0.28f;
    }

    private void Update()
    {
        if (dead)
        {
            return;
        }

        ReadMovement();
        ReadAim();
        CoolHeat();
        PullScrapIn();
        CheckWeaponPickup();

        if (Input.GetMouseButton(0) && weapon != null && !overheated)
        {
            weapon.TryFire(aimDirection);
        }

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextDashTime && moveInput.sqrMagnitude > 0.01f)
        {
            dashTimer = dashDuration;
            nextDashTime = Time.time + dashCooldown;
        }

        if (Input.GetKeyDown(KeyCode.R) && Heat >= purgeHeatCost * 0.55f)
        {
            Purge();
        }

        if (nearbyWeapon != null && Input.GetKeyDown(KeyCode.E))
        {
            nearbyWeapon.Collect(this);
            nearbyWeapon = null;
        }
    }

    private void FixedUpdate()
    {
        if (dead)
        {
            body.velocity = Vector2.zero;
            return;
        }

        Vector2 velocity = moveInput * moveSpeed;
        if (dashTimer > 0f)
        {
            velocity = moveInput.normalized * dashForce;
            dashTimer -= Time.fixedDeltaTime;
        }

        body.velocity = velocity;
    }

    public void AddHeat(float amount)
    {
        if (dead)
        {
            return;
        }

        Heat = Mathf.Clamp(Heat + amount, 0f, maxHeat);
        if (Heat >= maxHeat)
        {
            overheated = true;
            director.ShowStatus("WEAPON JAMMED - keep moving or press R to purge heat");
        }
    }

    public void TakeDamage(int amount)
    {
        if (dead)
        {
            return;
        }

        armor = Mathf.Max(0, armor - amount);
        director.FlashDamage();
        if (armor <= 0)
        {
            dead = true;
            director.PlayerDied();
        }
    }

    public void AddScrap(int amount)
    {
        scrap += amount;
        armor = Mathf.Min(maxArmor, armor + amount * 4);
    }

    public void EquipWeapon(WeaponStats stats)
    {
        if (weapon == null || stats == null)
        {
            return;
        }

        weapon.ApplyWeapon(stats);
        director.ShowStatus($"Equipped {stats.Name}", 2f);
    }

    private void ReadMovement()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }
    }

    private void ReadAim()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return;
        }

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = mouseWorld - transform.position;
        if (toMouse.sqrMagnitude > 0.04f)
        {
            aimDirection = toMouse.normalized;
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = aimDirection.x < -0.05f;
            }
        }
    }

    private void CoolHeat()
    {
        if (Heat <= 0f)
        {
            Heat = 0f;
            return;
        }

        Heat = Mathf.Max(0f, Heat - heatCooldownRate * Time.deltaTime);
        if (overheated && Heat <= overheatRecoveryHeat)
        {
            overheated = false;
            director.ShowStatus("Weapon back online");
        }
    }

    private void Purge()
    {
        director.PlayPurgeEffect(transform.position, purgeRadius);
        Heat = Mathf.Max(0f, Heat - purgeHeatCost);
        overheated = false;

        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        foreach (EnemyController enemy in enemies)
        {
            if (!enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 offset = enemy.transform.position - transform.position;
            if (offset.magnitude <= purgeRadius)
            {
                enemy.TakeDamage(purgeDamage, offset.normalized * purgeKnockback);
            }
        }
    }

    private void PullScrapIn()
    {
        ScrapPickup[] scraps = FindObjectsOfType<ScrapPickup>();
        foreach (ScrapPickup pickup in scraps)
        {
            if (!pickup.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, pickup.transform.position);
            if (distance <= scrapPickupRadius)
            {
                pickup.Collect(this);
            }
            else if (distance <= scrapPickupRadius * 2.2f)
            {
                pickup.DriftToward(transform.position);
            }
        }
    }

    private void CheckWeaponPickup()
    {
        nearbyWeapon = null;
        float bestDistance = 2.25f;
        WeaponPickup[] pickups = FindObjectsOfType<WeaponPickup>();
        foreach (WeaponPickup pickup in pickups)
        {
            if (!pickup.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, pickup.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearbyWeapon = pickup;
            }
        }

        if (nearbyWeapon != null)
        {
            director.ShowStatus($"Press E to equip {nearbyWeapon.DisplayName}", 0.08f);
        }
    }
}
