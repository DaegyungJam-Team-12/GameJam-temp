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
        private bool suspended;

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

        public bool IsSuspended => suspended;

        public void MarkDirty()
        {
            if (suspended)
            {
                return;
            }

            DirtyMarkCount++;
            pendingDirty = true;
            elapsedSinceDirty = 0d;
        }

        /// <summary>
        /// Deletes the persisted save and permanently stops this service from writing again.
        /// After this call MarkDirty/Tick/Flush are no-ops, so no shutdown or debounce path can
        /// recreate the file for the remainder of the session.
        /// </summary>
        public void ClearAndSuspend()
        {
            suspended = true;
            pendingDirty = false;
            elapsedSinceDirty = 0d;
            store.Delete(data.profileId);
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
            if (suspended || !pendingDirty)
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
