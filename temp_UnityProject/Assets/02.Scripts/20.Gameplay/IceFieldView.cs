#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.State;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// MonoBehaviour that creates an <see cref="IceField"/> with 20 ice blocks (T1~T3),
    /// applies automatic cursor-area attacks and renders ice plus the cursor range indicator.
    /// Attach to a GameObject in siyeon.unity.
    /// </summary>
    public sealed class IceFieldView : MonoBehaviour
    {
        private const float ReferenceWidth = 960f;
        private const float ReferenceHeight = 540f;
        private const int RuntimeSpriteSize = 64;
        private const float SpawnMargin = 56f;

        // --- Direct attack defaults (D01 level 0, D02 level 0) ---
        private const float BaseDirectDamage = 1f;
        private const float BaseAttackTicksPerSecond = 5f;
        private const float BaseCursorRadiusReferencePixels = 56f;
        private const float CriticalChance = 0.05f;        // 5%
        private const float CriticalMultiplier = 3f;       // x3 damage
        private const float CursorRingDegreesPerSecond = 60f;

        private static readonly Rect[] ProtectedSpawnAreas =
        {
            // Spec coordinates use a top-left origin; gameplay reference coordinates use bottom-left.
            new Rect(0f, 476f, 252f, 64f),
            new Rect(384f, 476f, 192f, 64f),
            new Rect(888f, 476f, 72f, 64f),
            new Rect(280f, 0f, 400f, 135f),
        };

#if UNITY_EDITOR
        public bool EnableDebugText { get; set; }

        private struct FloatingText
        {
            public Vector2 Position;
            public string Text;
            public Color Color;
            public double ExpiryTime;
        }
#endif

        [SerializeField] private Camera? sceneCamera;
        [SerializeField] private long stageId = 1L;

        private IceField? field;
        private IceFieldConfig? config;
        private DirectAttackConfig? directAttackConfig;
        private AttackTickScheduler? attackTickScheduler;
        private readonly List<SpriteRenderer> visuals = new();
#if UNITY_EDITOR
        private readonly List<FloatingText> floatingTexts = new();
#endif
        private Sprite? iceSprite;
        private Sprite? cursorRingSprite;
        private Transform? cursorRingRoot;
        private Transform? cursorRingRotator;
        private double stageStartedAt;
        private IStageClock? injectedClock;
        private CombatConfig? injectedCombatConfig;
        private IStageClock? activeClock;

        private sealed class DummyClock : IStageClock
        {
            private readonly IceFieldView view;
            public DummyClock(IceFieldView view) => this.view = view;
            
            public GamePhase Phase => GamePhase.Playing;
            public double DurationSeconds => 60d;
            public double StageElapsedSeconds => Time.timeAsDouble - view.stageStartedAt;
            public double RemainingSeconds => Math.Max(0d, DurationSeconds - StageElapsedSeconds);
            public bool IsPaused => false;
        }

        /// <summary>Live combat-event source for integration wiring (INT-01/INT-02).</summary>
        public ICombatEventSource Source =>
            field ?? throw new InvalidOperationException("IceField is not initialized yet.");

        public void InjectStageClock(IStageClock clock)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            if (field != null)
            {
                throw new InvalidOperationException("Stage clock must be injected before IceFieldView.Awake.");
            }

            injectedClock = clock;
        }

        /// <summary>
        /// Supplies the immutable maintenance-derived values for the next stage field.
        /// The values are applied together by <see cref="ResetStage"/> before play begins.
        /// </summary>
        public void InjectCombatConfig(CombatConfig combatConfig)
        {
            if (combatConfig == null)
            {
                throw new ArgumentNullException(nameof(combatConfig));
            }

            if (activeClock?.Phase == GamePhase.Playing)
            {
                throw new InvalidOperationException("Combat config cannot change while a stage is playing.");
            }

            injectedCombatConfig = combatConfig;
        }

        public void ResetStage()
        {
            if (field == null)
            {
                throw new InvalidOperationException("IceField is not initialized yet.");
            }

            ApplyCombatConfig();
            field.Initialize(0d);
            RefreshAllVisuals();
            attackTickScheduler?.Reset();
        }

        private void Awake()
        {
            stageStartedAt = Time.timeAsDouble;
            activeClock = injectedClock ?? new DummyClock(this);
            ApplyCombatConfig();
            field.DamageApplied += HandleDamageApplied;
            field.IceDestroyed += HandleIceDestroyed;
            field.IceRespawned += HandleIceRespawned;

            iceSprite = CreateIceSprite();
            field.Initialize(0d);
        }

        private void ApplyCombatConfig()
        {
            config = injectedCombatConfig?.IceField ?? CreateDefaultConfig();
            directAttackConfig = injectedCombatConfig?.DirectAttack ?? new DirectAttackConfig(
                BaseDirectDamage,
                BaseAttackTicksPerSecond,
                BaseCursorRadiusReferencePixels,
                CriticalChance,
                CriticalMultiplier);
            var spawnBounds = new Rect(
                SpawnMargin,
                SpawnMargin,
                ReferenceWidth - SpawnMargin * 2f,
                ReferenceHeight - SpawnMargin * 2f);
            var positioner = new IceSpawnPositioner(
                spawnBounds,
                config.MinimumSpawnDistanceReferencePixels,
                ProtectedSpawnAreas,
                config.IceCollisionRadiusReferencePixels);
            var criticalStrike = new CriticalStrike(
                directAttackConfig.CriticalChance,
                directAttackConfig.CriticalDamageMultiplier);
            var supportConfig = injectedCombatConfig?.SupportAttack ?? new SupportAttackConfig(
                enabled: false,
                requiredDirectHitCount: 12,
                primaryDamageMultiplier: 1.0f,
                additionalTargetCount: 2,
                additionalDamageMultiplier: 0.7f,
                prioritizeSpecialIce: true,
                specialIceDamageMultiplier: 2.0f);
            var chainConfig = injectedCombatConfig?.ChainEffect ?? new ChainEffectConfig(
                overkillEnabled: true,
                overkillTransferMultiplier: 0.5f,
                hullFragmentDamageMultiplier: 0f,
                hullFragmentRadiusReferencePixels: 90f,
                crystalShardCount: 5,
                crackDamageMultiplier: 3.0f,
                crackRadiusReferencePixels: 120f,
                iceCollapseEnabled: false,
                iceCollapseRequiredDestroyCount: 5,
                iceCollapseDamageMultiplier: 1.5f,
                iceCollapseRadiusReferencePixels: 140f,
                maxChainDepth: 3);

            if (field == null)
            {
                field = new IceField(
                    stageId,
                    config,
                    new IceIdGenerator(),
                    positioner,
                    activeClock!,
                    criticalStrike,
                    supportConfig,
                    chainConfig);
            }
            else
            {
                field.Reconfigure(config, positioner, criticalStrike, supportConfig, chainConfig);
            }

            attackTickScheduler = new AttackTickScheduler(directAttackConfig.AttackTicksPerSecond);
        }

        private void Start()
        {
            sceneCamera ??= Camera.main;
            CreateAllVisuals();
            CreateCursorRing();
        }

        private void OnDisable()
        {
            if (cursorRingRoot != null)
            {
                cursorRingRoot.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (field == null)
            {
                return;
            }

            field.DamageApplied -= HandleDamageApplied;
            field.IceDestroyed -= HandleIceDestroyed;
            field.IceRespawned -= HandleIceRespawned;

            if (iceSprite != null)
            {
                Destroy(iceSprite.texture);
                Destroy(iceSprite);
            }

            if (cursorRingSprite != null)
            {
                Destroy(cursorRingSprite.texture);
                Destroy(cursorRingSprite);
            }
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (field == null || sceneCamera == null || mouse == null ||
                attackTickScheduler == null || directAttackConfig == null)
            {
                return;
            }

            var screenPosition = mouse.position.ReadValue();
            var cursorIsOnScreen = IsInsideScreen(screenPosition);
            UpdateCursorRing(screenPosition, cursorIsOnScreen);

            if (activeClock == null || activeClock.Phase != GamePhase.Playing || activeClock.IsPaused)
            {
                attackTickScheduler.Reset();
                return;
            }

            var ticks = attackTickScheduler.Update(Time.deltaTime);
            if (!cursorIsOnScreen || ticks <= 0)
            {
                return;
            }

            var refPos = ScreenToReference(screenPosition);
            var elapsed = activeClock?.StageElapsedSeconds ?? Time.timeAsDouble - stageStartedAt;

            for (var i = 0; i < ticks; i++)
            {
                field.ApplyAreaTickAt(
                    refPos,
                    directAttackConfig.CursorRadiusReferencePixels,
                    directAttackConfig.CurrentDirectDamage,
                    elapsed);
            }
        }

        // --- Visual helpers ---

        private void CreateAllVisuals()
        {
            if (field == null || iceSprite == null)
            {
                return;
            }

            for (var i = 0; i < field.ActiveIce.Count; i++)
            {
                var ice = field.ActiveIce[i];
                var renderer = CreateVisual(ice, i);
                visuals.Add(renderer);
            }
        }

        private SpriteRenderer CreateVisual(IceInstance ice, int index)
        {
            var child = new GameObject($"Ice_{index}");
            child.transform.SetParent(transform, false);

            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = iceSprite;
            renderer.sortingOrder = 10;

            PositionVisual(renderer, ice);
            UpdateVisualColor(renderer, ice);
            return renderer;
        }

        private void PositionVisual(SpriteRenderer renderer, IceInstance ice)
        {
            if (sceneCamera == null)
            {
                return;
            }

            var worldPos = ReferenceToWorld(ice.ReferencePosition);
            renderer.transform.position = worldPos;

            var worldRadius = ResolveWorldRadius(config?.IceCollisionRadiusReferencePixels ?? 56f);
            renderer.transform.localScale = Vector3.one * (worldRadius * 2f);
        }

        private void UpdateVisualColor(SpriteRenderer renderer, IceInstance ice)
        {
            var hpRatio = ice.RemainingHp / ice.MaxHp;
            if (ice.IsDestroyed)
            {
                renderer.color = new Color(0.7f, 0.85f, 0.9f, 0.3f);
                return;
            }

            // Tier-based colors from spec: T1=white, T2=sky blue, T3=cobalt blue.
            var tierColor = ice.Tier switch
            {
                IceTier.T2 => new Color(0.53f, 0.81f, 0.98f, 1f), // Sky blue
                IceTier.T3 => new Color(0.24f, 0.35f, 0.67f, 1f), // Cobalt blue
                _          => new Color(0.87f, 0.98f, 1f, 1f),    // White/light blue (T1)
            };

            if (ice.SpecialType == SpecialIceType.Crystal)
            {
                tierColor = new Color(1f, 0.92f, 0.5f, 1f); // Golden yellow for Crystal
            }
            else if (ice.SpecialType == SpecialIceType.Crack)
            {
                tierColor = new Color(1f, 0.4f, 0.4f, 1f); // Reddish for Crack
            }

            renderer.color = Color.Lerp(tierColor * 0.8f, tierColor, hpRatio);
        }

        private void RefreshVisual(int index)
        {
            if (field == null || index < 0 || index >= visuals.Count)
            {
                return;
            }

            var ice = field.ActiveIce[index];
            var renderer = visuals[index];
            PositionVisual(renderer, ice);
            UpdateVisualColor(renderer, ice);
        }

        private void RefreshAllVisuals()
        {
            for (var i = 0; i < visuals.Count; i++)
            {
                RefreshVisual(i);
            }
        }

        // --- Coordinate conversion ---

        private Vector2 ScreenToReference(Vector2 screenPos)
        {
            if (sceneCamera == null)
            {
                return screenPos;
            }

            return new Vector2(
                screenPos.x / Screen.width * ReferenceWidth,
                screenPos.y / Screen.height * ReferenceHeight);
        }

        private Vector3 ReferenceToWorld(Vector2 refPos)
        {
            if (sceneCamera == null)
            {
                return Vector3.zero;
            }

            var screenPos = new Vector3(
                refPos.x / ReferenceWidth * Screen.width,
                refPos.y / ReferenceHeight * Screen.height,
                -sceneCamera.transform.position.z);

            return sceneCamera.ScreenToWorldPoint(screenPos);
        }

        private static bool IsInsideScreen(Vector2 screenPosition)
        {
            return screenPosition.x >= 0f && screenPosition.x <= Screen.width &&
                   screenPosition.y >= 0f && screenPosition.y <= Screen.height;
        }

        private void CreateCursorRing()
        {
            if (cursorRingRoot != null || sceneCamera == null || directAttackConfig == null)
            {
                return;
            }

            var root = new GameObject("CursorAreaRing");
            root.transform.SetParent(transform, false);
            cursorRingRoot = root.transform;

            var rotatingChild = new GameObject("RotatingDashes");
            rotatingChild.transform.SetParent(cursorRingRoot, false);
            cursorRingRotator = rotatingChild.transform;

            cursorRingSprite = CreateCursorRingSprite();
            var renderer = rotatingChild.AddComponent<SpriteRenderer>();
            renderer.sprite = cursorRingSprite;
            renderer.sortingOrder = 20;
            renderer.color = new Color(0.08f, 0.16f, 0.18f, 0.9f);

            var worldRadius = ResolveWorldRadius(directAttackConfig.CursorRadiusReferencePixels);
            cursorRingRotator.localScale = Vector3.one * (worldRadius * 2f);
            root.SetActive(false);
        }

        private void UpdateCursorRing(Vector2 screenPosition, bool cursorIsOnScreen)
        {
            if (cursorRingRoot == null || cursorRingRotator == null || directAttackConfig == null)
            {
                return;
            }

            if (cursorRingRoot.gameObject.activeSelf != cursorIsOnScreen)
            {
                cursorRingRoot.gameObject.SetActive(cursorIsOnScreen);
            }

            if (!cursorIsOnScreen)
            {
                return;
            }

            cursorRingRoot.position = ReferenceToWorld(ScreenToReference(screenPosition));
            var worldRadius = ResolveWorldRadius(directAttackConfig.CursorRadiusReferencePixels);
            cursorRingRotator.localScale = Vector3.one * (worldRadius * 2f);
            cursorRingRotator.Rotate(Vector3.back, CursorRingDegreesPerSecond * Time.deltaTime);
        }

        private float ResolveWorldRadius(float referenceRadius)
        {
            if (sceneCamera == null || !sceneCamera.orthographic)
            {
                return 0.55f;
            }

            var worldHeight = sceneCamera.orthographicSize * 2f;
            var displaySize = referenceRadius * 2f;
            return worldHeight * (displaySize / ReferenceHeight) * 0.5f;
        }

        // --- Event handlers ---

        private void HandleDamageApplied(DamageAppliedEvent e)
        {
            // Find and update the visual for the damaged ice.
            if (field == null)
            {
                return;
            }

            for (var i = 0; i < field.ActiveIce.Count; i++)
            {
                if (field.ActiveIce[i].IceInstanceId == e.IceInstanceId)
                {
                    UpdateVisualColor(visuals[i], field.ActiveIce[i]);
                    break;
                }
            }

#if UNITY_EDITOR
            if (EnableDebugText)
            {
                var color = Color.white;
                if (e.WasCritical) color = Color.yellow;
                else if (e.EffectType == EffectType.SupportShot) color = Color.cyan;
                else if (e.EffectType == EffectType.Overkill) color = new Color(1f, 0.5f, 0f); // Orange
                else if (e.EffectType == EffectType.HullFragment) color = Color.magenta;
                else if (e.EffectType == EffectType.IceCollapse) color = Color.red;
                else if (e.EffectType == EffectType.CrystalShard || e.EffectType == EffectType.CrackExplosion) color = Color.green;

                var text = e.WasCritical ? $"Critical {e.Damage:F0}!" : $"{e.Damage:F0}";
                if (e.EffectType != EffectType.CursorAreaPulse &&
                    e.EffectType != EffectType.Click &&
                    e.EffectType != EffectType.Hold)
                {
                    text += $" ({e.EffectType})";
                }

                floatingTexts.Add(new FloatingText
                {
                    Position = e.ReferencePosition,
                    Text = text,
                    Color = color,
                    ExpiryTime = Time.timeAsDouble + 1.0 // 1 second duration
                });
            }
#endif
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!EnableDebugText || sceneCamera == null) return;

            var currentTime = Time.timeAsDouble;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            for (var i = floatingTexts.Count - 1; i >= 0; i--)
            {
                var ft = floatingTexts[i];
                var lifeRemaining = ft.ExpiryTime - currentTime;
                if (lifeRemaining <= 0)
                {
                    floatingTexts.RemoveAt(i);
                    continue;
                }

                var floatOffset = new Vector2(0, (float)(1.0 - lifeRemaining) * 50f);
                var worldPos = ReferenceToWorld(ft.Position);
                var screenPos = sceneCamera.WorldToScreenPoint(worldPos);

                if (screenPos.z > 0)
                {
                    var guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y) - floatOffset;
                    var rect = new Rect(guiPos.x - 100, guiPos.y - 20, 200f, 40f);
                    style.normal.textColor = ft.Color;
                    GUI.Label(rect, ft.Text, style);
                }
            }
        }
#endif

        private void HandleIceDestroyed(IceDestroyedEvent e)
        {
        }

        private void HandleIceRespawned(int index)
        {
            RefreshVisual(index);
        }

        // --- Sprite & Config creation ---

        private IceFieldConfig CreateDefaultConfig()
        {
            // T1~T3 definitions from ice_types.md spec.
            var iceDefinitions = new[]
            {
                new IceDefinition(IceTier.T1, "백빙", 10f, 10L),
                new IceDefinition(IceTier.T2, "청빙", 60f, 80L),
                new IceDefinition(IceTier.T3, "심빙", 360f, 700L),
            };

            var weightList = new List<IceSpawnWeight>
            {
                new IceSpawnWeight(IceTier.T1, 100)
            };

            var specialDefinitions = new[]
            {
                new SpecialIceDefinition(SpecialIceType.Crystal, 0.025f, IceTier.T2, 1.0f, 4.0f),
                new SpecialIceDefinition(SpecialIceType.Crack, 0.02f, IceTier.T1, 0.6f, 1.0f),
            };

            return new IceFieldConfig(
                maxActiveIceCount: 20,
                maxSpecialIceCount: 2,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 120f,
                respawnProtectionSeconds: 0.25f,
                iceDefinitions: iceDefinitions,
                spawnWeights: weightList.ToArray(),
                specialDefinitions: specialDefinitions);
        }

        private static Sprite CreateCursorRingSprite()
        {
            const int textureSize = 128;
            const int dashCount = 20;
            const float dashFill = 0.52f;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            var outerRadius = textureSize * 0.47f;
            var innerRadius = textureSize * 0.40f;
            for (var y = 0; y < textureSize; y++)
            {
                for (var x = 0; x < textureSize; x++)
                {
                    var offset = new Vector2(x, y) - center;
                    var radius = offset.magnitude;
                    if (radius < innerRadius || radius > outerRadius)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var angle = Mathf.Atan2(offset.y, offset.x);
                    if (angle < 0f)
                    {
                        angle += Mathf.PI * 2f;
                    }

                    var dashProgress = angle / (Mathf.PI * 2f) * dashCount;
                    texture.SetPixel(x, y, dashProgress - Mathf.Floor(dashProgress) < dashFill
                        ? Color.white
                        : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0, 0, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize);
        }

        private static Sprite CreateIceSprite()
        {
            var texture = new Texture2D(RuntimeSpriteSize, RuntimeSpriteSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = new Vector2((RuntimeSpriteSize - 1) * 0.5f, (RuntimeSpriteSize - 1) * 0.5f);
            var maxRadius = RuntimeSpriteSize * 0.46f;

            for (var y = 0; y < RuntimeSpriteSize; y++)
            {
                for (var x = 0; x < RuntimeSpriteSize; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance > maxRadius)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var edge = distance / maxRadius;
                    var color = Color.Lerp(
                        new Color(0.87f, 0.98f, 1f, 1f),
                        new Color(0.55f, 0.82f, 0.9f, 1f),
                        edge);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0, 0, RuntimeSpriteSize, RuntimeSpriteSize),
                new Vector2(0.5f, 0.5f),
                RuntimeSpriteSize);
        }
    }
}

