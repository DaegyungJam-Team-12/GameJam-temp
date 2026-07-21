#nullable enable

namespace Icebreaker.Core
{
    [System.Serializable]
    public struct SaveMaintenanceLevel
    {
        public SaveMaintenanceLevel(string id, int level)
        {
            this.id = id;
            this.level = level;
        }

        public string id;
        public int level;
    }
}
