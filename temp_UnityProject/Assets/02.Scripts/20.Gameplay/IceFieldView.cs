#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
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

        [SerializeField] private Camera? sceneCamera;
        [SerializeField] private long stageId = 1L;

        private IceField? field;
        private IceFieldConfig? config;
        private HoldInputHandler? holdInput;
        private readonly List<SpriteRenderer> visuals = new();
        private Sprite? iceSprite;
        private double stageStartedAt;

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
            var positioner = new IceSpawnPositioner(spawnBounds, config.MinimumSpawnDistanceReferencePixels);
            var criticalStrike = new CriticalStrike(CriticalChance, CriticalMultiplier);

            field = new IceField(stageId, config, idGenerator, positioner, criticalStrike);
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
            var elapsed = Time.timeAsDouble - stageStartedAt;

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

            var critLabel = e.WasCritical ? " CRITICAL!" : "";
            Debug.Log($"[GP-03] Damage {e.Damage:F1}{critLabel}, remaining HP {e.RemainingHp:F1}, ice={e.IceInstanceId}.", this);
        }

        private void HandleIceDestroyed(IceDestroyedEvent e)
        {
            Debug.Log($"[GP-03] IceDestroyedEvent ice={e.IceInstanceId} tier={e.Tier}.", this);
        }

        private void HandleIceRespawned(int index)
        {
            RefreshVisual(index);
            if (field != null)
            {
                Debug.Log($"[GP-03] Respawned index={index}, newId={field.ActiveIce[index].IceInstanceId}, tier={field.ActiveIce[index].Tier}.", this);
            }
        }

        // --- Sprite & Config creation ---

        private static IceFieldConfig CreateDefaultConfig()
        {
            // T1~T3 definitions from ice_types.md spec.
            var iceDefinitions = new[]
            {
                new IceDefinition(IceTier.T1, "백빙", 10f, 10L),
                new IceDefinition(IceTier.T2, "청빙", 60f, 80L),
                new IceDefinition(IceTier.T3, "심빙", 360f, 700L),
            };

            // Spawn weights: T1 dominant, T2/T3 gradually mixed in.
            // These weights will be adjusted by upgrade unlocks later.
            var spawnWeights = new[]
            {
                new IceSpawnWeight(IceTier.T1, 70),
                new IceSpawnWeight(IceTier.T2, 25),
                new IceSpawnWeight(IceTier.T3, 5),
            };

            var specialDefinitions = Array.Empty<SpecialIceDefinition>();

            return new IceFieldConfig(
                maxActiveIceCount: 20,
                maxSpecialIceCount: 2,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 120f,
                respawnProtectionSeconds: 0.25f,
                iceDefinitions: iceDefinitions,
                spawnWeights: spawnWeights,
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
