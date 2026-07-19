#nullable enable

using System;

namespace Icebreaker.Window
{
    public static class WindowAttachWait
    {
        public static AttachWaitStatus Evaluate(
            bool attached,
            double elapsedSeconds,
            double timeoutSeconds)
        {
            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            }

            if (attached)
            {
                return AttachWaitStatus.Attached;
            }

            if (elapsedSeconds >= timeoutSeconds)
            {
                return AttachWaitStatus.TimedOut;
            }

            return AttachWaitStatus.Pending;
        }
    }
}
