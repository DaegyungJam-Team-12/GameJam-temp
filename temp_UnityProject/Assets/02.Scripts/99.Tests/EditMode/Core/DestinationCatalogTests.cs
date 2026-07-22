#nullable enable

using NUnit.Framework;

namespace Icebreaker.Core.Tests
{
    public sealed class DestinationCatalogTests
    {
        [Test]
        public void CreateStandardAndDemo_MatchDestinationSpecification()
        {
            var expectedIds = new[]
            {
                "island-village",
                "lighthouse-port",
                "northern-base"
            };
            var expectedNames = new[] { "섬마을", "등대항", "북쪽 기지" };
            var expectedCargoNames = new[]
            {
                "식료품·우편",
                "발전기 연료·의약품",
                "기계 부품·우편"
            };
            var expectedStandardTargets = new[] { 120, 600, 2_400 };
            var expectedDemoTargets = new[] { 40, 120, 300 };
            var standard = DestinationCatalog.CreateStandard();
            var demo = DestinationCatalog.CreateDemo();

            Assert.That(standard, Has.Count.EqualTo(3));
            Assert.That(demo, Has.Count.EqualTo(3));
            for (var index = 0; index < expectedIds.Length; index++)
            {
                Assert.That(standard[index].Id, Is.EqualTo(expectedIds[index]));
                Assert.That(standard[index].DisplayName, Is.EqualTo(expectedNames[index]));
                Assert.That(standard[index].CargoName, Is.EqualTo(expectedCargoNames[index]));
                Assert.That(standard[index].DisplayOrder, Is.EqualTo(index));
                Assert.That(
                    standard[index].TargetProgress,
                    Is.EqualTo(expectedStandardTargets[index]));

                Assert.That(demo[index].Id, Is.EqualTo(standard[index].Id));
                Assert.That(demo[index].DisplayName, Is.EqualTo(standard[index].DisplayName));
                Assert.That(demo[index].CargoName, Is.EqualTo(standard[index].CargoName));
                Assert.That(demo[index].DisplayOrder, Is.EqualTo(standard[index].DisplayOrder));
                Assert.That(demo[index].TargetProgress, Is.EqualTo(expectedDemoTargets[index]));
            }
        }
    }
}
