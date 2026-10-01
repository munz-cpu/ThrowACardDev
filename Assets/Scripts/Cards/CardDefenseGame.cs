using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>Single-screen, one-card-at-a-time defense game. All gameplay uses Canvas coordinates.</summary>
public sealed class CardDefenseGame : MonoBehaviour
{
    private enum Suit { Heart, Club, Spade, Diamond }
    private enum EnemyKind { Basic, Runner, Tank }
    private enum Upgrade { Damage, Speed, Size, Ana, Creal, Taffy, Pharah, Count }
    private const string AnaDisplayName = "ANA";
    private const string CrealDisplayName = "C,REAL";
    private const string TapieDisplayName = "TAPIE";
    private const string ParaDisplayName = "PARA";

    private sealed class Enemy
    {
        public RectTransform Rect;
        public Image Image;
        public EnemyKind Kind;
        public float Speed;
        public float Radius;
        public int Health;
        public int MaxHealth;
        public int LastHitThrow;
        public RectTransform HealthFill;
        public float PullParticleTimer;
        public bool PharahLatched;
    }

    private sealed class GravityMote
    {
        public RectTransform Rect;
        public Image Image;
        public float Age;
        public float Size;
    }

    private sealed class MiniCard
    {
        public RectTransform Rect;
        public Vector2 Velocity;
    }

    [Header("씬 참조")]
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private CanvasCardThrow cardThrow;
    [SerializeField] private RectTransform enemyLayer;
    [SerializeField] private RectTransform effectLayer;
    [SerializeField] private RectTransform coreRect;
    [SerializeField] private Image coreImage;
    [SerializeField] private Text coreText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text waveText;
    [SerializeField] private Text comboText;
    [SerializeField] private Text rulesText;
    [SerializeField] private Font uiFont;
    [SerializeField] private Sprite uiSprite;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private RectTransform suitSelector;
    [SerializeField] private Button[] suitButtons = new Button[4];
    [SerializeField] private Sprite[] suitSprites = new Sprite[4];
    [SerializeField] private Sprite[] rankSprites = new Sprite[48];
    [SerializeField] private Suit selectedSuit = Suit.Spade;

    [Header("적 이미지")]
    [SerializeField] private Sprite basicEnemySprite;
    [SerializeField] private Sprite runnerEnemySprite;
    [SerializeField] private Sprite tankEnemySprite;

    [Header("게임 밸런스")]
    [SerializeField, Min(1)] private int startingCoreHealth = 15;
    [SerializeField, Min(0.1f)] private float coreRegenInterval = 12f;
    [SerializeField, Min(0.01f)] private float coreDamageFlashSeconds = 0.32f;
    [SerializeField, Min(1f)] private float cardFlightSeconds = 6.5f;
    [SerializeField, Min(0.1f)] private float firstWaveSpawnInterval = 1.1f;
    [SerializeField, Min(1)] private int firstWaveEnemyCount = 7;
    [SerializeField, Min(1f)] private float waveEnemyCountMultiplier = 1.18f;
    [SerializeField, Min(1f)] private float waveEnemyHealthMultiplier = 1.11f;
    [SerializeField, Min(1f)] private float waveEnemySpeedMultiplier = 1.07f;
    [SerializeField, Range(0.01f, 1f)] private float waveSpawnIntervalMultiplier = 0.91f;
    [SerializeField, Min(0.01f)] private float minimumSpawnInterval = 0.18f;
    [SerializeField, Min(1)] private int maximumEnemiesPerWave = 200;
    [SerializeField, Min(1)] private int maximumAliveEnemies = 60;
    [SerializeField, Min(1)] private int maximumEnemyHealth = 40;
    [SerializeField, Min(1f)] private float maximumEnemySpeedMultiplier = 3.5f;
    [SerializeField, Min(0f)] private float suitMenuMaxCardSpeed = 850f;

    [Header("코인 보상")]
    [SerializeField, Min(1)] private int baseCoinsPerKill = 1;
    [SerializeField, Min(1)] private int comboHitsPerBonusCoin = 3;
    [SerializeField, Min(1)] private int wallBouncesPerBonusCoin = 2;
    [SerializeField, Min(0)] private int maxComboBonusCoins = 5;
    [SerializeField, Min(0)] private int maxBounceBonusCoins = 5;

    [Header("스킬 트리 가격")]
    [SerializeField, Min(1)] private int upgradeBaseCost = 5;
    [SerializeField, Min(0)] private int upgradeGlobalCostIncrease = 2;
    [SerializeField, Min(0)] private int upgradeLevelCostIncrease = 2;

    [Header("카드 조준 슬로 모션")]
    [SerializeField, Range(0.05f, 1f)] private float aimTimeScale = 0.2f;
    [SerializeField, Range(0f, 0.8f)] private float aimDimOpacity = 0.3f;

    [Header("카드 패시브")]
    [SerializeField, Min(0f)] private float pharahPullRadius = 250f;
    [SerializeField, Min(1f)] private float pharahLatchedRadiusMultiplier = 2.5f;
    [SerializeField, Min(0f)] private float pharahPullSpeed = 300f;
    [SerializeField, Min(1f)] private float taffyGrabRangeMultiplier = 1.5f;
    [SerializeField, Min(1)] private int anaKillsPerBurst = 5;
    [SerializeField, Min(1)] private int anaMiniCardCount = 6;
    [SerializeField, Min(0f)] private float anaMiniCardSpeed = 480f;
    [SerializeField, Min(1f)] private float serialSpeedMultiplier = 1.1f;
    [SerializeField, Min(1)] private int serialMaxStacks = 12;

    [Header("효과음")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.55f;
    [SerializeField] private AudioClip bounceSound;
    [SerializeField, Range(0f, 1f)] private float bounceVolume = 0.23f;

    [Header("카드 능력 효과음")]
    [SerializeField, InspectorName("ANA · 폭발")] private AudioClip anaAbilitySound;
    [SerializeField, InspectorName("C,REAL · 속도 증가")] private AudioClip crealAbilitySound;
    [SerializeField, InspectorName("TAPIE · 공전 시작")] private AudioClip tapieAbilitySound;
    [SerializeField, InspectorName("PARA · 적 끌어당김")] private AudioClip paraAbilitySound;
    [SerializeField, Range(0f, 1f), InspectorName("능력 효과음 볼륨")]
    private float abilitySoundVolume = 0.7f;

    [Header("카드 조작 효과음")]
    [SerializeField, InspectorName("카드 잡기")] private AudioClip cardGrabSound;
    [SerializeField, InspectorName("카드 놓기")] private AudioClip cardReleaseSound;
    [SerializeField, InspectorName("카드 던지기")] private AudioClip cardThrowSound;
    [SerializeField, Min(0f), InspectorName("던지기 효과음 최소 속도")]
    private float throwSoundMinSpeed = 350f;
    [SerializeField, Range(0f, 1f), InspectorName("조작 효과음 볼륨")]
    private float cardHandlingSoundVolume = 0.7f;

    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly List<MiniCard> miniCards = new List<MiniCard>();
    private readonly GravityMote[] gravityMotes = new GravityMote[64];
    private const int MaxUpgradeLevel = 10;
    private readonly int[] upgradeLevels = new int[(int)Upgrade.Count];
    private readonly Text[] upgradeLabels = new Text[(int)Upgrade.Count];
    private readonly Text[] upgradePrices = new Text[(int)Upgrade.Count];
    private readonly Button[] upgradeButtons = new Button[(int)Upgrade.Count];
    private readonly RectTransform[] upgradeRows = new RectTransform[(int)Upgrade.Count];
    private readonly CanvasGroup[] upgradeRowGroups = new CanvasGroup[(int)Upgrade.Count];
    private RectTransform upgradeTab;
    private RectTransform upgradePanel;
    private float skillTreeReveal;
    private int totalUpgrades;
    private int nextGravityMote;
    private static readonly Color[] SuitColors =
    {
        new Color(0.98f, 0.31f, 0.50f),
        new Color(0.55f, 0.90f, 0.42f),
        new Color(0.43f, 0.60f, 1f),
        new Color(0.31f, 0.91f, 0.94f)
    };

    private int wave;
    private int waveSpawned;
    private int waveTarget;
    private int score;
    private int coins;
    private Text coinText;
    private Text finalScoreText;
    public int Coins => coins;
    private int coreHealth;
    private float coreRegenTimer;
    private Image coreHealthFill;
    private int combo;
    private int wallBounces;
    private int throwSequence;
    private int anaKillCount;
    private int serialStacks;
    private float nextSpawnTime;
    private float nextWaveTime;
    private float flightEndTime;
    private float slowTime;
    private float corePulse;
    private float coreDamageFlashRemaining;
    private Color coreBaseColor;
    private float shakeTime;
    private float shakeStrength;
    private AudioSource audioSource;
    private AudioSource abilityAudioSource;
    private float nextParaAbilitySoundTime;
    private Coroutine hitPause;
    private bool hitPauseActive;
    private Image aimDimmer;
    private RectTransform passiveDescriptionPanel;
    private Text passiveDescriptionText;
    private int visiblePassiveSuit = -1;
    private int selectedDescriptionSuit = -1;
    private float selectedDescriptionUntil;
    private bool gameOver;
    private readonly RectTransform[] suitCardRects = new RectTransform[4];
    private readonly Image[] suitCardImages = new Image[4];
    private readonly float[] suitCardVerticalSpeeds = new float[4];
    private readonly bool[] suitCardRaised = new bool[4];
    private bool suitMenuOpen;
    private bool suitMenuDismissed;
    private float suitMenuChangedAt;
    private Camera uiCamera;
    private Vector2Int lastScreenSize;
    private Vector2 lastCanvasSize;
    private Sprite selectedCardSprite;
    private Vector2 previousCardPosition;
    private float previousCardAngle;

    private void Awake()
    {
        if (canvasRect == null || cardThrow == null || enemyLayer == null || effectLayer == null ||
            coreRect == null || coreImage == null || coreText == null || statusText == null ||
            waveText == null || comboText == null || rulesText == null || uiFont == null || uiSprite == null ||
            gameOverPanel == null ||
            restartButton == null || suitSelector == null || suitButtons == null || suitButtons.Length != 4 ||
            suitSprites == null || suitSprites.Length != 4 ||
            rankSprites == null || rankSprites.Length != 48)
        {
            Debug.LogError("CardDefenseGame의 씬 UI 참조를 연결하세요.", this);
            enabled = false;
            return;
        }

        Canvas canvas = canvasRect.GetComponent<Canvas>();
        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        UpdateViewport();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("Card Canvas에 AudioSource를 추가하세요.", this);
            enabled = false;
            return;
        }
        audioSource.playOnAwake = false;
        abilityAudioSource = gameObject.AddComponent<AudioSource>();
        abilityAudioSource.playOnAwake = false;
        abilityAudioSource.spatialBlend = 0f;
        abilityAudioSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        if (hitSound == null) hitSound = MakeTone(310f);
        if (bounceSound == null) bounceSound = MakeTone(165f);
        for (int i = 0; i < suitButtons.Length; i++)
        {
            int suit = i;
            if (suitButtons[i] == null || suitSprites[i] == null)
            {
                Debug.LogError("카드 4종과 해당 이미지를 모두 연결하세요.", this);
                enabled = false;
                return;
            }
            suitCardRects[i] = suitButtons[i].GetComponent<RectTransform>();
            suitCardImages[i] = suitButtons[i].GetComponent<Image>();
            suitCardImages[i].sprite = suitSprites[i];
            suitCardImages[i].preserveAspect = true;
            suitCardImages[i].raycastTarget = false;
            suitButtons[i].onClick.AddListener(() => SelectSuit(suit));
        }
        for (int i = 0; i < rankSprites.Length; i++)
        {
            if (rankSprites[i] != null) continue;
            Debug.LogError("카드 이미지 48장을 모두 연결하세요.", this);
            enabled = false;
            return;
        }
        restartButton.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
        CreateFinalScoreText();
        gameOverPanel.SetActive(false);
        cardThrow.Grabbed += OnGrabbed;
        cardThrow.Released += OnReleased;
        cardThrow.Thrown += OnThrown;
        cardThrow.WallBounced += OnWallBounced;
        cardThrow.OrbitStarted += OnOrbitStarted;
        coreBaseColor = coreImage.color;
        coreHealth = startingCoreHealth;
        coins = 0;
        CreateCoinHud();
        UpdateCoinHud();
        CreateCoreHealthBar();
        UpdateCoreHealthBar();
        CreateAimDimmer();
        CreatePassiveDescription();
        CreateGravityMotes();
        CreateSkillTree();
    }

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        UpdateResponsiveLayout();
        PickRandomCard();
        PrepareCard();
        BeginWave();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        if (cardThrow == null) return;
        cardThrow.Grabbed -= OnGrabbed;
        cardThrow.Released -= OnReleased;
        cardThrow.Thrown -= OnThrown;
        cardThrow.WallBounced -= OnWallBounced;
        cardThrow.OrbitStarted -= OnOrbitStarted;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }

    private void Update()
    {
        UpdateViewport();
        if (lastCanvasSize != canvasRect.rect.size) UpdateResponsiveLayout();
        UpdateAimEffect();
        UpdateCoreDamageFeedback();
        if (gameOver) return;
        UpdateCoreRegen();
        UpdateSpawning();
        UpdateEnemies();
        UpdateGravityMotes();
        UpdateSkillTreeHover();
        UpdateSuitMenu();
        UpdateShake();
        UpdateHud();
    }

    private void UpdateViewport()
    {
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (screenSize == lastScreenSize) return;
        lastScreenSize = screenSize;
        if (uiCamera == null || screenSize.x <= 0 || screenSize.y <= 0) return;

        const float designAspect = 16f / 9f;
        float screenAspect = (float)screenSize.x / screenSize.y;
        uiCamera.rect = screenAspect > designAspect
            ? new Rect((1f - designAspect / screenAspect) * 0.5f, 0f,
                designAspect / screenAspect, 1f)
            : new Rect(0f, (1f - screenAspect / designAspect) * 0.5f,
                1f, screenAspect / designAspect);
    }

    private void UpdateResponsiveLayout()
    {
        lastCanvasSize = canvasRect.rect.size;
        if (passiveDescriptionPanel != null)
            passiveDescriptionPanel.anchoredPosition = new Vector2(0f,
                lastCanvasSize.y * 0.19f + cardThrow.CardRect.rect.height * 0.5f + 38f);
        if (cardThrow != null)
            cardThrow.Configure(1f + 0.08f * upgradeLevels[(int)Upgrade.Speed],
                1f + 0.06f * upgradeLevels[(int)Upgrade.Size], -lastCanvasSize.y * 0.31f);
    }

    private void LateUpdate()
    {
        if (gameOver) return;
        CheckCardHits();
        UpdateMiniCards();
        UpdateFlight();
    }

    public void SelectSuit(int suit)
    {
        if (suit < 0 || suit >= 4 || gameOver || cardThrow.IsDragging ||
            cardThrow.IsOrbiting ||
            cardThrow.IsFlying && cardThrow.Speed > suitMenuMaxCardSpeed) return;
        if (selectedSuit != (Suit)suit) serialStacks = 0;
        selectedSuit = (Suit)suit;
        PickRandomCard();
        PrepareCard();
        selectedDescriptionSuit = suit;
        selectedDescriptionUntil = Time.unscaledTime + 2.5f;
        ShowPassiveDescription(suit);
        suitMenuOpen = false;
        suitMenuDismissed = true;
    }

    private void PrepareCard()
    {
        ClearGravityMotes();
        wallBounces = 0;
        slowTime = 0f;
        float handY = -canvasRect.rect.height * 0.31f;
        cardThrow.Configure(1f + 0.08f * upgradeLevels[(int)Upgrade.Speed],
            1f + 0.06f * upgradeLevels[(int)Upgrade.Size], handY);
        cardThrow.SetOrbitEnabled(selectedSuit == Suit.Club);
        cardThrow.SetOrbitUpgrades(0.25f * upgradeLevels[(int)Upgrade.Taffy],
            0.04f * upgradeLevels[(int)Upgrade.Taffy]);
        cardThrow.SetGrabDistanceMultiplier(selectedSuit == Suit.Club
            ? taffyGrabRangeMultiplier : 1f);
        Sprite sprite = selectedCardSprite;
        cardThrow.ReturnToHand(sprite, sprite == null ? new Color(0.98f, 0.98f, 1f) : Color.white);
        previousCardPosition = cardThrow.Position;
        previousCardAngle = cardThrow.CardRect.localEulerAngles.z;
        UpdateSuitCards();
    }

    private void PickRandomCard()
    {
        selectedCardSprite = rankSprites[(int)selectedSuit * 12 + Random.Range(0, 12)];
    }

    private void UpdateSuitCards()
    {
        for (int i = 0; i < suitButtons.Length; i++)
        {
            suitCardImages[i].color = i == (int)selectedSuit ? Color.white : new Color(0.80f, 0.82f, 0.88f);
        }
    }

    private void UpdateSuitMenu()
    {
        Mouse mouse = Mouse.current;
        bool canOpen = !cardThrow.IsDragging && !cardThrow.IsOrbiting && mouse != null &&
            (!cardThrow.IsFlying || cardThrow.Speed <= suitMenuMaxCardSpeed);
        Vector2 screenPoint = mouse == null ? Vector2.zero : mouse.position.ReadValue();
        bool pointerOnMenu = false;
        if (canOpen && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            Vector2 centeredPoint = localPoint - canvasRect.rect.center;
            float fromBottom = centeredPoint.y + canvasRect.rect.height * 0.5f;
            pointerOnMenu = Mathf.Abs(centeredPoint.x) < 250f && fromBottom >= 0f &&
                fromBottom < (suitMenuOpen ? 205f : 95f);
        }
        if (!pointerOnMenu) suitMenuDismissed = false;
        bool shouldOpen = canOpen && pointerOnMenu && !suitMenuDismissed;
        if (shouldOpen != suitMenuOpen) suitMenuChangedAt = Time.unscaledTime;
        suitMenuOpen = shouldOpen;
        float elapsed = Time.unscaledTime - suitMenuChangedAt;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
        int hoveredSuit = -1;
        for (int i = 0; i < suitCardRects.Length; i++)
        {
            RectTransform rect = suitCardRects[i];
            if (elapsed >= (suitMenuOpen ? i : 3 - i) * 0.045f)
                suitCardRaised[i] = suitMenuOpen;
            bool hovered = suitMenuOpen && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, uiCamera);
            if (hovered) hoveredSuit = i;
            float targetY = suitCardRaised[i] ? (hovered ? 97f : 72f) : -80f;
            Vector2 position = rect.anchoredPosition;
            suitCardVerticalSpeeds[i] += ((targetY - position.y) * 210f -
                suitCardVerticalSpeeds[i] * 19f) * dt;
            position.y += suitCardVerticalSpeeds[i] * dt;
            if (Mathf.Abs(targetY - position.y) < 0.1f && Mathf.Abs(suitCardVerticalSpeeds[i]) < 1f)
            {
                position.y = targetY;
                suitCardVerticalSpeeds[i] = 0f;
            }
            rect.anchoredPosition = position;
            float blend = 1f - Mathf.Exp(-14f * dt);
            rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * (hovered ? 1.13f : 1f), blend);
            float angle = suitCardRaised[i] ? (i - 1.5f) * -5f : 0f;
            rect.localRotation = Quaternion.Slerp(rect.localRotation, Quaternion.Euler(0f, 0f, angle), blend);
            suitCardImages[i].raycastTarget = suitMenuOpen && position.y > 5f;
            suitButtons[i].interactable = suitMenuOpen && position.y > 5f;
        }
        if (hoveredSuit >= 0) ShowPassiveDescription(hoveredSuit);
        else if (Time.unscaledTime < selectedDescriptionUntil)
            ShowPassiveDescription(selectedDescriptionSuit);
        else HidePassiveDescription();
    }

    private void OnThrown()
    {
        if (cardThrow.Speed >= throwSoundMinSpeed)
            PlayCardHandlingSound(cardThrowSound);
        throwSequence++;
        previousCardPosition = cardThrow.Position;
        previousCardAngle = cardThrow.CardRect.localEulerAngles.z;
        flightEndTime = Time.time + cardFlightSeconds;
        slowTime = 0f;
        if (selectedSuit == Suit.Diamond && serialStacks > 0)
            cardThrow.MultiplySpeed(Mathf.Pow(GetCrealSpeedMultiplier(), serialStacks));
    }

    private void OnGrabbed()
    {
        PlayCardHandlingSound(cardGrabSound);
        combo = 0;
        wallBounces = 0;
        ShowCombo();
        selectedDescriptionUntil = 0f;
        HidePassiveDescription();
    }

    private void OnReleased()
    {
        PlayCardHandlingSound(cardReleaseSound);
    }

    private void OnWallBounced()
    {
        if (!cardThrow.IsFlying || gameOver) return;
        wallBounces++;
        audioSource.pitch = 0.9f + Mathf.Min(wallBounces, 7) * 0.05f;
        audioSource.PlayOneShot(bounceSound, bounceVolume);
        if (wallBounces > 1)
            ShowPopup("벽 반사 " + wallBounces + "회", cardThrow.Position, SuitColors[(int)Suit.Spade]);
    }

    private void OnOrbitStarted()
    {
        if (selectedSuit == Suit.Club && !gameOver)
            PlayAbilitySound(tapieAbilitySound);
    }

    private void PlayAbilitySound(AudioClip clip)
    {
        if (clip != null) abilityAudioSource.PlayOneShot(clip, abilitySoundVolume);
    }

    private void PlayCardHandlingSound(AudioClip clip)
    {
        if (clip != null) abilityAudioSource.PlayOneShot(clip, cardHandlingSoundVolume);
    }

    private void UpdateFlight()
    {
        if (!cardThrow.IsFlying || cardThrow.IsDragging) return;
        if (selectedSuit == Suit.Diamond) return;
        if (cardThrow.IsOrbiting)
        {
            flightEndTime += Time.deltaTime;
            slowTime = 0f;
            return;
        }
        slowTime = cardThrow.Speed < 55f ? slowTime + Time.deltaTime : 0f;
        if (Time.time >= flightEndTime || slowTime >= 1.2f) PrepareCard();
    }

    private void BeginWave()
    {
        wave++;
        waveSpawned = 0;
        waveTarget = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(maximumEnemiesPerWave,
            firstWaveEnemyCount * Mathf.Pow(waveEnemyCountMultiplier, wave - 1))));
        nextSpawnTime = Time.time + 1f;
        nextWaveTime = 0f;
        ShowPopup("웨이브 " + wave, new Vector2(0f, canvasRect.rect.height * 0.22f), Color.black);
    }

    private void UpdateSpawning()
    {
        if (waveSpawned < waveTarget && enemies.Count < maximumAliveEnemies &&
            Time.time >= nextSpawnTime)
        {
            SpawnEnemy();
            waveSpawned++;
            nextSpawnTime = Time.time + Mathf.Max(minimumSpawnInterval,
                firstWaveSpawnInterval * Mathf.Pow(waveSpawnIntervalMultiplier, wave - 1));
        }
        if (waveSpawned == waveTarget && enemies.Count == 0)
        {
            if (nextWaveTime <= 0f) nextWaveTime = Time.time + 3f;
            if (Time.time >= nextWaveTime) BeginWave();
        }
    }

    private void SpawnEnemy()
    {
        EnemyKind kind = wave >= 3 && waveSpawned % 7 == 6 ? EnemyKind.Tank
            : wave >= 2 && waveSpawned % 4 == 3 ? EnemyKind.Runner : EnemyKind.Basic;
        float radius = kind == EnemyKind.Tank ? 38f : kind == EnemyKind.Runner ? 21f : 29f;
        float speed = (kind == EnemyKind.Runner ? 95f : kind == EnemyKind.Tank ? 31f : 52f)
            * Mathf.Min(maximumEnemySpeedMultiplier,
                Mathf.Pow(waveEnemySpeedMultiplier, wave - 1));
        Color color = kind == EnemyKind.Tank ? new Color(0.72f, 0.39f, 1f)
            : kind == EnemyKind.Runner ? new Color(1f, 0.77f, 0.31f)
            : new Color(1f, 0.42f, 0.43f);
        RectTransform rect = CreateRect(kind.ToString() + " Enemy", enemyLayer,
            new Vector2(radius * 2f, radius * 2f), Vector2.zero);
        float halfW = canvasRect.rect.width * 0.5f - radius;
        float halfH = canvasRect.rect.height * 0.5f - radius;
        int side = UnityEngine.Random.Range(0, 4);
        rect.anchoredPosition = side == 0 ? new Vector2(-halfW, UnityEngine.Random.Range(-halfH, halfH))
            : side == 1 ? new Vector2(halfW, UnityEngine.Random.Range(-halfH, halfH))
            : side == 2 ? new Vector2(UnityEngine.Random.Range(-halfW, halfW), -halfH)
            : new Vector2(UnityEngine.Random.Range(-halfW, halfW), halfH);
        Image image = rect.gameObject.AddComponent<Image>();
        Sprite enemySprite = kind == EnemyKind.Tank ? tankEnemySprite
            : kind == EnemyKind.Runner ? runnerEnemySprite : basicEnemySprite;
        image.sprite = enemySprite;
        image.color = enemySprite == null ? color : Color.white;
        image.preserveAspect = enemySprite != null;
        image.raycastTarget = false;
        if (enemySprite == null)
        {
            Text glyph = MakeText(kind == EnemyKind.Tank ? "강" : kind == EnemyKind.Runner ? ">" : "X",
                rect, 24, Color.white, TextAnchor.MiddleCenter);
            Stretch(glyph.rectTransform, 0f);
        }
        int maxHealth = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(maximumEnemyHealth,
            (kind == EnemyKind.Tank ? 3 : 2) *
            Mathf.Pow(waveEnemyHealthMultiplier, wave - 1))));
        RectTransform healthBar = CreateRect("Health Bar", rect,
            new Vector2(radius * 1.6f, 8f), new Vector2(0f, radius + 12f));
        Image healthBackground = healthBar.gameObject.AddComponent<Image>();
        healthBackground.sprite = uiSprite;
        healthBackground.type = Image.Type.Sliced;
        healthBackground.pixelsPerUnitMultiplier = 4f;
        healthBackground.color = new Color(0.12f, 0.14f, 0.2f, 0.95f);
        healthBackground.raycastTarget = false;
        RectTransform healthFill = CreateRect("Health Fill", healthBar,
            new Vector2(radius * 1.6f - 4f, 4f), Vector2.zero);
        healthFill.anchorMin = healthFill.anchorMax = new Vector2(0f, 0.5f);
        healthFill.pivot = new Vector2(0f, 0.5f);
        healthFill.anchoredPosition = new Vector2(2f, 0f);
        Image fillImage = healthFill.gameObject.AddComponent<Image>();
        fillImage.sprite = uiSprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.pixelsPerUnitMultiplier = 6f;
        fillImage.color = new Color(0.42f, 0.95f, 0.55f);
        fillImage.raycastTarget = false;
        enemies.Add(new Enemy
        {
            Rect = rect, Image = image, Kind = kind, Speed = speed, Radius = radius,
            Health = maxHealth, MaxHealth = maxHealth, HealthFill = healthFill,
            PullParticleTimer = Random.Range(0f, 0.08f)
        });
    }

    private void UpdateEnemies()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            Vector2 pos = enemy.Rect.anchoredPosition;
            Vector2 step = -pos.normalized * enemy.Speed * Time.deltaTime;
            pos += step;
            bool pulled = false;
            if (selectedSuit == Suit.Heart && cardThrow.IsFlying && !cardThrow.IsDragging)
            {
                Vector2 towardCard = cardThrow.Position - pos;
                float distance = towardCard.magnitude;
                float pullRadius = pharahPullRadius *
                    (enemy.PharahLatched ? pharahLatchedRadiusMultiplier : 1f);
                if (distance > 0.01f && distance < pullRadius)
                {
                    if (!enemy.PharahLatched && Time.unscaledTime >= nextParaAbilitySoundTime)
                    {
                        PlayAbilitySound(paraAbilitySound);
                        nextParaAbilitySoundTime = Time.unscaledTime + 0.12f;
                    }
                    enemy.PharahLatched = true;
                    float strength = 1f - distance / Mathf.Max(1f, pullRadius);
                    Vector2 direction = towardCard / distance;
                    pos += direction * (pharahPullSpeed *
                        (1f + 0.12f * upgradeLevels[(int)Upgrade.Pharah]) *
                        strength * strength * Time.deltaTime);
                    enemy.PullParticleTimer -= Time.deltaTime;
                    if (enemy.PullParticleTimer <= 0f)
                    {
                        SpawnGravityMote(enemy);
                        enemy.PullParticleTimer = Random.Range(0.075f, 0.12f);
                    }
                    pulled = true;
                }
            }
            if (!pulled) enemy.PullParticleTimer = 0f;
            enemy.Rect.anchoredPosition = pos;
            if (enemy.Rect.anchoredPosition.magnitude > 58f) continue;
            coreHealth = Mathf.Max(0, coreHealth - (enemy.Kind == EnemyKind.Tank ? 3 : 1));
            coreRegenTimer = 0f;
            UpdateCoreHealthBar();
            corePulse = 1f;
            coreDamageFlashRemaining = coreDamageFlashSeconds;
            Shake(7f, 0.14f);
            ShowPopup("코어 -" + (enemy.Kind == EnemyKind.Tank ? 3 : 1), Vector2.zero, Color.red);
            Destroy(enemy.Rect.gameObject);
            enemies.RemoveAt(i);
            if (coreHealth == 0) EndGame();
        }
    }

    private void CreateGravityMotes()
    {
        for (int i = 0; i < gravityMotes.Length; i++)
        {
            RectTransform rect = CreateRect("Gravity Mote", effectLayer,
                new Vector2(6f, 6f), Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            rect.gameObject.SetActive(false);
            gravityMotes[i] = new GravityMote { Rect = rect, Image = image };
        }
    }

    private void SpawnGravityMote(Enemy enemy)
    {
        GravityMote mote = gravityMotes[nextGravityMote];
        nextGravityMote = (nextGravityMote + 1) % gravityMotes.Length;
        Vector2 center = canvasRect.InverseTransformPoint(enemy.Rect.position);
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
        mote.Rect.anchoredPosition = center + direction * Random.Range(enemy.Radius + 4f, enemy.Radius + 22f);
        mote.Size = Random.Range(4f, 8f);
        mote.Rect.sizeDelta = Vector2.one * mote.Size;
        mote.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
        mote.Image.color = new Color(0.7f, 0.35f, 1f, 0.95f);
        mote.Age = 0f;
        mote.Rect.gameObject.SetActive(true);
    }

    private void UpdateGravityMotes()
    {
        bool active = selectedSuit == Suit.Heart && cardThrow.IsFlying && !cardThrow.IsDragging;
        for (int i = 0; i < gravityMotes.Length; i++)
        {
            GravityMote mote = gravityMotes[i];
            if (!mote.Rect.gameObject.activeSelf) continue;
            if (!active)
            {
                mote.Rect.gameObject.SetActive(false);
                continue;
            }
            mote.Age += Time.deltaTime;
            Vector2 target = cardThrow.Position;
            mote.Rect.anchoredPosition = Vector2.MoveTowards(mote.Rect.anchoredPosition,
                target, (280f + mote.Age * 900f) * Time.deltaTime);
            float remaining = Mathf.Clamp01(1f - mote.Age / 0.7f);
            mote.Rect.sizeDelta = Vector2.one * (mote.Size * (0.35f + 0.65f * remaining));
            mote.Image.color = new Color(0.7f, 0.35f, 1f, 0.95f * remaining);
            if (mote.Age >= 0.7f || Vector2.Distance(mote.Rect.anchoredPosition, target) < 10f)
                mote.Rect.gameObject.SetActive(false);
        }
    }

    private void ClearGravityMotes()
    {
        for (int i = 0; i < gravityMotes.Length; i++)
            if (gravityMotes[i] != null) gravityMotes[i].Rect.gameObject.SetActive(false);
    }

    private void CheckCardHits()
    {
        Vector2 currentPosition = cardThrow.Position;
        float currentAngle = cardThrow.CardRect.localEulerAngles.z;
        if (!cardThrow.IsFlying || cardThrow.IsDragging)
        {
            previousCardPosition = currentPosition;
            previousCardAngle = currentAngle;
            return;
        }
        Vector2 halfSize = Vector2.Scale(cardThrow.CardRect.rect.size * 0.5f,
            cardThrow.CardRect.localScale);
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            if (enemy.LastHitThrow == throwSequence) continue;
            Vector2 visibleEnemyPosition = canvasRect.InverseTransformPoint(enemy.Rect.position);
            if (!SweptCardTouchesEnemy(previousCardPosition, currentPosition,
                previousCardAngle, currentAngle, halfSize, visibleEnemyPosition, enemy.Radius)) continue;
            enemy.LastHitThrow = throwSequence;
            bool killed = DamageEnemy(enemy,
                cardThrow.Speed >= cardThrow.TrailMinSpeed ? enemy.MaxHealth :
                1 + upgradeLevels[(int)Upgrade.Damage]);
            combo++;
            score += 100 + Mathf.Min(combo, 20) * 10;
            audioSource.pitch = Mathf.Min(1.65f, 0.92f + combo * 0.075f);
            audioSource.PlayOneShot(hitSound, hitVolume);
            Shake(12f + Mathf.Min(combo, 10), 0.16f);
            if (hitPause == null) hitPause = StartCoroutine(HitPause());
            RegisterCrealHit();
            ShowPopup(killed ? (combo > 1 ? "콤보 " + combo + "회" : "명중!") : "타격",
                enemy.Rect.anchoredPosition, Color.black);
            ShowCombo();
            flightEndTime = Mathf.Min(Time.time + 9f, flightEndTime + 0.5f);
            if (killed) DefeatEnemy(i, true);
        }
        previousCardPosition = currentPosition;
        previousCardAngle = currentAngle;
    }

    private bool DamageEnemy(Enemy enemy, int damage)
    {
        enemy.Health = Mathf.Max(0, enemy.Health - damage);
        float healthFraction = enemy.Health / (float)enemy.MaxHealth;
        enemy.HealthFill.sizeDelta = new Vector2(
            (enemy.Radius * 1.6f - 4f) * healthFraction, 4f);
        enemy.HealthFill.GetComponent<Image>().color = Color.Lerp(
            new Color(1f, 0.35f, 0.36f), new Color(0.42f, 0.95f, 0.55f), healthFraction);
        return enemy.Health == 0;
    }

    private void DefeatEnemy(int index, bool mainCardKill)
    {
        Enemy enemy = enemies[index];
        Vector2 position = enemy.Rect.anchoredPosition;
        StartCoroutine(Burst(position, enemy.Image.color));
        int comboBonus = Mathf.Min(maxComboBonusCoins,
            Mathf.Max(0, combo - 1) / Mathf.Max(1, comboHitsPerBonusCoin));
        int bounceBonus = Mathf.Min(maxBounceBonusCoins,
            wallBounces / Mathf.Max(1, wallBouncesPerBonusCoin));
        int earnedCoins = Mathf.Max(1, baseCoinsPerKill) + comboBonus + bounceBonus;
        AddCoins(earnedCoins);
        ShowPopup("코인 +" + earnedCoins, position + new Vector2(0f, 40f),
            new Color(1f, 0.83f, 0.3f));
        Destroy(enemy.Rect.gameObject);
        enemies.RemoveAt(index);

        if (!mainCardKill || selectedSuit != Suit.Spade) return;
        anaKillCount++;
        if (anaKillCount < Mathf.Max(1, anaKillsPerBurst)) return;
        anaKillCount = 0;
        SpawnAnaMiniCards(cardThrow.Position);
        PlayAbilitySound(anaAbilitySound);
        ShowPopup("폭발!", cardThrow.Position, SuitColors[(int)Suit.Spade]);
    }

    private void SpawnAnaMiniCards(Vector2 origin)
    {
        int count = Mathf.Max(1, anaMiniCardCount);
        for (int i = 0; i < count; i++)
        {
            float angle = (i + 0.5f) * Mathf.PI * 2f / count;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float sizeMultiplier = 1f + 0.1f * upgradeLevels[(int)Upgrade.Ana];
            RectTransform rect = CreateRect("Ana Mini Card", effectLayer,
                new Vector2(30f, 42f) * sizeMultiplier, origin);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = selectedCardSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
            miniCards.Add(new MiniCard { Rect = rect, Velocity = direction * anaMiniCardSpeed });
        }
    }

    private void UpdateMiniCards()
    {
        float dt = Time.deltaTime;
        for (int j = miniCards.Count - 1; j >= 0; j--)
        {
            MiniCard mini = miniCards[j];
            Vector2 start = mini.Rect.anchoredPosition;
            Vector2 end = start + mini.Velocity * dt;
            mini.Rect.anchoredPosition = end;
            bool consumed = false;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                if (!SegmentTouchesCircle(start, end, enemy.Rect.anchoredPosition,
                    enemy.Radius + mini.Rect.rect.width * 0.5f)) continue;
                score += 100;
                if (DamageEnemy(enemy, 1 + upgradeLevels[(int)Upgrade.Ana])) DefeatEnemy(i, false);
                else ShowPopup("타격", enemy.Rect.anchoredPosition, Color.black);
                consumed = true;
                break;
            }
            if (!consumed &&
                Mathf.Abs(end.x) < canvasRect.rect.width * 0.5f + 30f &&
                Mathf.Abs(end.y) < canvasRect.rect.height * 0.5f + 30f) continue;
            Destroy(mini.Rect.gameObject);
            miniCards.RemoveAt(j);
        }
    }

    private static bool SegmentTouchesCircle(Vector2 start, Vector2 end,
        Vector2 center, float radius)
    {
        Vector2 travel = end - start;
        float fraction = travel.sqrMagnitude > 0f
            ? Mathf.Clamp01(Vector2.Dot(center - start, travel) / travel.sqrMagnitude) : 0f;
        return (center - (start + travel * fraction)).sqrMagnitude <= radius * radius;
    }

    private void RegisterCrealHit()
    {
        if (selectedSuit != Suit.Diamond) return;
        serialStacks = Mathf.Min(Mathf.Max(1, serialMaxStacks), serialStacks + 1);
        cardThrow.MultiplySpeed(GetCrealSpeedMultiplier());
        PlayAbilitySound(crealAbilitySound);
        if (serialStacks % 3 == 0)
            ShowPopup("크리얼 " + serialStacks + "중첩", cardThrow.Position,
                SuitColors[(int)Suit.Diamond]);
    }

    private float GetCrealSpeedMultiplier()
    {
        return serialSpeedMultiplier + 0.015f * upgradeLevels[(int)Upgrade.Creal];
    }

    private static bool SweptCardTouchesEnemy(Vector2 start, Vector2 end,
        float startAngle, float endAngle, Vector2 halfSize, Vector2 enemyPosition, float enemyRadius)
    {
        Vector2 travel = end - start;
        float broadRadius = halfSize.magnitude + enemyRadius;
        float fraction = travel.sqrMagnitude > 0f
            ? Mathf.Clamp01(Vector2.Dot(enemyPosition - start, travel) / travel.sqrMagnitude) : 0f;
        if ((enemyPosition - (start + travel * fraction)).sqrMagnitude > broadRadius * broadRadius)
            return false;

        int steps = Mathf.Clamp(Mathf.Max(
            Mathf.CeilToInt(travel.magnitude / 18f),
            Mathf.CeilToInt(Mathf.Abs(Mathf.DeltaAngle(startAngle, endAngle)) / 7f)), 1, 128);
        for (int step = 0; step <= steps; step++)
        {
            float t = step / (float)steps;
            if (CardTouchesEnemy(Vector2.Lerp(start, end, t),
                Mathf.LerpAngle(startAngle, endAngle, t), halfSize, enemyPosition, enemyRadius))
                return true;
        }
        return false;
    }

    private static bool CardTouchesEnemy(Vector2 cardPosition, float cardAngle,
        Vector2 halfSize, Vector2 enemyPosition, float enemyRadius)
    {
        Vector2 delta = enemyPosition - cardPosition;
        float radians = cardAngle * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        Vector2 local = new Vector2(delta.x * cosine + delta.y * sine,
            -delta.x * sine + delta.y * cosine);
        Vector2 closest = new Vector2(Mathf.Clamp(local.x, -halfSize.x, halfSize.x),
            Mathf.Clamp(local.y, -halfSize.y, halfSize.y));
        return (local - closest).sqrMagnitude <= enemyRadius * enemyRadius;
    }

    private void ShowCombo()
    {
        comboText.text = combo >= 2 ? combo + " 콤보 " : "";
        comboText.color = combo >= 5 ? new Color(1f, 0.80f, 0.25f) : Color.black;
        comboText.transform.localScale = Vector3.one * (1f + Mathf.Min(combo, 10) * 0.04f);
    }

    private void EndGame()
    {
        gameOver = true;
        if (upgradeTab != null) upgradeTab.gameObject.SetActive(false);
        if (upgradePanel != null) upgradePanel.gameObject.SetActive(false);
        ClearGravityMotes();
        HidePassiveDescription();
        Time.timeScale = 1f;
        suitMenuOpen = false;
        for (int i = 0; i < suitCardRects.Length; i++)
        {
            suitCardRects[i].anchoredPosition = new Vector2(suitCardRects[i].anchoredPosition.x, -80f);
            suitCardVerticalSpeeds[i] = 0f;
            suitCardRaised[i] = false;
            suitCardImages[i].raycastTarget = false;
            suitButtons[i].interactable = false;
        }
        cardThrow.enabled = false;
        rulesText.gameObject.SetActive(false);
        finalScoreText.text = "최종 점수  " + score;
        gameOverPanel.SetActive(true);
        waveText.text = "게임 오버  ·  웨이브 " + wave;
    }

    private void CreateFinalScoreText()
    {
        RectTransform panel = gameOverPanel.transform as RectTransform;
        finalScoreText = MakeText("", panel, 32, Color.white, TextAnchor.MiddleCenter);
        finalScoreText.gameObject.name = "Final Score";
        finalScoreText.rectTransform.sizeDelta = new Vector2(380f, 42f);
        finalScoreText.rectTransform.anchoredPosition = new Vector2(0f, -20f);
        restartButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -80f);
    }

    private void UpdateHud()
    {
        statusText.text = "점수  " + score;
        waveText.text = "웨이브  " + wave + "     적  " + enemies.Count;
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || coins < amount) return false;
        coins -= amount;
        UpdateCoinHud();
        RefreshSkillTree();
        return true;
    }

    private void AddCoins(int amount)
    {
        coins = coins > int.MaxValue - amount ? int.MaxValue : coins + amount;
        UpdateCoinHud();
        RefreshSkillTree();
    }

    private void CreateCoinHud()
    {
        RectTransform hud = statusText.rectTransform.parent as RectTransform;
        coinText = MakeText("", hud, 22, new Color(1f, 0.83f, 0.3f), TextAnchor.MiddleLeft);
        coinText.gameObject.name = "Coin Text";
        RectTransform rect = coinText.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -60f);
        rect.sizeDelta = new Vector2(300f, 36f);
    }

    private void UpdateCoinHud()
    {
        coinText.text = "코인  " + coins;
    }

    private void CreateSkillTree()
    {
        RectTransform tab = CreateRect("Skill Tree Tab", canvasRect,
            new Vector2(220f, 40f), Vector2.zero);
        tab.anchorMin = tab.anchorMax = Vector2.zero;
        tab.pivot = Vector2.zero;
        tab.anchoredPosition = new Vector2(20f, 18f);
        Image tabImage = tab.gameObject.AddComponent<Image>();
        tabImage.sprite = uiSprite;
        tabImage.type = Image.Type.Sliced;
        tabImage.pixelsPerUnitMultiplier = 0.65f;
        tabImage.color = new Color(0.22f, 0.23f, 0.25f, 0.96f);
        tabImage.raycastTarget = false;
        Text tabText = MakeText("스킬 트리  ▲", tab, 20, Color.white, TextAnchor.MiddleCenter);
        Stretch(tabText.rectTransform, 2f);
        upgradeTab = tab;

        RectTransform panel = CreateRect("Skill Tree", canvasRect,
            new Vector2(380f, 304f), Vector2.zero);
        panel.anchorMin = panel.anchorMax = Vector2.zero;
        panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(20f, 58f);
        upgradePanel = panel;
        string[] names =
        {
            "카드 피해량", "카드 속도", "카드 크기", AnaDisplayName, CrealDisplayName,
            TapieDisplayName, ParaDisplayName
        };
        Color[] colors =
        {
            new Color(1f, 0.75f, 0.56f), new Color(0.93f, 0.49f, 0.82f),
            new Color(0.69f, 0.50f, 1f), new Color(0.49f, 0.66f, 1f),
            new Color(1f, 0.86f, 0.38f), new Color(0.53f, 0.88f, 0.43f),
            new Color(0.43f, 0.88f, 0.87f)
        };
        for (int i = 0; i < (int)Upgrade.Count; i++)
        {
            RectTransform row = CreateRect(names[i], panel,
                new Vector2(380f, 34f), new Vector2(190f, 278f - i * 40f));
            row.anchorMin = row.anchorMax = Vector2.zero;
            upgradeRows[i] = row;
            CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            upgradeRowGroups[i] = group;
            Image background = row.gameObject.AddComponent<Image>();
            background.sprite = uiSprite;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 0.75f;
            background.color = new Color(0.22f, 0.23f, 0.25f, 0.96f);
            background.raycastTarget = false;
            Text nameText = MakeText(names[i], row, 18, Color.white, TextAnchor.MiddleLeft);
            nameText.fontStyle = FontStyle.Bold;
            nameText.rectTransform.anchoredPosition = new Vector2(-94f, 0f);
            nameText.rectTransform.sizeDelta = new Vector2(174f, 32f);
            Text levelText = MakeText("", row, 15, Color.white, TextAnchor.MiddleCenter);
            levelText.rectTransform.anchoredPosition = new Vector2(25f, 0f);
            levelText.rectTransform.sizeDelta = new Vector2(66f, 32f);
            upgradeLabels[i] = levelText;
            Text priceText = MakeText("", row, 13, Color.white, TextAnchor.MiddleCenter);
            priceText.rectTransform.anchoredPosition = new Vector2(98f, 0f);
            priceText.rectTransform.sizeDelta = new Vector2(72f, 32f);
            upgradePrices[i] = priceText;
            RectTransform plusRect = CreateRect("Buy " + names[i], row,
                new Vector2(53f, 30f), new Vector2(161f, 0f));
            Image plusImage = plusRect.gameObject.AddComponent<Image>();
            plusImage.sprite = uiSprite;
            plusImage.type = Image.Type.Sliced;
            plusImage.pixelsPerUnitMultiplier = 0.9f;
            plusImage.color = colors[i];
            Button button = plusRect.gameObject.AddComponent<Button>();
            int upgradeIndex = i;
            button.onClick.AddListener(() => BuyUpgrade((Upgrade)upgradeIndex));
            Text plusText = MakeText("+", plusRect, 30,
                new Color(0.18f, 0.19f, 0.22f), TextAnchor.MiddleCenter);
            plusText.fontStyle = FontStyle.Bold;
            Stretch(plusText.rectTransform, 0f);
            upgradeButtons[i] = button;
        }
        panel.gameObject.SetActive(false);
        RefreshSkillTree();
    }

    private void UpdateSkillTreeHover()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 screenPoint = mouse.position.ReadValue();
        bool hoverTab = RectTransformUtility.RectangleContainsScreenPoint(
            upgradeTab, screenPoint, uiCamera);
        bool hoverPanel = upgradePanel.gameObject.activeSelf &&
            RectTransformUtility.RectangleContainsScreenPoint(
                upgradePanel, screenPoint, uiCamera);
        bool show = hoverTab || hoverPanel;
        skillTreeReveal = Mathf.MoveTowards(skillTreeReveal, show ? 1f : 0f,
            Time.unscaledDeltaTime * 3.5f);
        bool visible = show || skillTreeReveal > 0f;
        if (upgradePanel.gameObject.activeSelf != visible) upgradePanel.gameObject.SetActive(visible);
        for (int i = 0; i < upgradeRows.Length; i++)
        {
            float delay = i * 0.055f;
            float progress = Mathf.Clamp01((skillTreeReveal - delay) / (1f - delay));
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            upgradeRows[i].anchoredPosition = new Vector2(
                Mathf.Lerp(-220f, 190f, eased), 278f - i * 40f);
            upgradeRowGroups[i].alpha = eased;
            upgradeRowGroups[i].blocksRaycasts = eased > 0.95f;
        }
    }

    private int GetUpgradeCost(int index)
    {
        return upgradeBaseCost + totalUpgrades * upgradeGlobalCostIncrease +
            upgradeLevels[index] * upgradeLevelCostIncrease;
    }

    private void BuyUpgrade(Upgrade upgrade)
    {
        if (gameOver) return;
        int index = (int)upgrade;
        if (index < 0 || index >= (int)Upgrade.Count ||
            upgradeLevels[index] >= MaxUpgradeLevel) return;
        if (!TrySpendCoins(GetUpgradeCost(index))) return;
        upgradeLevels[index]++;
        totalUpgrades++;
        if (upgrade == Upgrade.Speed || upgrade == Upgrade.Size || upgrade == Upgrade.Taffy)
        {
            float handY = -canvasRect.rect.height * 0.31f;
            cardThrow.Configure(1f + 0.08f * upgradeLevels[(int)Upgrade.Speed],
                1f + 0.06f * upgradeLevels[(int)Upgrade.Size], handY);
            cardThrow.SetOrbitUpgrades(0.25f * upgradeLevels[(int)Upgrade.Taffy],
                0.04f * upgradeLevels[(int)Upgrade.Taffy]);
        }
        if (upgrade == Upgrade.Taffy) visiblePassiveSuit = -1;
        RefreshSkillTree();
    }

    private void RefreshSkillTree()
    {
        for (int i = 0; i < (int)Upgrade.Count; i++)
        {
            bool maxed = upgradeLevels[i] >= MaxUpgradeLevel;
            upgradeLabels[i].text = upgradeLevels[i] + "/" + MaxUpgradeLevel;
            upgradePrices[i].text = maxed ? "최대" : GetUpgradeCost(i) + " 코인";
            upgradeButtons[i].interactable = !maxed && coins >= GetUpgradeCost(i);
        }
    }

    private void CreatePassiveDescription()
    {
        passiveDescriptionPanel = CreateRect("Card Passive Description", canvasRect,
            new Vector2(0f, 104f), Vector2.zero);
        passiveDescriptionPanel.anchorMin = new Vector2(0.08f, 0f);
        passiveDescriptionPanel.anchorMax = new Vector2(0.92f, 0f);
        passiveDescriptionPanel.anchoredPosition = new Vector2(0f, 260f);

        passiveDescriptionText = MakeText("", passiveDescriptionPanel, 21,
            Color.black, TextAnchor.MiddleCenter);
        passiveDescriptionText.gameObject.name = "Passive Text";
        Stretch(passiveDescriptionText.rectTransform, 12f);
        passiveDescriptionPanel.gameObject.SetActive(false);
    }

    private void ShowPassiveDescription(int suit)
    {
        if (suit < 0 || suit >= 4) return;
        if (visiblePassiveSuit != suit)
        {
            switch ((Suit)suit)
            {
                case Suit.Heart:
                    passiveDescriptionText.text =
                        ParaDisplayName + "\n비행 중 카드 주변의 적을 끌어당깁니다. 한 번 끌린 적은 더 넓은 범위에서 끌립니다.";
                    break;
                case Suit.Club:
                    passiveDescriptionText.text = TapieDisplayName + "\n카드를 잡는 범위가 " +
                        taffyGrabRangeMultiplier.ToString("0.#") +
                        "배 넓습니다. 비행 중 클릭하면 마우스 주변을 공전하고 " +
                        "놓으면 더 빠르게 날아갑니다. 공전은 최대 " +
                        cardThrow.MaxOrbitDuration.ToString("0.#") + "초입니다.";
                    break;
                case Suit.Spade:
                    passiveDescriptionText.text = AnaDisplayName + "\n본 카드로 적 " + anaKillsPerBurst +
                        "명 처치하면 작은 카드 " + anaMiniCardCount +
                        "장을 방사형으로 발사합니다. 작은 카드는 화면 끝까지 날아갑니다.";
                    break;
                case Suit.Diamond:
                    passiveDescriptionText.text =
                        CrealDisplayName + "\n적을 맞힐 때마다 속도가 중첩 증가합니다. 비행 제한시간이 없습니다.";
                    break;
            }
            visiblePassiveSuit = suit;
        }
        if (!passiveDescriptionPanel.gameObject.activeSelf)
        {
            passiveDescriptionPanel.gameObject.SetActive(true);
            passiveDescriptionPanel.SetAsLastSibling();
        }
    }

    private void HidePassiveDescription()
    {
        if (passiveDescriptionPanel == null) return;
        passiveDescriptionPanel.gameObject.SetActive(false);
        visiblePassiveSuit = -1;
    }

    private void CreateCoreHealthBar()
    {
        coreText.enabled = false;
        RectTransform backgroundRect = CreateRect("Core Health Bar", coreRect,
            new Vector2(160f, 16f), new Vector2(0f, -82f));
        Image background = backgroundRect.gameObject.AddComponent<Image>();
        background.sprite = uiSprite;
        background.type = Image.Type.Sliced;
        background.pixelsPerUnitMultiplier = 2f;
        background.color = new Color(0.12f, 0.14f, 0.20f, 0.95f);
        background.raycastTarget = false;

        RectTransform fillRect = CreateRect("Health Fill", backgroundRect,
            new Vector2(152f, 8f), Vector2.zero);
        coreHealthFill = fillRect.gameObject.AddComponent<Image>();
        coreHealthFill.sprite = uiSprite;
        coreHealthFill.type = Image.Type.Sliced;
        coreHealthFill.pixelsPerUnitMultiplier = 4f;
        coreHealthFill.color = new Color(0.35f, 0.91f, 0.58f);
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(4f, 0f);
        coreHealthFill.raycastTarget = false;
    }

    private void UpdateCoreHealthBar()
    {
        float healthFraction = Mathf.Clamp01(coreHealth / (float)startingCoreHealth);
        coreHealthFill.rectTransform.sizeDelta = new Vector2(152f * healthFraction, 8f);
        coreHealthFill.color = Color.Lerp(new Color(1f, 0.29f, 0.34f),
            new Color(0.35f, 0.91f, 0.58f), healthFraction);
    }

    private void UpdateCoreRegen()
    {
        if (coreHealth <= 0 || coreHealth >= startingCoreHealth)
        {
            coreRegenTimer = 0f;
            return;
        }

        coreRegenTimer += Time.deltaTime;
        if (coreRegenTimer < coreRegenInterval) return;
        coreRegenTimer = 0f;
        coreHealth++;
        UpdateCoreHealthBar();
    }

    private void UpdateCoreDamageFeedback()
    {
        float dt = Time.unscaledDeltaTime;
        corePulse = Mathf.MoveTowards(corePulse, 0f, dt * 4f);
        coreDamageFlashRemaining = Mathf.Max(0f, coreDamageFlashRemaining - dt);
        float flash = Mathf.Clamp01(coreDamageFlashRemaining /
            Mathf.Max(0.01f, coreDamageFlashSeconds));
        coreImage.color = Color.Lerp(coreBaseColor,
            new Color(1f, 0.12f, 0.12f, coreBaseColor.a), flash);
        coreImage.transform.localScale = Vector3.one * (1f + corePulse * 0.15f);
    }

    private void CreateAimDimmer()
    {
        RectTransform rect = CreateRect("Aim Dimmer", canvasRect, Vector2.zero, Vector2.zero);
        Stretch(rect, 0f);
        rect.SetSiblingIndex(cardThrow.CardRect.GetSiblingIndex());
        aimDimmer = rect.gameObject.AddComponent<Image>();
        aimDimmer.sprite = uiSprite;
        aimDimmer.type = Image.Type.Sliced;
        aimDimmer.pixelsPerUnitMultiplier = 10f;
        aimDimmer.color = new Color(0f, 0f, 0f, 0f);
        aimDimmer.raycastTarget = false;
    }

    private void UpdateAimEffect()
    {
        bool aiming = !gameOver && cardThrow.IsDragging;
        Time.timeScale = hitPauseActive || aiming ? aimTimeScale : 1f;
        if (aimDimmer == null) return;
        Color color = aimDimmer.color;
        color.a = Mathf.MoveTowards(color.a, aiming ? aimDimOpacity : 0f,
            Time.unscaledDeltaTime * 2.5f);
        aimDimmer.color = color;
    }

    private static AudioClip MakeTone(float frequency)
    {
        const int sampleRate = 44100;
        const int samples = 2205;
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate)
                * (1f - t) * (1f - t) * 0.35f;
        }
        AudioClip clip = AudioClip.Create("Card Impact", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void Shake(float strength, float duration)
    {
        shakeStrength = Mathf.Max(shakeStrength, strength);
        shakeTime = Mathf.Max(shakeTime, duration);
    }

    private void UpdateShake()
    {
        if (shakeTime > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            Vector2 offset = UnityEngine.Random.insideUnitCircle * shakeStrength;
            enemyLayer.anchoredPosition = offset;
            coreRect.anchoredPosition = offset;
            shakeStrength = Mathf.MoveTowards(shakeStrength, 0f, Time.unscaledDeltaTime * 90f);
        }
        else
        {
            enemyLayer.anchoredPosition = Vector2.zero;
            coreRect.anchoredPosition = Vector2.zero;
        }
    }

    private IEnumerator HitPause()
    {
        hitPauseActive = true;
        yield return new WaitForSecondsRealtime(0.045f);
        hitPauseActive = false;
        hitPause = null;
    }

    private IEnumerator Burst(Vector2 origin, Color color)
    {
        const int count = 7;
        var pieces = new RectTransform[count];
        var images = new Image[count];
        var directions = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            pieces[i] = CreateRect("Impact Spark", effectLayer, new Vector2(10f, 10f), origin);
            images[i] = pieces[i].gameObject.AddComponent<Image>();
            images[i].color = color;
            images[i].raycastTarget = false;
            float angle = (i + UnityEngine.Random.value * 0.45f) * Mathf.PI * 2f / count;
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        float age = 0f;
        while (age < 0.3f)
        {
            age += Time.unscaledDeltaTime;
            for (int i = 0; i < count; i++)
            {
                pieces[i].anchoredPosition = origin + directions[i] * (20f + age * 160f);
                images[i].color = new Color(color.r, color.g, color.b, 1f - age / 0.3f);
            }
            yield return null;
        }
        for (int i = 0; i < count; i++) Destroy(pieces[i].gameObject);
    }

    private void ShowPopup(string message, Vector2 position, Color color)
    {
        Text label = MakeText(message, effectLayer, 22, color, TextAnchor.MiddleCenter);
        label.fontStyle = FontStyle.Bold;
        label.rectTransform.sizeDelta = new Vector2(220f, 42f);
        label.rectTransform.anchoredPosition = position;
        StartCoroutine(FloatAndFade(label));
    }

    private IEnumerator FloatAndFade(Text label)
    {
        float age = 0f;
        Color start = label.color;
        while (age < 0.6f && label != null)
        {
            age += Time.deltaTime;
            label.rectTransform.anchoredPosition += Vector2.up * (65f * Time.deltaTime);
            label.color = new Color(start.r, start.g, start.b, 1f - age / 0.6f);
            yield return null;
        }
        if (label != null) Destroy(label.gameObject);
    }

    private static RectTransform CreateRect(string name, RectTransform parent, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private Text MakeText(string content, RectTransform parent, int size, Color color, TextAnchor alignment)
    {
        RectTransform rect = CreateRect("Text", parent, new Vector2(220f, 45f), Vector2.zero);
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = uiFont;
        label.text = content;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }
}
