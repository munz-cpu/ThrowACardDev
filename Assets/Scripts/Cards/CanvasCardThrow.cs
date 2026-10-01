using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class CanvasCardThrow : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("씬 참조")]
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private RectTransform shadowRect;
    [SerializeField] private Image shadowImage;

    [Header("던지기 설정 (초당 캔버스 좌표)")]
    [SerializeField, Min(0f)] private float throwMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float maxSpeed = 2200f;
    [SerializeField, Min(1f)] private float maxGrabDistance = 320f;
    [SerializeField, Min(0f)] private float friction = 0.12f;
    [SerializeField, Range(0f, 1f)] private float bounce = 0.85f;
    [SerializeField, Min(0f)] private float spinSpeed = 900f;

    [Header("잡기 범위 표시")]
    [SerializeField] private Sprite grabRangeSprite;
    [SerializeField, Min(0.01f)] private float grabRangeGrowSeconds = 0.25f;
    [SerializeField] private float grabRangeRotationSpeed = 30f;

    [Header("빠른 카드 잔상")]
    [SerializeField, Min(0f)] private float trailMinSpeed = 650f;
    [SerializeField, Min(0.01f)] private float trailSpawnInterval = 0.025f;
    [SerializeField, Min(0.01f)] private float trailLifetime = 0.22f;
    [SerializeField] private Color trailColor = new Color(0.3f, 0.85f, 1f, 0.42f);

    [Header("공전 패시브")]
    [SerializeField, Min(0f)] private float orbitCenterFollowSpeed = 10f;
    [SerializeField, Min(0f)] private float orbitSpeedGainPerSecond = 0.12f;
    [SerializeField, Min(0f)] private float orbitRadiusRestoreSpeed = 220f;
    [SerializeField, Min(0.1f)] private float maxOrbitDuration = 2.5f;
    [SerializeField, Min(1f)] private float orbitReleaseSpeedMultiplier = 1.1f;

    private sealed class TrailGhost
    {
        public RectTransform Rect;
        public Image Image;
        public float Age;
        public bool Active;
    }

    private const int TrailPoolSize = 12;
    private const float GrabRangeStartScale = 0.08f;
    private readonly TrailGhost[] trailGhosts = new TrailGhost[TrailPoolSize];
    private RectTransform grabRangeRect;
    private Image grabRangeImage;
    private float grabRangeGrowElapsed;
    private float trailTimer;
    private int nextTrailGhost;

    public event Action Grabbed;
    public event Action Released;
    public event Action Thrown;
    public event Action WallBounced;
    public event Action OrbitStarted;
    public RectTransform CardRect => cardRect;
    public Image CardImage => cardRect.GetComponent<Image>();
    public Vector2 Position => cardRect.anchoredPosition;
    public float Speed => velocity.magnitude;
    public float TrailMinSpeed => trailMinSpeed;
    public bool IsDragging => dragging;
    public bool IsFlying { get; private set; }
    public bool IsOrbiting => orbiting;
    public float MaxOrbitDuration => maxOrbitDuration + orbitDurationBonus;

    private Vector2 handPosition;
    private Vector2 baseSize;
    private float speedMultiplier = 1f;
    private float grabDistanceMultiplier = 1f;

    private Vector2 velocity;
    private Vector2 pointerOffset;
    private Vector2 grabStartPosition;
    private Vector2 dragVelocity;
    private float lastDragTime;
    private float rotationSpeed;
    private bool dragging;
    private float lift;
    private bool orbitEnabled;
    private bool orbiting;
    private Vector2 orbitCenter;
    private float orbitRadius;
    private float orbitBaseRadius;
    private float orbitRestoreAt;
    private float orbitAngle;
    private float orbitDirection;
    private float orbitStartSpeed;
    private float orbitDuration;
    private float orbitDurationBonus;
    private float orbitReleaseSpeedBonus;
    private Camera canvasCamera;

    private void Awake()
    {
        if (canvasRect == null || cardRect == null || shadowRect == null || shadowImage == null)
        {
            Debug.LogError("CanvasCardThrow의 캔버스, 카드, 그림자 참조를 연결하세요.", this);
            enabled = false;
        }
        else
        {
            handPosition = cardRect.anchoredPosition;
            baseSize = cardRect.sizeDelta;
            Canvas canvas = canvasRect.GetComponent<Canvas>();
            canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            CreateTrailPool();
            CreateGrabRange();
            UpdateAppearance();
        }
    }

    private void OnDisable()
    {
        dragging = false;
        orbiting = false;
        if (grabRangeRect != null) grabRangeRect.gameObject.SetActive(false);
        ClearTrail();
    }

    private void Update()
    {
        if (cardRect == null) return;

        if (dragging)
        {
            if (Time.unscaledTime - lastDragTime > 0.1f) dragVelocity = Vector2.zero;
            UpdateGrabRange(Time.unscaledDeltaTime);
        }
        else
        {
            float dt = Time.deltaTime;
            UpdateOrbit(dt);
            if (!orbiting)
            {
                cardRect.anchoredPosition += velocity * dt;
                cardRect.Rotate(0f, 0f, rotationSpeed * dt);
                float damping = Mathf.Exp(-friction * dt);
                velocity *= damping;
                rotationSpeed *= damping;
                BounceInsideCanvas();
            }
        }

        lift = Mathf.MoveTowards(lift, dragging ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        UpdateAppearance();
        UpdateTrail(Time.deltaTime);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            eventData.pointerCurrentRaycast.gameObject != cardRect.gameObject ||
            !TryGetPointer(eventData, out Vector2 pointer)) return;

        dragging = true;
        orbiting = false;
        orbitDuration = 0f;
        velocity = Vector2.zero;
        rotationSpeed = 0f;
        dragVelocity = Vector2.zero;
        pointerOffset = cardRect.anchoredPosition - pointer;
        grabStartPosition = cardRect.anchoredPosition;
        lastDragTime = Time.unscaledTime;
        eventData.useDragThreshold = false;
        ShowGrabRange();
        Grabbed?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || !TryGetPointer(eventData, out Vector2 pointer)) return;

        Vector2 previousPosition = cardRect.anchoredPosition;
        Vector2 offset = Vector2.ClampMagnitude(pointer + pointerOffset - grabStartPosition,
            maxGrabDistance * grabDistanceMultiplier);
        Vector2 limit = GetCanvasLimit();
        Vector2 target = grabStartPosition + offset;
        cardRect.anchoredPosition = new Vector2(
            Mathf.Clamp(target.x, -limit.x, limit.x),
            Mathf.Clamp(target.y, -limit.y, limit.y));
        float elapsed = Time.unscaledTime - lastDragTime;
        Vector2 movement = cardRect.anchoredPosition - previousPosition;
        if (elapsed > 0.001f && movement.sqrMagnitude > 0.01f)
        {
            dragVelocity = Vector2.ClampMagnitude(
                movement / elapsed, maxSpeed);
            lastDragTime = Time.unscaledTime;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!dragging || eventData.button != PointerEventData.InputButton.Left) return;
        dragging = false;
        orbiting = false;
        grabRangeRect.gameObject.SetActive(false);
        if (Time.unscaledTime - lastDragTime > 0.1f) dragVelocity = Vector2.zero;
        velocity = Vector2.ClampMagnitude(dragVelocity * throwMultiplier * speedMultiplier, maxSpeed);
        rotationSpeed = velocity.sqrMagnitude > 100f
            ? spinSpeed * (velocity.x >= 0f ? -1f : 1f) : 0f;
        if (velocity.sqrMagnitude > 100f)
        {
            IsFlying = true;
            orbitDuration = 0f;
            Thrown?.Invoke();
        }
        else IsFlying = false;
        Released?.Invoke();
    }

    public void Configure(float newSpeedMultiplier, float sizeMultiplier, float handY)
    {
        speedMultiplier = newSpeedMultiplier;
        cardRect.sizeDelta = baseSize * sizeMultiplier;
        shadowRect.sizeDelta = cardRect.sizeDelta;
        handPosition = new Vector2(0f, handY);
        if (!IsFlying && !dragging) cardRect.anchoredPosition = handPosition;
        UpdateAppearance();
    }

    public void SetOrbitEnabled(bool enabled)
    {
        orbitEnabled = enabled;
        if (!enabled) orbiting = false;
    }

    public void SetOrbitUpgrades(float durationBonus, float releaseSpeedBonus)
    {
        orbitDurationBonus = Mathf.Max(0f, durationBonus);
        orbitReleaseSpeedBonus = Mathf.Max(0f, releaseSpeedBonus);
    }

    public void SetGrabDistanceMultiplier(float multiplier)
    {
        grabDistanceMultiplier = Mathf.Max(1f, multiplier);
    }

    public void ReturnToHand(Sprite sprite, Color tint)
    {
        dragging = false;
        orbiting = false;
        orbitDuration = 0f;
        grabRangeRect.gameObject.SetActive(false);
        IsFlying = false;
        velocity = Vector2.zero;
        rotationSpeed = 0f;
        dragVelocity = Vector2.zero;
        cardRect.anchoredPosition = handPosition;
        cardRect.localRotation = Quaternion.identity;
        CardImage.sprite = sprite;
        CardImage.color = tint;
        shadowImage.sprite = sprite;
        lift = 0f;
        ClearTrail();
        UpdateAppearance();
    }

    public void MultiplySpeed(float multiplier)
    {
        velocity = Vector2.ClampMagnitude(velocity * multiplier, maxSpeed);
    }

    private bool TryGetPointer(PointerEventData eventData, out Vector2 pointer)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out pointer)) return false;
        pointer -= canvasRect.rect.center;
        return true;
    }

    private void UpdateOrbit(float dt)
    {
        Mouse mouse = Mouse.current;
        bool holding = orbitEnabled && IsFlying && orbitDuration < MaxOrbitDuration &&
            mouse != null && mouse.leftButton.isPressed;
        if (!holding)
        {
            if (orbiting)
            {
                ReleaseOrbit();
            }
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, mouse.position.ReadValue(), canvasCamera, out Vector2 pointer)) return;
        pointer -= canvasRect.rect.center;

        if (!orbiting)
        {
            orbiting = true;
            orbitCenter = pointer;
            Vector2 radial = cardRect.anchoredPosition - orbitCenter;
            orbitRadius = Mathf.Max(40f, radial.magnitude);
            orbitBaseRadius = orbitRadius;
            orbitRestoreAt = 0f;
            orbitAngle = Mathf.Atan2(radial.y, radial.x);
            orbitDirection = radial.x * velocity.y - radial.y * velocity.x >= 0f ? 1f : -1f;
            orbitStartSpeed = Mathf.Max(velocity.magnitude, 150f);
            OrbitStarted?.Invoke();
        }

        orbitDuration = Mathf.Min(MaxOrbitDuration, orbitDuration + dt);
        orbitCenter = Vector2.Lerp(orbitCenter, pointer,
            1f - Mathf.Exp(-orbitCenterFollowSpeed * dt));
        Vector2 limit = GetCanvasLimit();
        Vector2 centerLimit = Vector2.Max(Vector2.zero, limit - Vector2.one * 24f);
        orbitCenter = new Vector2(
            Mathf.Clamp(orbitCenter.x, -centerLimit.x, centerLimit.x),
            Mathf.Clamp(orbitCenter.y, -centerLimit.y, centerLimit.y));
        float orbitSpeed = Mathf.Min(maxSpeed,
            orbitStartSpeed * (1f + orbitDuration * orbitSpeedGainPerSecond));
        orbitAngle += orbitDirection * Mathf.Min(8f, orbitSpeed / Mathf.Max(40f, orbitRadius)) * dt;
        Vector2 radialDirection = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));
        float allowedRadius = GetAllowedOrbitRadius(orbitCenter, radialDirection, limit);
        if (orbitRadius > allowedRadius)
        {
            orbitRadius = allowedRadius;
            orbitRestoreAt = Time.time + 0.3f;
        }
        else if (Time.time >= orbitRestoreAt)
        {
            orbitRadius = Mathf.Min(orbitBaseRadius,
                Mathf.Min(allowedRadius, orbitRadius + orbitRadiusRestoreSpeed * dt));
        }
        cardRect.anchoredPosition = orbitCenter + radialDirection * orbitRadius;
        cardRect.anchoredPosition = new Vector2(
            Mathf.Clamp(cardRect.anchoredPosition.x, -limit.x, limit.x),
            Mathf.Clamp(cardRect.anchoredPosition.y, -limit.y, limit.y));
        velocity = new Vector2(-radialDirection.y, radialDirection.x) *
            (orbitDirection * orbitSpeed);
        cardRect.Rotate(0f, 0f, rotationSpeed * dt);
        if (orbitDuration >= MaxOrbitDuration)
        {
            ReleaseOrbit();
        }
    }

    private void ReleaseOrbit()
    {
        orbiting = false;
        velocity = Vector2.ClampMagnitude(velocity *
            (orbitReleaseSpeedMultiplier + orbitReleaseSpeedBonus), maxSpeed);
        rotationSpeed = spinSpeed * (velocity.x >= 0f ? -1f : 1f);
    }

    private static float GetAllowedOrbitRadius(Vector2 center, Vector2 direction, Vector2 limit)
    {
        float xDistance = direction.x > 0.001f
            ? (limit.x - center.x) / direction.x
            : direction.x < -0.001f ? (-limit.x - center.x) / direction.x
            : float.PositiveInfinity;
        float yDistance = direction.y > 0.001f
            ? (limit.y - center.y) / direction.y
            : direction.y < -0.001f ? (-limit.y - center.y) / direction.y
            : float.PositiveInfinity;
        return Mathf.Max(0f, Mathf.Min(xDistance, yDistance) - 8f);
    }

    private void CreateGrabRange()
    {
        var rangeObject = new GameObject("Grab Range", typeof(RectTransform), typeof(Image));
        grabRangeRect = rangeObject.GetComponent<RectTransform>();
        grabRangeRect.SetParent(canvasRect, false);
        grabRangeRect.anchorMin = grabRangeRect.anchorMax = new Vector2(0.5f, 0.5f);
        grabRangeRect.sizeDelta = Vector2.one * (maxGrabDistance * grabDistanceMultiplier * 2f);
        grabRangeRect.SetSiblingIndex(cardRect.GetSiblingIndex());
        grabRangeImage = rangeObject.GetComponent<Image>();
        grabRangeImage.sprite = grabRangeSprite;
        grabRangeImage.color = new Color(1f, 1f, 1f, 0.65f);
        grabRangeImage.raycastTarget = false;
        rangeObject.SetActive(false);
    }

    private void ShowGrabRange()
    {
        if (grabRangeSprite == null) return;
        grabRangeRect.anchoredPosition = grabStartPosition;
        grabRangeRect.sizeDelta = Vector2.one * (maxGrabDistance * grabDistanceMultiplier * 2f);
        grabRangeGrowElapsed = 0f;
        grabRangeRect.localScale = Vector3.one * GrabRangeStartScale;
        grabRangeRect.localRotation = Quaternion.identity;
        grabRangeImage.sprite = grabRangeSprite;
        grabRangeRect.gameObject.SetActive(true);
    }

    private void UpdateGrabRange(float unscaledDeltaTime)
    {
        if (!grabRangeRect.gameObject.activeSelf) return;
        grabRangeGrowElapsed += unscaledDeltaTime;
        float progress = Mathf.Clamp01(grabRangeGrowElapsed / Mathf.Max(0.01f, grabRangeGrowSeconds));
        float easedProgress = 1f - (1f - progress) * (1f - progress) * (1f - progress);
        float scale = Mathf.Lerp(GrabRangeStartScale, 1f, easedProgress);
        grabRangeRect.localScale = Vector3.one * scale;
        grabRangeRect.Rotate(0f, 0f, grabRangeRotationSpeed * unscaledDeltaTime);
    }

    private void BounceInsideCanvas()
    {
        Vector2 limit = GetCanvasLimit();
        Vector2 pos = cardRect.anchoredPosition;
        if (pos.x < -limit.x)
        {
            pos.x = -limit.x;
            if (velocity.x < 0f) { velocity.x = -velocity.x * bounce; WallBounced?.Invoke(); }
        }
        else if (pos.x > limit.x)
        {
            pos.x = limit.x;
            if (velocity.x > 0f) { velocity.x = -velocity.x * bounce; WallBounced?.Invoke(); }
        }
        if (pos.y < -limit.y)
        {
            pos.y = -limit.y;
            if (velocity.y < 0f) { velocity.y = -velocity.y * bounce; WallBounced?.Invoke(); }
        }
        else if (pos.y > limit.y)
        {
            pos.y = limit.y;
            if (velocity.y > 0f) { velocity.y = -velocity.y * bounce; WallBounced?.Invoke(); }
        }
        cardRect.anchoredPosition = pos;
    }

    private Vector2 GetCanvasLimit()
    {
        float angle = cardRect.localEulerAngles.z * Mathf.Deg2Rad;
        float c = Mathf.Abs(Mathf.Cos(angle));
        float s = Mathf.Abs(Mathf.Sin(angle));
        Vector2 half = cardRect.rect.size * 0.5f;
        Vector2 extent = new Vector2(c * half.x + s * half.y, s * half.x + c * half.y);
        return Vector2.Max(Vector2.zero, canvasRect.rect.size * 0.5f - extent);
    }

    private void UpdateAppearance()
    {
        cardRect.localScale = Vector3.one * (1f + lift * 0.07f);
        shadowRect.anchoredPosition = cardRect.anchoredPosition + new Vector2(8f + lift * 14f, -10f - lift * 17f);
        shadowRect.localRotation = cardRect.localRotation;
        shadowRect.localScale = Vector3.one * (1f + lift * 0.035f);
        shadowImage.color = new Color(0f, 0f, 0f, 0.12f + lift * 0.16f);
    }

    private void CreateTrailPool()
    {
        for (int i = 0; i < trailGhosts.Length; i++)
        {
            var obj = new GameObject("Card Trail " + (i + 1), typeof(RectTransform), typeof(Image));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(canvasRect, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = cardRect.sizeDelta;
            rect.SetSiblingIndex(cardRect.GetSiblingIndex());
            Image image = obj.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            obj.SetActive(false);
            trailGhosts[i] = new TrailGhost { Rect = rect, Image = image };
        }
    }

    private void UpdateTrail(float dt)
    {
        for (int i = 0; i < trailGhosts.Length; i++)
        {
            TrailGhost ghost = trailGhosts[i];
            if (!ghost.Active) continue;
            ghost.Age += dt;
            if (ghost.Age >= trailLifetime)
            {
                ghost.Active = false;
                ghost.Rect.gameObject.SetActive(false);
                continue;
            }
            Color color = trailColor;
            color.a *= 1f - ghost.Age / trailLifetime;
            ghost.Image.color = color;
        }

        if (!IsFlying || dragging || Speed < trailMinSpeed)
        {
            trailTimer = 0f;
            return;
        }

        trailTimer += dt;
        if (trailTimer < trailSpawnInterval) return;
        trailTimer = 0f;
        TrailGhost next = trailGhosts[nextTrailGhost];
        nextTrailGhost = (nextTrailGhost + 1) % trailGhosts.Length;
        next.Active = true;
        next.Age = 0f;
        next.Rect.sizeDelta = cardRect.sizeDelta;
        next.Rect.anchoredPosition = cardRect.anchoredPosition;
        next.Rect.localRotation = cardRect.localRotation;
        next.Rect.localScale = cardRect.localScale;
        next.Image.sprite = CardImage.sprite;
        next.Image.color = trailColor;
        next.Rect.gameObject.SetActive(true);
    }

    private void ClearTrail()
    {
        trailTimer = 0f;
        for (int i = 0; i < trailGhosts.Length; i++)
        {
            if (trailGhosts[i] == null) continue;
            trailGhosts[i].Active = false;
            trailGhosts[i].Rect.gameObject.SetActive(false);
        }
    }
}
