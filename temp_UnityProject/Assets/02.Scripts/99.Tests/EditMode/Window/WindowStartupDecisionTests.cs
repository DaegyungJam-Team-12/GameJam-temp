#nullable enable

using System;
using NUnit.Framework;

namespace Icebreaker.Window.Tests
{
    public sealed class WindowStartupDecisionTests
    {
        [Test]
        public void ForceFallback_SelectsNormalWindow_WithoutInvokingInitializer()
        {
            var invoked = false;

            var result = WindowStartupDecision.Decide(
                true,
                () =>
                {
                    invoked = true;
                    return true;
                });

            Assert.That(invoked, Is.False);
            Assert.That(result.Mode, Is.EqualTo(WindowStartupMode.NormalWindowFallback));
        }

        [Test]
        public void InitializerFailure_SelectsNormalWindow()
        {
            var result = WindowStartupDecision.Decide(false, () => false);

            Assert.That(result.Mode, Is.EqualTo(WindowStartupMode.NormalWindowFallback));
        }

        [Test]
        public void InitializerException_SelectsNormalWindow_AndDoesNotThrow()
        {
            WindowStartupResult result = default;

            Assert.DoesNotThrow(() => result = WindowStartupDecision.Decide(
                false,
                () => throw new InvalidOperationException("native load failed")));
            Assert.That(result.Mode, Is.EqualTo(WindowStartupMode.NormalWindowFallback));
            Assert.That(result.Reason, Does.Contain("native load failed"));
        }

        [Test]
        public void InitializerSuccess_SelectsPluginWindow()
        {
            var result = WindowStartupDecision.Decide(false, () => true);

            Assert.That(result.Mode, Is.EqualTo(WindowStartupMode.PluginWindow));
        }

        [Test]
        public void NullInitializer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => WindowStartupDecision.Decide(false, null!));
        }
    }
}
