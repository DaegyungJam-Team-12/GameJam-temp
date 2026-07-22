#nullable enable

namespace Icebreaker.Shared.Events
{
    public enum DestroyCategory
    {
        Direct,
        Support,
        Chain
    }

    public enum EffectType
    {
        CursorAreaPulse,
        Click,
        Hold,
        Overkill,
        SupportShot,
        CrystalShard,
        CrackExplosion,
        HullFragment,
        IceCollapse
    }
}
