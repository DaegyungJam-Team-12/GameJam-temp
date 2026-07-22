#nullable enable

using System;

namespace Icebreaker.Core
{
    public sealed class SaveService
    {
        private readonly SaveStore store;
        private readonly SaveData data;
        private readonly double debounceSeconds;
        private bool pendingDirty;
        private double elapsedSinceDirty;

        public SaveService(SaveStore store, SaveData data, double debounceSeconds = 1.0)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (debounceSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(debounceSeconds),
                    debounceSeconds,
                    "Value must be positive.");
            }

            this.store = store;
            this.data = data;
            this.debounceSeconds = debounceSeconds;
        }

        public SaveData Data => data;

        public bool HasPendingWrite => pendingDirty;

        public int DirtyMarkCount { get; private set; }

        public int FlushCount { get; private set; }

        public void MarkDirty()
        {
            DirtyMarkCount++;
            pendingDirty = true;
            elapsedSinceDirty = 0d;
        }

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds),
                    deltaSeconds,
                    "Value cannot be negative.");
            }

            if (!pendingDirty)
            {
                return;
            }

            elapsedSinceDirty += deltaSeconds;
            if (elapsedSinceDirty >= debounceSeconds)
            {
                Flush();
            }
        }

        public void Flush()
        {
            if (!pendingDirty)
            {
                return;
            }

            store.Save(data);
            FlushCount++;
            pendingDirty = false;
            elapsedSinceDirty = 0d;
        }
    }
}
