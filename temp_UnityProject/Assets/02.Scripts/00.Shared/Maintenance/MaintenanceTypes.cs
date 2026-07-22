#nullable enable

namespace Icebreaker.Shared.Maintenance
{
    public enum MaintenanceBranch
    {
        Common,
        Direct,
        Support,
        Chain
    }

    public enum MaintenanceNodeState
    {
        Owned,
        Available,
        Locked
    }

    public enum MaintenancePurchaseResult
    {
        Success,
        InvalidPhase,
        InvalidNode,
        Locked,
        InsufficientFunds,
        MaxLevel
    }
}
