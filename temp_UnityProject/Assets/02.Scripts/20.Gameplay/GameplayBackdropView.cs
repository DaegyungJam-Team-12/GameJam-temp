#nullable enable

using UnityEngine;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// Renders the approved 1920x1080 ocean and ship layers against the gameplay camera.
    /// Both layers share one scale so the artist-authored center-bottom ship placement is kept.
    /// </summary>
    public sealed class GameplayBackdropView : MonoBehaviour
    {
        private const int BackgroundSortingOrder = -100;
        private const int ShipSortingOrder = 5;

        [SerializeField] private Camera? sceneCamera;
        [SerializeField] private Sprite? oceanBackground;
        [SerializeField] private Sprite? ship;

        private SpriteRenderer? backgroundRenderer;
        private SpriteRenderer? shipRenderer;
        private float lastCameraAspect = -1f;
        private float lastOrthographicSize = -1f;

        public bool HasAssignedArt =>
            sceneCamera != null && oceanBackground != null && ship != null;

        public SpriteRenderer? BackgroundRenderer => backgroundRenderer;
        public SpriteRenderer? ShipRenderer => shipRenderer;

        private void Awake()
        {
            if (!HasAssignedArt)
            {
                Debug.LogError(
                    "[ART-P0] Gameplay backdrop camera, ocean, or ship reference is missing.",
                    this);
                return;
            }

            backgroundRenderer = CreateLayer(
                "OceanBackground",
                oceanBackground!,
                BackgroundSortingOrder);
            shipRenderer = CreateLayer("Ship", ship!, ShipSortingOrder);
            AlignToCamera();
        }

        private void LateUpdate()
        {
            if (sceneCamera == null ||
                Mathf.Approximately(lastCameraAspect, sceneCamera.aspect) &&
                Mathf.Approximately(lastOrthographicSize, sceneCamera.orthographicSize))
            {
                return;
            }

            AlignToCamera();
        }

        private SpriteRenderer CreateLayer(string objectName, Sprite sprite, int sortingOrder)
        {
            var layer = new GameObject(objectName);
            layer.transform.SetParent(transform, false);

            var renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.white;
            return renderer;
        }

        private void AlignToCamera()
        {
            if (sceneCamera == null ||
                backgroundRenderer == null ||
                shipRenderer == null ||
                oceanBackground == null)
            {
                return;
            }

            var worldHeight = sceneCamera.orthographicSize * 2f;
            var worldWidth = worldHeight * sceneCamera.aspect;
            var spriteSize = oceanBackground.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            // Cover the camera without stretching; non-16:9 previews crop symmetrically.
            var uniformScale = Mathf.Max(
                worldWidth / spriteSize.x,
                worldHeight / spriteSize.y);
            var worldCenter = new Vector3(
                sceneCamera.transform.position.x,
                sceneCamera.transform.position.y,
                0f);

            backgroundRenderer.transform.position = worldCenter;
            backgroundRenderer.transform.localScale = Vector3.one * uniformScale;
            shipRenderer.transform.position = worldCenter;
            shipRenderer.transform.localScale = Vector3.one * uniformScale;

            lastCameraAspect = sceneCamera.aspect;
            lastOrthographicSize = sceneCamera.orthographicSize;
        }
    }
}
