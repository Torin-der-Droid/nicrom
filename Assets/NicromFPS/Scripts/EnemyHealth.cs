using System;

namespace NicromFPS
{
    public sealed class EnemyHealth
    {
        public float Maximum { get; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0;
        public EnemyHealth(float maximum)
        {
            if (maximum <= 0 || float.IsNaN(maximum) || float.IsInfinity(maximum)) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = Current = maximum;
        }
        public bool Damage(float amount)
        {
            if (IsDead || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            Current = Math.Max(0, Current - amount);
            return true;
        }
        public void Set(float value)
        {
            if (IsDead || float.IsNaN(value) || float.IsInfinity(value)) return;
            Current = Math.Max(0, Math.Min(Maximum, value));
        }
    }
}
