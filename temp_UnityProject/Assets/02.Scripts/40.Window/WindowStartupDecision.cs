#nullable enable

using System;

namespace Icebreaker.Window
{
    public static class WindowStartupDecision
    {
        public static WindowStartupResult Decide(bool forceFallback, Func<bool> pluginInitializer)
        {
            if (pluginInitializer == null)
            {
                throw new ArgumentNullException(nameof(pluginInitializer));
            }

            if (forceFallback)
            {
                return new WindowStartupResult(
                    WindowStartupMode.NormalWindowFallback,
                    "Forced fallback flag is enabled.");
            }

            try
            {
                if (!pluginInitializer())
                {
                    return new WindowStartupResult(
                        WindowStartupMode.NormalWindowFallback,
                        "Plugin initializer reported failure.");
                }

                return new WindowStartupResult(
                    WindowStartupMode.PluginWindow,
                    "Plugin initializer succeeded.");
            }
            catch (Exception exception)
            {
                return new WindowStartupResult(
                    WindowStartupMode.NormalWindowFallback,
                    $"Plugin initialization threw {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
