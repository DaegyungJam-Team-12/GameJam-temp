#nullable enable

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// Issues unique, ever-increasing ice instance IDs within a single app session.
    /// </summary>
    public sealed class IceIdGenerator
    {
        private long nextId;

        public IceIdGenerator(long startId = 1L)
        {
            nextId = startId;
        }

        public long NextId()
        {
            return nextId++;
        }

        public long PeekNextId()
        {
            return nextId;
        }
    }
}
