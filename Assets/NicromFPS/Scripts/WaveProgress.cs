using System.Collections.Generic;

namespace NicromFPS
{
    public sealed class WaveProgress
    {
        private readonly HashSet<int> alive = new HashSet<int>();
        private readonly HashSet<int> registered = new HashSet<int>();
        public int Wave { get; private set; }
        public int Target => Wave * 10;
        public int Spawned { get; private set; }
        public int Alive => alive.Count;
        public int Pending => Target - Spawned;
        public bool Complete => Wave > 0 && Pending == 0 && Alive == 0;

        public bool Next()
        {
            if (Wave > 0 && !Complete) return false;
            Wave++;
            Spawned = 0;
            alive.Clear();
            registered.Clear();
            return true;
        }

        public bool Register(int id)
        {
            if (Wave == 0 || Pending == 0 || !registered.Add(id)) return false;
            alive.Add(id);
            Spawned++;
            return true;
        }

        public void Defeat(int id) => alive.Remove(id);
    }
}
