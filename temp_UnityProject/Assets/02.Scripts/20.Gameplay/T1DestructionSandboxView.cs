#nullable enable

using Icebreaker.Shared.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Icebreaker.Gameplay
{
    public sealed class T1DestructionSandboxView : MonoBehaviour
    {
        private const float ReferenceWidth = 960f;
        private const float ReferenceHeight = 540f;
        private const int RuntimeSpriteSize = 64;

        [SerializeField] private Camera? sceneCamera;
        [SerializeField] private long stageId = 1L;
        [SerializeField] private long iceInstanceId = 1001L;
        [SerializeField] private Vector2 referencePosition = new(480f, 270f);
        [SerializeField] private float displaySizePixels = 104f;

        private T1DestructionSandbox? sandbox;
        private SpriteRenderer? iceRenderer;
        private float worldRadius;
        private double stageStartedAt;

        public T1DestructionSandbox? Sandbox => sandbox;

        private void Awake()
        {
            sceneCamera ??= Camera.main;
            sandbox = new T1DestructionSandbox(stageId, iceInstanceId, referencePosition);
            sandbox.DamageApplied += HandleDamageApplied;
            sandbox.IceDestroyed += HandleIceDestroyed;

            stageStartedAt = Time.timeAsDouble;
            EnsureRuntimeIce();
            RenderHp();
        }

        private void OnDestroy()
        {
            if (sandbox == null)
            {
                return;
            }

            sandbox.DamageApplied -= HandleDamageApplied;
            sandbox.IceDestroyed -= HandleIceDestroyed;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (sandbox == null || sceneCamera == null || mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            var worldPoint = sceneCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            if (Vector2.Distance(transform.position, worldPoint) > worldRadius)
            {
                return;
            }

            sandbox.ApplyClick(Time.timeAsDouble - stageStartedAt);
        }

        private void EnsureRuntimeIce()
        {
            worldRadius = ResolveWorldRadius();

            var child = new GameObject("T1 Ice Runtime");
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.zero;

            iceRenderer = child.AddComponent<SpriteRenderer>();
            iceRenderer.sprite = CreateIceSprite();
            iceRenderer.sortingOrder = 10;
            child.transform.localScale = Vector3.one * (worldRadius * 2f);
        }

        private float ResolveWorldRadius()
        {
            if (sceneCamera == null || !sceneCamera.orthographic)
            {
                return 0.55f;
            }

            var worldHeight = sceneCamera.orthographicSize * 2f;
            return worldHeight * (displaySizePixels / ReferenceHeight) * 0.5f;
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
                    var color = Color.Lerp(new Color(0.87f, 0.98f, 1f, 1f), new Color(0.55f, 0.82f, 0.9f, 1f), edge);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, RuntimeSpriteSize, RuntimeSpriteSize), new Vector2(0.5f, 0.5f), RuntimeSpriteSize);
        }

        private void HandleDamageApplied(DamageAppliedEvent e)
        {
            RenderHp();
        }

        private void HandleIceDestroyed(IceDestroyedEvent e)
        {
            RenderHp();
        }

        private void RenderHp()
        {
            if (sandbox == null || iceRenderer == null)
            {
                return;
            }

            var hpRatio = sandbox.Target.RemainingHp / T1IceTarget.MaxHp;
            iceRenderer.color = sandbox.Target.IsDestroyed
                ? new Color(0.7f, 0.85f, 0.9f, 0.3f)
                : Color.Lerp(new Color(0.7f, 0.92f, 1f, 1f), Color.white, hpRatio);
        }
    }
}
