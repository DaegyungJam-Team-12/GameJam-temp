#nullable enable

using System;
using NUnit.Framework;

namespace Icebreaker.Window.Tests
{
    public sealed class WindowAttachWaitTests
    {
        [Test]
        public void AttachedBeforeTimeout_ReturnsAttached()
        {
            var status = WindowAttachWait.Evaluate(true, 1.0, 2.0);

            Assert.That(status, Is.EqualTo(AttachWaitStatus.Attached));
        }

        [Test]
        public void AttachedPastTimeout_ReturnsAttached()
        {
            var status = WindowAttachWait.Evaluate(true, 2.1, 2.0);

            Assert.That(status, Is.EqualTo(AttachWaitStatus.Attached));
        }

        [Test]
        public void NotAttachedBeforeTimeout_ReturnsPending()
        {
            var status = WindowAttachWait.Evaluate(false, 1.9, 2.0);

            Assert.That(status, Is.EqualTo(AttachWaitStatus.Pending));
        }

        [Test]
        public void NotAttachedAtTimeout_ReturnsTimedOut()
        {
            var status = WindowAttachWait.Evaluate(false, 2.0, 2.0);

            Assert.That(status, Is.EqualTo(AttachWaitStatus.TimedOut));
        }

        [Test]
        public void ZeroOrNegativeTimeout_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WindowAttachWait.Evaluate(false, 0.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => WindowAttachWait.Evaluate(false, 0.0, -1.0));
        }
    }
}
