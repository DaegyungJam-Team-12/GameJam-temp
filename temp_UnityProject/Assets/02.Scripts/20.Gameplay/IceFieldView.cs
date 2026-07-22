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
    /// handles mouse click and hold input, and renders each ice as a coloured circle.
    /// Attach to a GameObject in siyeon.unity.
    /// </summary>
    public sealed class IceFieldView : MonoBehaviour
    {
        private const float ReferenceWidth = 960f;
        private const float ReferenceHeight = 540f;
        private const int RuntimeSpriteSize = 64;
        private const float SpawnMargin = 56f;

        // --- Direct attack defaults (D01 level 0, D02 level 0) ---
        private const float BaseClickDamage = 1f;
        private const float BaseHoldAttacksPerSecond = 5f; // 5 + D02Level * 2, max 11
        private const float CriticalChance = 0.05f;        // 5%
        private const float CriticalMultiplier = 3f;       // x3 damage

        private static readonly Rect[] ProtectedSpawnAreas =
        {
            // Spec coordinates use a top-left origin; gameplay reference coordinates use bottom-left.
            new Rect(0f, 476f, 252f, 64f),
            new Rect(384f, 476f, 192f, 64f),
            new Rect(888f, 476f, 72f, 64f),
            new Rect(280f, 0f, 400f, 135f),
        };

        [Serializable]
        public struct SandboxSettings
        {
            [Header("Spawn Weights (0~100)")]
            [Range(0, 100)] public int weightT1;
            [Range(0, 100)] public int weightT2;
            [Range(0, 100)] public int weightT3;

            [Header("Support Attack (GP-06)")]
            public bool enableSupportAttack;
            [Range(1, 20)] public int requiredHits;
            [Range(0, 5)] public int additionalTargets;
            public bool prioritizeSpecialIce;

            [Header("Chain Destruction (GP-07)")]
            public bool enableOverkill;
            public bool enableHullFragment;
            public bool enableIceCollapse;

            [Header("Debug")]
            public bool enableDebugText;
        }

        private struct FloatingText
        {
            public Vector2 Position;
            public string Text;
            public Color Color;
            public double ExpiryTime;
        }

        [SerializeField] private Camera? sceneCamera;
        [SerializeField] private long stageId = 1L;
        [SerializeField] private SandboxSettings sandboxSettings = new SandboxSettings
        {
            weightT1 = 100,
            weightT2 = 0,
            weightT3 = 0,
            enableSupportAttack = true,
            requiredHits = 12,
            additionalTargets = 2,
            prioritizeSpecialIce = true,
            enableOverkill = true,
            enableHullFragment = true,
            enableIceCollapse = true,
            enableDebugText = true
        };

        private IceField? field;
        private IceFieldConfig? config;
        private HoldInputHandler? holdInput;
        private readonly List<SpriteRenderer> visuals = new();
        private readonly List<FloatingText> floatingTexts = new();
        private Sprite? iceSprite;
        private double stageStartedAt;
        private IStageClock? injectedClock;
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

        public void ResetStage()
        {
            if (field == null)
            {
                throw new InvalidOperationException("IceField is not initialized yet.");
            }

            field.Initialize(0d);
            RefreshAllVisuals();
        }

        private void Awake()
        {
            stageStartedAt = Time.timeAsDouble;

            config = CreateDefaultConfig();
            var idGenerator = new IceIdGenerator();
            var spawnBounds = new Rect(
                SpawnMargin,
                SpawnMargin,
                ReferenceWidth - SpawnMargin * 2f,
                ReferenceHeight - SpawnMargin * 2f);
            var positioner = new IceSpawnPositioner(
                spawnBounds,
                config.MinimumSpawnDistanceReferencePixels,
                ProtectedSpawnAreas,
                config.HitRadiusReferencePixels);
            var criticalStrike = new CriticalStrike(CriticalChance, CriticalMultiplier);
            activeClock = injectedClock ?? new DummyClock(this);

            // [GP-06] Inspector 설정에 따른 보조 파쇄 적용
            var supportConfig = new SupportAttackConfig(
                enabled: sandboxSettings.enableSupportAttack,
                requiredDirectHitCount: sandboxSettings.requiredHits,
                primaryDamageMultiplier: 1.0f,
                additionalTargetCount: sandboxSettings.additionalTargets,
                additionalDamageMultiplier: 0.7f,
                prioritizeSpecialIce: sandboxSettings.prioritizeSpecialIce,
                specialIceDamageMultiplier: 2.0f);

            // [GP-07] 연쇄 파괴 적용
            var chainConfig = new ChainDestructionConfig(
                enableOverkill: sandboxSettings.enableOverkill,
                overkillTransferRatio: 0.5f,
                enableHullFragment: sandboxSettings.enableHullFragment,
                hullFragmentDamageMultiplier: 0.25f,
                hullFragmentRadius: 90f,
                crystalShardCount: 5,
                crackDamageMultiplier: 1.0f,
                crackRadius: 120f,
                enableIceCollapse: sandboxSettings.enableIceCollapse,
                iceCollapseDamageMultiplier: 1.5f,
                iceCollapseRadius: 140f);

            field = new IceField(stageId, config, idGenerator, positioner, activeClock, criticalStrike, supportConfig, chainConfig);
            field.DamageApplied += HandleDamageApplied;
            field.IceDestroyed += HandleIceDestroyed;
            field.IceRespawned += HandleIceRespawned;

            holdInput = new HoldInputHandler(BaseHoldAttacksPerSecond);

            iceSprite = CreateIceSprite();
            field.Initialize(0d);
        }

        private void Start()
        {
            sceneCamera ??= Camera.main;
            CreateAllVisuals();
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
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (field == null || sceneCamera == null || mouse == null || holdInput == null)
            {
                return;
            }

            var isPressed = mouse.leftButton.isPressed;
            var wasPressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            var ticks = holdInput.Update(isPressed, wasPressedThisFrame, Time.deltaTime);

            if (ticks <= 0)
            {
                return;
            }

            var screenPos = mouse.position.ReadValue();
            var refPos = ScreenToReference(screenPos);
            var effectType = wasPressedThisFrame ? EffectType.Click : EffectType.Hold;
            var elapsed = activeClock?.StageElapsedSeconds ?? Time.timeAsDouble - stageStartedAt;

            for (var i = 0; i < ticks; i++)
            {
                field.ApplyClickAt(refPos, BaseClickDamage, effectType, elapsed);
                // After the first tick, subsequent ones are Hold.
                effectType = EffectType.Hold;
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

            var worldRadius = ResolveWorldRadius();
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

        private float ResolveWorldRadius()
        {
            if (sceneCamera == null || !sceneCamera.orthographic)
            {
                return 0.55f;
            }

            var worldHeight = sceneCamera.orthographicSize * 2f;
            var displaySize = config?.HitRadiusReferencePixels * 2f ?? 112f;
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

            if (sandboxSettings.enableDebugText)
            {
                var color = Color.white;
                if (e.WasCritical) color = Color.yellow;
                else if (e.EffectType == EffectType.SupportShot) color = Color.cyan;
                else if (e.EffectType == EffectType.Overkill) color = new Color(1f, 0.5f, 0f); // Orange
                else if (e.EffectType == EffectType.HullFragment) color = Color.magenta;
                else if (e.EffectType == EffectType.IceCollapse) color = Color.red;
                else if (e.EffectType == EffectType.CrystalShard || e.EffectType == EffectType.CrackExplosion) color = Color.green;

                var text = e.WasCritical ? $"Critical {e.Damage:F0}!" : $"{e.Damage:F0}";
                if (e.EffectType != EffectType.Click && e.EffectType != EffectType.Hold)
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
        }

        private void OnGUI()
        {
            if (!sandboxSettings.enableDebugText || sceneCamera == null) return;

            var currentTime = Time.timeAsDouble;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
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

                // Move up over time
                var floatOffset = new Vector2(0, (float)(1.0 - lifeRemaining) * 50f);
                var screenPos = sceneCamera.WorldToScreenPoint(ReferenceToWorld(ft.Position));
                
                // Unity GUI y is inverted
                var guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y) - floatOffset;
                
                style.normal.textColor = ft.Color;
                GUI.Label(new Rect(guiPos.x - 100, guiPos.y - 20, 200, 40), ft.Text, style);
            }
        }

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

            // Inspector에서 설정한 가중치를 사용합니다 (0인 것은 제외)
            var weightList = new List<IceSpawnWeight>();
            if (sandboxSettings.weightT1 > 0) weightList.Add(new IceSpawnWeight(IceTier.T1, sandboxSettings.weightT1));
            if (sandboxSettings.weightT2 > 0) weightList.Add(new IceSpawnWeight(IceTier.T2, sandboxSettings.weightT2));
            if (sandboxSettings.weightT3 > 0) weightList.Add(new IceSpawnWeight(IceTier.T3, sandboxSettings.weightT3));

            if (weightList.Count == 0)
            {
                weightList.Add(new IceSpawnWeight(IceTier.T1, 100));
            }

            var specialDefinitions = new[]
            {
                new SpecialIceDefinition(SpecialIceType.Crystal, 0.025f, IceTier.T2, 1.0f, 4.0f),
                new SpecialIceDefinition(SpecialIceType.Crack, 0.020f, IceTier.T1, 0.6f, 1.0f),
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
