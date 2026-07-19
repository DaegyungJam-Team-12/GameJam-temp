#nullable enable

using System;

namespace Icebreaker.Window
{
    public readonly struct WindowStartupResult
    {
        public WindowStartupResult(WindowStartupMode mode, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason cannot be null or whitespace.", nameof(reason));
            }

            Mode = mode;
            Reason = reason;
        }

        public WindowStartupMode Mode { get; }

        public string Reason { get; }
    }
}
