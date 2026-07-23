#nullable enable

using System;

namespace Icebreaker.Shared.State
{
    public enum ManagementScreen
    {
        None,
        Maintenance,
        Route,
        Settings
    }

    public static class ManagementScreenRules
    {
        public static bool CanOpen(ManagementScreen screen, GamePhase phase) => screen switch
        {
            ManagementScreen.Maintenance or ManagementScreen.Route =>
                phase == GamePhase.Traveling || phase == GamePhase.Ready,
            ManagementScreen.Settings =>
                phase == GamePhase.Traveling || phase == GamePhase.Ready ||
                phase == GamePhase.Countdown || phase == GamePhase.Playing ||
                phase == GamePhase.Completed,
            _ => false
        };
    }

    public interface IManagementScreenSource
    {
        event Action<ManagementScreen> ManagementScreenChanged;

        ManagementScreen CurrentManagementScreen { get; }

        bool RequestManagementScreen(ManagementScreen screen);

        void CloseManagementScreen();
    }
}
