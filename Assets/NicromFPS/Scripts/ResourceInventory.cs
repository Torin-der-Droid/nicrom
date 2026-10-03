using System;
using System.Collections.Generic;

namespace NicromFPS
{
    // Scene-owned resource storage, separate from the framework's equipped weapon slots.
    public sealed class ResourceInventory
    {
        public sealed class Stack
        {
            public string ItemId { get; }
            public int Count { get; internal set; }
            internal Stack(string itemId, int count) { ItemId = itemId; Count = count; }
        }

        private readonly List<Stack> stacks = new List<Stack>();
        public IReadOnlyList<Stack> Stacks => stacks.AsReadOnly();
        public int SlotCount { get; }
        public int StackLimit { get; }
        public event Action Changed;

        public ResourceInventory(int slots = 8, int stackLimit = 20)
        {
            if (slots <= 0 || stackLimit <= 0) throw new ArgumentOutOfRangeException();
            SlotCount = slots;
            StackLimit = stackLimit;
        }

        public int Count(string itemId)
        {
            int total = 0;
            foreach (Stack stack in stacks)
                if (stack.ItemId == itemId) total += stack.Count;
            return total;
        }

        public bool CanAdd(string itemId, int count)
        {
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0) return false;
            long space = (long)(SlotCount - stacks.Count) * StackLimit;
            foreach (Stack stack in stacks)
                if (stack.ItemId == itemId) space += StackLimit - stack.Count;
            return space >= count;
        }

        public bool TryRemove(string itemId, int count)
        {
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0 || Count(itemId) < count) return false;
            for (int i = stacks.Count - 1; i >= 0 && count > 0; i--)
            {
                if (stacks[i].ItemId != itemId) continue;
                int removed = Math.Min(stacks[i].Count, count);
                stacks[i].Count -= removed;
                count -= removed;
                if (stacks[i].Count == 0) stacks.RemoveAt(i);
            }
            Changed?.Invoke();
            return true;
        }

        // A rejected pickup leaves both the inventory and the world object unchanged.
        public bool TryAdd(string itemId, int count)
        {
            if (!CanAdd(itemId, count)) return false;
            foreach (Stack stack in stacks)
            {
                if (stack.ItemId != itemId) continue;
                int added = Math.Min(StackLimit - stack.Count, count);
                stack.Count += added;
                count -= added;
            }
            while (count > 0)
            {
                int added = Math.Min(StackLimit, count);
                stacks.Add(new Stack(itemId, added));
                count -= added;
            }
            Changed?.Invoke();
            return true;
        }
    }
}
