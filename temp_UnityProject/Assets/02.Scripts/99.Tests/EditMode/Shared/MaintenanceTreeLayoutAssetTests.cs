#nullable enable

using System;
using System.Linq;
using Icebreaker.Shared.Maintenance;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Shared.Tests
{
    public sealed class MaintenanceTreeLayoutAssetTests
    {
        private MaintenanceTreeLayoutAsset asset = null!;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<MaintenanceTreeLayoutAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void Validate_AcceptsCompleteUniqueRootedLayoutInsideContentBounds()
        {
            asset.Configure(
                new Vector2(1600f, 900f),
                new[]
                {
                    Node("C01-L1", 100f, 100f, 60f),
                    Node("D01-L1", 300f, 100f, 52f),
                    Node("D01-L2", 500f, 100f, 44f)
                },
                new[]
                {
                    Edge("C01-L1", "D01-L1", new Vector2(200f, 100f)),
                    Edge("D01-L1", "D01-L2", new Vector2(400f, 100f))
                });

            var errors = asset.Validate(new[] { "C01-L1", "D01-L1", "D01-L2" });

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void Validate_ReportsEveryLockedLayoutBoundary()
        {
            asset.Configure(
                new Vector2(1600f, 900f),
                new[]
                {
                    Node("D01-L1", 20f, 20f, 60f),
                    Node("D01-L1", 300f, 100f, 44f),
                    Node("UNKNOWN-L1", 1700f, 100f, 44f)
                },
                new[]
                {
                    Edge("D01-L1", "D01-L1"),
                    Edge("D01-L1", "MISSING-L1")
                });

            var errors = asset.Validate(new[] { "C01-L1", "D01-L1" });

            Assert.That(errors.Any(error => error.Contains("C01-L1", StringComparison.Ordinal)), Is.True);
            Assert.That(errors.Any(error => error.Contains("Duplicate", StringComparison.Ordinal)), Is.True);
            Assert.That(errors.Any(error => error.Contains("UNKNOWN-L1", StringComparison.Ordinal)), Is.True);
            Assert.That(errors.Any(error => error.Contains("bounds", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(errors.Any(error => error.Contains("itself", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(errors.Any(error => error.Contains("MISSING-L1", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void Validate_RejectsInvalidContentSizeAndNodeVisualSize()
        {
            asset.Configure(
                Vector2.zero,
                new[] { Node("C01-L1", 100f, 100f, 0f) },
                Array.Empty<MaintenanceTreeEdgeLayout>());

            var errors = asset.Validate(new[] { "C01-L1" });

            Assert.That(errors.Any(error => error.Contains("Content", StringComparison.Ordinal)), Is.True);
            Assert.That(errors.Any(error => error.Contains("visual size", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        private static MaintenanceTreeNodeLayout Node(
            string stepId,
            float x,
            float y,
            float size)
        {
            return new MaintenanceTreeNodeLayout(
                stepId,
                new Vector2(x, y),
                new Vector2(size, size),
                null,
                "");
        }

        private static MaintenanceTreeEdgeLayout Edge(
            string fromStepId,
            string toStepId,
            params Vector2[] bendPoints)
        {
            return new MaintenanceTreeEdgeLayout(fromStepId, toStepId, bendPoints);
        }
    }
}
