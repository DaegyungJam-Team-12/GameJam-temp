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
    /// MonoBehaviour that creates an <see cref="IceField"/> with 20 T1 ice blocks,
    /// handles mouse input, and renders each ice as a coloured circle.
    /// Attach to a GameObject in siyeon.unity.
    /// </summary>
    public sealed class IceFieldView : MonoBehaviour
    {
        private const float ReferenceWidth = 960f;
        private const float ReferenceHeight = 540f;
        private const int RuntimeSpriteSize = 64;
        private const float ClickDamage = 1f;
        private const float SpawnMargin = 56f;

        [SerializeField] private Camera? sceneCamera;
        [SerializeField] private long stageId = 1L;

        private IceField? field;
        private IceFieldConfig? config;
        private readonly List<SpriteRenderer> visuals = new();
        private Sprite? iceSprite;
        private double stageStartedAt;

        private void Awake()
        {
            sceneCamera ??= Camera.main;
            stageStartedAt = Time.timeAsDouble;

            config = CreateDefaultConfig();
            var idGenerator = new IceIdGenerator();
            var spawnBounds = new Rect(
                SpawnMargin,
                SpawnMargin,
                ReferenceWidth - SpawnMargin * 2f,
                ReferenceHeight - SpawnMargin * 2f);
            var positioner = new IceSpawnPositioner(spawnBounds, config.MinimumSpawnDistanceReferencePixels);

            field = new IceField(stageId, config, idGenerator, positioner);
            field.DamageApplied += HandleDamageApplied;
            field.IceDestroyed += HandleIceDestroyed;
            field.IceRespawned += HandleIceRespawned;

            iceSprite = CreateIceSprite();
            field.Initialize(0d);
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
            if (field == null || sceneCamera == null || mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            var screenPos = mouse.position.ReadValue();
            var refPos = ScreenToReference(screenPos);

            field.ApplyClickAt(refPos, ClickDamage, Time.timeAsDouble - stageStartedAt);
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
            renderer.color = ice.IsDestroyed
                ? new Color(0.7f, 0.85f, 0.9f, 0.3f)
                : Color.Lerp(new Color(0.7f, 0.92f, 1f, 1f), Color.white, hpRatio);
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

            Debug.Log($"[GP-02] Damage {e.Damage}, remaining HP {e.RemainingHp}, ice={e.IceInstanceId}.", this);
        }

        private void HandleIceDestroyed(IceDestroyedEvent e)
        {
            Debug.Log($"[GP-02] IceDestroyedEvent ice={e.IceInstanceId} tier={e.Tier}.", this);
        }

        private void HandleIceRespawned(int index)
        {
            RefreshVisual(index);
            if (field != null)
            {
                Debug.Log($"[GP-02] Respawned index={index}, newId={field.ActiveIce[index].IceInstanceId}.", this);
            }
        }

        // --- Sprite & Config creation ---

        private static IceFieldConfig CreateDefaultConfig()
        {
            var iceDefinitions = new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) };
            var spawnWeights = new[] { new IceSpawnWeight(IceTier.T1, 100) };
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
