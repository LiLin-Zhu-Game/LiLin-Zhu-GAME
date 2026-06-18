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
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int maxArmor = 20;
    [SerializeField] private float scrapPickupRadius = 1.45f;

    [Header("Heat")]
    [SerializeField] private float maxHeat = 100f;
    [SerializeField] private float heatCooldownRate = 18f;
    [SerializeField] private float overheatRecoveryHeat = 32f;
    [SerializeField] private float purgeHeatCost = 45f;
    [SerializeField] private float purgeRadius = 3.1f;
    [SerializeField] private int purgeDamage = 36;
    [SerializeField] private float purgeKnockback = 9f;
    [SerializeField] private int novaScrapCost = 20;
    [SerializeField] private float novaRadius = 4.2f;
    [SerializeField] private int novaDamage = 58;
    [SerializeField] private float novaKnockback = 13f;
    [SerializeField] private float novaHeatBonus = 22f;
    [SerializeField] private int guardScrapCost = 6;
    [SerializeField] private float guardDuration = 4.5f;
    [SerializeField] private int guardArmorRepair = 16;
    [SerializeField] private float guardHeatCooling = 26f;
    [SerializeField] private float guardDamageReduction = 0.55f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private WeaponController weapon;
    private GameDirector director;
    private Camera mainCamera;
    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.right;
    private float dashTimer;
    private float nextDashTime;
    private float guardTimer;
    private float overdriveTimer;
    private float heatGainMultiplier = 1f;
    private WeaponPickup nearbyWeapon;
    private int health;
    private int armor;
    private int scrap;
    private bool overheated;
    private bool dead;
    private Vector3 visualBasePosition;
    private Vector3 visualBaseScale = Vector3.one;
    private float movementCycle;
    private float fireKickTimer;
    private float hitFlashTimer;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public int Armor => armor;
    public int MaxArmor => maxArmor;
    public int Scrap => scrap;
    public float Heat { get; private set; }
    public float MaxHeat => maxHeat;
    public bool IsOverheated => overheated;
    public Vector2 AimDirection => aimDirection;
    public bool IsDead => dead;
    public string WeaponName => weapon != null ? weapon.CurrentWeaponName : "None";
    public WeaponStats CurrentWeaponStats => weapon != null ? weapon.CurrentStats : null;
    public int NovaScrapCost => novaScrapCost;
    public int GuardScrapCost => guardScrapCost;
    public float FireDelayMultiplier => overdriveTimer > 0f ? 0.72f : 1f;

    public void SetBodyRenderer(SpriteRenderer bodyRenderer)
    {
        spriteRenderer = bodyRenderer;
        if (spriteRenderer != null)
        {
            visualBasePosition = spriteRenderer.transform.localPosition;
            visualBaseScale = spriteRenderer.transform.localScale;
        }
    }

    public void Initialize(GameDirector owner, WeaponController playerWeapon)
    {
        director = owner;
        weapon = playerWeapon;
        mainCamera = Camera.main;
        health = maxHealth;
        armor = maxArmor;
        scrap = 0;
        Heat = 0f;
        heatGainMultiplier = 1f;
        overdriveTimer = 0f;
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
        if (dead || Time.timeScale <= 0f)
        {
            return;
        }

        ReadMovement();
        ReadAim();
        TickSkillState();
        CoolHeat();
        PullScrapIn();
        CheckWeaponPickup();
        AnimateVisuals();

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

        if (Input.GetKeyDown(KeyCode.Q))
        {
            ScrapNova();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            MagneticGuard();
        }

        if (nearbyWeapon != null && Input.GetKeyDown(KeyCode.E))
        {
            nearbyWeapon.Collect(this);
            nearbyWeapon = null;
        }
    }

    private void FixedUpdate()
    {
        if (dead || Time.timeScale <= 0f)
        {
            body.velocity = Vector2.zero;
            return;
        }

        float currentMoveSpeed = overdriveTimer > 0f ? moveSpeed * 1.25f : moveSpeed;
        Vector2 velocity = moveInput * currentMoveSpeed;
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

        Heat = Mathf.Clamp(Heat + amount * heatGainMultiplier, 0f, maxHeat);
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

        int finalDamage = guardTimer > 0f ? Mathf.Max(1, Mathf.RoundToInt(amount * (1f - guardDamageReduction))) : amount;
        int absorbed = Mathf.Min(armor, finalDamage);
        armor -= absorbed;
        health = Mathf.Max(0, health - (finalDamage - absorbed));
        director.FlashDamage();
        hitFlashTimer = 0.18f;
        if (health <= 0)
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

    public void RepairArmor(int amount)
    {
        if (dead)
        {
            return;
        }

        armor = Mathf.Min(maxArmor, armor + amount);
    }

    public void RepairHealth(int amount)
    {
        if (dead)
        {
            return;
        }

        health = Mathf.Min(maxHealth, health + amount);
    }

    public void CoolWeapon(float amount)
    {
        if (dead)
        {
            return;
        }

        Heat = Mathf.Max(0f, Heat - amount);
        if (overheated && Heat <= overheatRecoveryHeat)
        {
            overheated = false;
        }
    }

    public void EquipWeapon(WeaponStats stats)
    {
        if (weapon == null || stats == null)
        {
            return;
        }

        weapon.ApplyWeapon(stats);
        director.ShowStatus($"Equipped {stats.Name}", 2f);
        director.HideWeaponComparison();
    }

    public void NotifyWeaponFired()
    {
        fireKickTimer = 0.12f;
    }

    public void ApplyRoomSkill(RoomSkillType skillType)
    {
        switch (skillType)
        {
            case RoomSkillType.KineticOverdrive:
                overdriveTimer = Mathf.Max(overdriveTimer, 24f);
                director.ShowStatus("Kinetic Overdrive: movement and fire rate boosted for 24 seconds", 2.4f);
                break;
            case RoomSkillType.CoolantMatrix:
                heatGainMultiplier = Mathf.Max(0.55f, heatGainMultiplier * 0.78f);
                CoolWeapon(maxHeat);
                director.ShowStatus("Coolant Matrix installed: shots permanently generate less heat", 2.4f);
                break;
            case RoomSkillType.NaniteShell:
                maxArmor += 25;
                armor = Mathf.Min(maxArmor, armor + 45);
                RepairHealth(20);
                director.ShowStatus("Nanite Shell installed: maximum armor increased by 25", 2.4f);
                break;
            default:
                scrapPickupRadius += 0.9f;
                AddScrap(8);
                director.ShowStatus("Salvage Magnet installed: pickup range increased and 8 scrap recovered", 2.4f);
                break;
        }
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

    private void ScrapNova()
    {
        if (scrap < novaScrapCost)
        {
            director.ShowStatus($"Need {novaScrapCost} scrap to release Scrap Nova", 1.2f);
            return;
        }

        scrap -= novaScrapCost;
        Heat = Mathf.Min(maxHeat, Heat + novaHeatBonus);
        if (Heat >= maxHeat)
        {
            overheated = true;
        }

        director.PlayPurgeEffect(transform.position, novaRadius);
        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        foreach (EnemyController enemy in enemies)
        {
            if (!enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 offset = enemy.transform.position - transform.position;
            float distance = offset.magnitude;
            if (distance <= novaRadius)
            {
                float falloff = Mathf.Lerp(1f, 0.45f, distance / novaRadius);
                enemy.TakeDamage(Mathf.RoundToInt(novaDamage * falloff), offset.normalized * novaKnockback);
            }
        }

        director.ShowStatus("Scrap Nova released: parts shockwave discharged", 1.6f);
    }

    private void MagneticGuard()
    {
        if (scrap < guardScrapCost)
        {
            director.ShowStatus($"Need {guardScrapCost} scrap to activate Magnetic Guard", 1.2f);
            return;
        }

        scrap -= guardScrapCost;
        guardTimer = guardDuration;
        armor = Mathf.Min(maxArmor, armor + guardArmorRepair);
        Heat = Mathf.Max(0f, Heat - guardHeatCooling);
        if (overheated && Heat <= overheatRecoveryHeat)
        {
            overheated = false;
        }

        director.PlayPurgeEffect(transform.position, 2.2f);
        director.ShowStatus("Magnetic Guard online: armor repaired and damage reduced", 1.6f);
    }

    private void TickSkillState()
    {
        if (overdriveTimer > 0f)
        {
            overdriveTimer = Mathf.Max(0f, overdriveTimer - Time.deltaTime);
        }

        if (guardTimer > 0f)
        {
            guardTimer = Mathf.Max(0f, guardTimer - Time.deltaTime);
        }

        if (spriteRenderer != null && hitFlashTimer <= 0f)
        {
            spriteRenderer.color = guardTimer > 0f
                ? new Color(0.64f, 1f, 0.95f, 1f)
                : overdriveTimer > 0f ? new Color(1f, 0.78f, 0.46f, 1f) : Color.white;
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
            director.ShowWeaponComparison(CurrentWeaponStats, nearbyWeapon.Stats);
        }
        else
        {
            director.HideWeaponComparison();
        }
    }

    private void AnimateVisuals()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        bool moving = moveInput.sqrMagnitude > 0.05f;
        movementCycle += Time.deltaTime * (moving ? 11f : 3f);
        fireKickTimer = Mathf.Max(0f, fireKickTimer - Time.deltaTime);
        hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.deltaTime);

        float step = moving ? Mathf.Sin(movementCycle) : Mathf.Sin(movementCycle) * 0.22f;
        float bob = moving ? Mathf.Abs(step) * 0.055f : step * 0.012f;
        float kick = fireKickTimer > 0f ? fireKickTimer / 0.12f : 0f;
        float hitKick = hitFlashTimer > 0f ? hitFlashTimer / 0.18f : 0f;

        Vector3 targetPosition = visualBasePosition + new Vector3(
            -aimDirection.x * kick * 0.07f,
            bob - aimDirection.y * kick * 0.04f,
            0f);
        float horizontalStretch = moving ? 1f + Mathf.Abs(moveInput.x) * 0.035f : 1f;
        float verticalSquash = moving ? 1f - Mathf.Abs(step) * 0.035f : 1f;
        Vector3 targetScale = Vector3.Scale(visualBaseScale, new Vector3(
            horizontalStretch + hitKick * 0.06f,
            verticalSquash - hitKick * 0.04f,
            1f));
        float directionLean = moving ? -moveInput.x * 4.5f + moveInput.y * step * 1.6f : 0f;
        float recoilLean = -aimDirection.y * kick * 2.5f;

        Transform visual = spriteRenderer.transform;
        visual.localPosition = Vector3.Lerp(visual.localPosition, targetPosition, Time.deltaTime * 18f);
        visual.localScale = Vector3.Lerp(visual.localScale, targetScale, Time.deltaTime * 18f);
        visual.localRotation = Quaternion.Slerp(
            visual.localRotation,
            Quaternion.Euler(0f, 0f, directionLean + recoilLean),
            Time.deltaTime * 16f);

        if (hitFlashTimer > 0f)
        {
            spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.32f, 0.22f, 1f), hitKick);
        }
    }

    public void MoveToNewMap(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, 0f);
        nearbyWeapon = null;
        director.HideWeaponComparison();
        dashTimer = 0f;
        if (body != null)
        {
            body.velocity = Vector2.zero;
        }
    }
}
