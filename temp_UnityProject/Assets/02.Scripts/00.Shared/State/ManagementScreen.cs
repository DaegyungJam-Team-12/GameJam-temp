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

    public interface IManagementScreenSource
    {
        event Action<ManagementScreen> ManagementScreenChanged;

        ManagementScreen CurrentManagementScreen { get; }

        bool RequestManagementScreen(ManagementScreen screen);

        void CloseManagementScreen();
    }
}
