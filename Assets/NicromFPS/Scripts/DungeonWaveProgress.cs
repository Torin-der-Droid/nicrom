using System;
using System.Collections.Generic;
namespace NicromFPS
{
    // Pure encounter clock: callers advance it only while this encounter is active.
    public sealed class DungeonWaveProgress
    {
        readonly HashSet<int> registered = new HashSet<int>();
        readonly HashSet<int> alive = new HashSet<int>();
        public int CountMultiplier { get; set; } = 1;
        public int Wave { get; private set; }
        public int Target => Wave == 0 ? 0 : (int)Math.Min(int.MaxValue, (long)CountFor(Wave) * Math.Max(1, CountMultiplier));
        public int Pending => Target - registered.Count;
        public int Alive => alive.Count;
        public float Countdown { get; private set; } = 10;
        public float SpawnWait { get; private set; }
        public bool Intermission { get; private set; } = true;
        public bool CanSpawn => !Intermission && Pending > 0 && Alive < 24 && SpawnWait <= 0;
        public static int CountFor(int wave)
        {
            double m = Math.Max(1, wave / 5.0);
            if (wave >= 10) m *= wave * 0.15;
            double count = 24 + Math.Floor(3 * m);
            double[] factors = { .25, .3, .5, .7, .9 };
            return (int)Math.Min(int.MaxValue, Math.Floor(count * (wave <= 5 ? factors[Math.Max(1, wave)-1] : 1)));
        }
        public static float HealthFor(int wave)
        {
            double health = 150;
            for (int i = 2; i <= wave; i++)
            {
                health = i <= 9 ? health + 100 : health + Math.Floor(health * .1);
                if (health >= float.MaxValue) return float.MaxValue;
            }
            return (float)health;
        }
        public static float IntervalFor(int wave) => (float)Math.Max(.08, 2 * Math.Pow(.95, wave - 1));
        public void Tick(float seconds, bool active)
        {
            if (!active || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (Intermission)
            {
                Countdown = Math.Max(0, Countdown - seconds);
                if (Countdown > 0) return;
                Wave++; registered.Clear(); alive.Clear(); Intermission = false; SpawnWait = 0;
            }
            else SpawnWait = Math.Max(0, SpawnWait - seconds);
        }
        public bool Register(int id)
        {
            if (!CanSpawn || !registered.Add(id)) return false;
            alive.Add(id); SpawnWait = IntervalFor(Wave); return true;
        }
        public bool Defeat(int id)
        {
            if (!alive.Remove(id)) return false;
            if (Pending == 0 && Alive == 0) { Intermission = true; Countdown = 10; }
            return true;
        }
        public void Retry() => SpawnWait = .5f;
        public void Reset()
        {
            Wave = 0; registered.Clear(); alive.Clear(); Countdown = 10; SpawnWait = 0; Intermission = true;
        }
    }
}
