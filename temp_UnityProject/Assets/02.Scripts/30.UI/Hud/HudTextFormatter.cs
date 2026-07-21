#nullable enable

using System;
using System.Globalization;

namespace Icebreaker.UI.Hud
{
    public static class HudTextFormatter
    {
        public static string FormatFunds(long funds)
        {
            if (funds < 1_000)
            {
                return funds.ToString("N0", CultureInfo.InvariantCulture);
            }

            var compact = funds / 1_000d;
            return $"{compact.ToString("0.#", CultureInfo.InvariantCulture)}K";
        }

        public static string FormatCountdown(double remainingSeconds)
        {
            var totalSeconds = Math.Max(0, (long)Math.Ceiling(remainingSeconds));
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        public static string FormatCountdownDigit(double remainingSeconds)
        {
            var digit = Math.Min(3, (int)Math.Ceiling(Math.Max(0d, remainingSeconds)));
            return digit > 0
                ? digit.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        public static string FormatProgress(int current, int target)
        {
            return $"{current.ToString("N0", CultureInfo.InvariantCulture)}/{target.ToString("N0", CultureInfo.InvariantCulture)}";
        }
    }
}
