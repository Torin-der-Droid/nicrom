using System;
using NicromFPS;

static class Program
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Main()
    {
        foreach (int fps in new[] {30,60,144})
        {
            var gesture = new HandGestureTiming();
            Check(gesture.Trigger() && !gesture.Trigger(), "No retrigger while hand is moving");
            int peaks = 0, frames = 0;
            while (gesture.Playing && frames++ < fps)
            {
                float before = gesture.Elapsed;
                gesture.Tick(1, true);
                Check(gesture.Elapsed == before && !gesture.PeakThisFrame, "Pause freezes hand timeline");
                gesture.Tick(1f/fps,false);
                if(gesture.PeakThisFrame)peaks++;
                Check(gesture.Weight >= 0 && gesture.Weight <= 1, "Gesture blend bounded");
            }
            Check(peaks == 1 && !gesture.Playing && gesture.Weight == 0, "One extension then full return at " + fps);
            Check(gesture.Trigger(), "Hand can repeat after return");gesture.Cancel();
            Check(!gesture.Playing && gesture.Weight == 0 && !gesture.PeakThisFrame, "Cancel resets pose");
        }
        Console.WriteLine("Hand gesture timing: 30/60/144 FPS, pause, repeat, one peak and cancellation passed.");
        var dungeon = new DungeonWaveProgress();
        int[] expected = {6,8,13,18,24,27,28,28,29,33};
        for (int i = 0; i < expected.Length; i++) Check(DungeonWaveProgress.CountFor(i+1) == expected[i], "Dungeon solo count " + (i+1));
        Check(DungeonWaveProgress.HealthFor(1) == 150 && DungeonWaveProgress.HealthFor(9) == 950
            && DungeonWaveProgress.HealthFor(10) == 1045 && DungeonWaveProgress.HealthFor(11) == 1149, "Dungeon health scaling");
        Check(DungeonWaveProgress.IntervalFor(1) == 2 && Math.Abs(DungeonWaveProgress.IntervalFor(2)-1.9f)<.001
            && DungeonWaveProgress.IntervalFor(1000) == .08f, "Dungeon spawn pacing");
        dungeon.Tick(9, true); dungeon.Tick(100, false);
        Check(dungeon.Countdown == 1 && dungeon.Wave == 0 && !dungeon.Register(1), "Paused countdown and no early spawns");
        dungeon.Tick(1, true);
        for (int round = 1; round <= 6; round++)
        {
            Check(dungeon.Wave == round, "Dungeon round advanced");
            int target = dungeon.Target;
            for (int id = 1; id <= target; id++)
            {
                if (id > 24) Check(dungeon.Defeat(id-24), "Free simultaneous slot");
                dungeon.Tick(3, true);
                Check(dungeon.Register(id) && !dungeon.Register(id), "Unique paced spawn");
                Check(!dungeon.CanSpawn, "Interval prevents burst");
                float wait = dungeon.SpawnWait;
                dungeon.Tick(100, false);
                Check(dungeon.SpawnWait == wait, "Spawn clock paused");
                Check(dungeon.Alive <= 24, "Concurrent cap");
                if (id == 24) { dungeon.Tick(3, true); Check(!dungeon.CanSpawn, "Cap blocks reserve spawn"); }
            }
            for (int id = Math.Max(1,target-23); id <= target; id++)
            { Check(dungeon.Defeat(id), "Registered kill"); Check(!dungeon.Defeat(id), "Duplicate kill ignored"); }
            Check(dungeon.Intermission && dungeon.Countdown == 10, "Full clear starts break");
            dungeon.Tick(10, true);
        }
        dungeon.Register(1); dungeon.Reset();
        Check(dungeon.Wave == 0 && dungeon.Alive == 0 && dungeon.Pending == 0 && dungeon.Countdown == 10, "Death resets dungeon");
        var early = new DungeonWaveProgress(); early.Tick(10,true); early.Register(1); early.Defeat(1);
        Check(!early.Intermission && early.Pending == 5, "Pending enemies prevent wave skip");
        Console.WriteLine("Dungeon waves: counts, health, pacing, caps, pause, reset and duplicate deaths passed.");
        var waves = new WaveProgress();
        Check(!waves.Complete && !waves.Register(1), "No enemies before first wave");
        for (int wave = 1; wave <= 3; wave++)
        {
            Check(waves.Next() && waves.Target == wave * 10, "Waves increase by ten");
            Check(!waves.Next(), "Cannot skip unfinished wave");
            for (int id = 1; id <= waves.Target; id++)
            {
                Check(waves.Register(id) && !waves.Register(id), "Each spawn counts once");
                if (id < waves.Target) Check(!waves.Complete, "Pending spawns prevent completion");
            }
            Check(!waves.Register(999) && waves.Alive == waves.Target, "Wave has exact enemy count");
            for (int id = 1; id <= waves.Target; id++)
            {
                waves.Defeat(id);
                waves.Defeat(id);
                Check(waves.Alive == waves.Target - id, "Duplicate deaths cannot advance wave");
            }
            Check(waves.Complete, "Wave completes after all enemies die");
        }
        var partial = new WaveProgress();
        partial.Next(); partial.Register(1); partial.Defeat(1);
        Check(!partial.Register(1), "A defeated spawn cannot be registered twice");
        Check(!partial.Complete && !partial.Next() && partial.Pending == 9,
            "Killing early spawns must not skip pending enemies");
        var goblin = new EnemyHealth(100);
        Check(goblin.Current == 100 && !goblin.IsDead, "Goblins start alive with full health");
        Check(goblin.Damage(25) && goblin.Current == 75, "Weapon hits subtract exact damage");
        Check(!goblin.Damage(-10) && !goblin.Damage(0) && !goblin.Damage(float.NaN)
            && !goblin.Damage(float.PositiveInfinity) && goblin.Current == 75, "Invalid damage cannot heal or corrupt health");
        goblin.Set(300);
        Check(goblin.Current == 100, "Health cannot exceed its maximum");
        Check(goblin.Damage(150) && goblin.Current == 0 && goblin.IsDead, "Lethal damage clamps to zero");
        Check(!goblin.Damage(10), "Corpses cannot take repeated damage");
        goblin.Set(100);
        Check(goblin.Current == 0, "Dead goblins cannot revive through a health assignment");
        Check(Akila.FPSFramework.InventorySelection.Cycle(1, 2, 1) == 0, "Next weapon wraps at actual count");
        Check(Akila.FPSFramework.InventorySelection.Cycle(0, 2, -1) == 1, "Previous weapon never produces negative index");
        Check(Akila.FPSFramework.InventorySelection.Cycle(-1, 2, 1) == 0
            && Akila.FPSFramework.InventorySelection.Cycle(-1, 2, -1) == 1, "Cycling from default selects a weapon");
        Check(Akila.FPSFramework.InventorySelection.Normalize(1, 1, false) == 0, "Dropped weapon clamps old selection");
        Check(Akila.FPSFramework.InventorySelection.Normalize(99, 0, false) == -1, "Empty list selects no weapon");
        Check(Akila.FPSFramework.InventorySelection.Normalize(-1, 2, true) == -1
            && Akila.FPSFramework.InventorySelection.Normalize(-1, 2, false) == 0, "Default selection requires a default item");
        foreach (int count in new[] { 0, 1, 2, 3 })
            foreach (int index in new[] { int.MinValue, -2, -1, 0, 1, 2, 9, int.MaxValue })
                foreach (int direction in new[] { -1, 1 })
                {
                    int next = Akila.FPSFramework.InventorySelection.Cycle(index, count, direction);
                    Check(count == 0 ? next == -1 : next >= 0 && next < count, "Weapon cycle must stay in bounds");
                }
        var inventory = new ResourceInventory(2, 20);
        int changes = 0;
        inventory.Changed += () => changes++;
        Check(inventory.TryAdd("cube", 19), "First pickup batch failed");
        Check(inventory.TryAdd("cube", 5), "Stack overflow should open another slot");
        Check(inventory.Stacks.Count == 2 && inventory.Stacks[0].Count == 20
            && inventory.Stacks[1].Count == 4, "Incorrect stacking across slots");
        Check(!inventory.TryAdd("cube", 17) && inventory.Count("cube") == 24,
            "Rejected pickup must not partially modify inventory");
        Check(!inventory.TryAdd("other", 1), "A different resource needs its own slot");
        Check(!inventory.TryAdd("cube", int.MaxValue), "Large amounts must not overflow capacity checks");
        Check(!inventory.TryAdd("", 1) && !inventory.TryAdd("cube", 0)
            && !inventory.TryAdd("cube", -1), "Invalid pickups must be rejected");
        Check(changes == 2, "Rejected pickups must not notify the UI");
        Check(inventory.TryAdd("cube", 16) && inventory.Count("cube") == 40
            && !inventory.CanAdd("cube", 1), "Inventory must accept exact capacity and then become full");
        var mixed = new ResourceInventory(3, 20);
        Check(mixed.TryAdd("cube", 2) && mixed.TryAdd("wood", 7)
            && mixed.Count("cube") == 2 && mixed.Count("wood") == 7, "Resource types must remain separate");
        Check(mixed.Count("unknown") == 0, "Unknown resource count must be zero");
        int beforeRemoval = changes;
        Check(!inventory.TryRemove("cube", 41) && inventory.Count("cube") == 40
            && changes == beforeRemoval, "Failed construction must not spend cubes or notify");
        Check(inventory.TryRemove("cube", 21) && inventory.Count("cube") == 19
            && inventory.Stacks.Count == 1, "Removal across stacks must free empty slots");
        Check(inventory.TryRemove("cube", 19) && inventory.Stacks.Count == 0,
            "Building with the last cube must free the final slot");
        Check(!inventory.TryRemove("cube", 1) && !inventory.TryRemove("cube", 0)
            && !inventory.TryRemove("cube", -1) && !inventory.TryRemove(null, 1),
            "Empty inventory and invalid construction costs must be rejected");
        Check(mixed.TryRemove("cube", 1) && mixed.Count("cube") == 1 && mixed.Count("wood") == 7,
            "Construction must consume only the selected resource");
        Check(inventory.TryAdd("cube", 1) && inventory.TryRemove("cube", 1)
            && !inventory.TryRemove("cube", 1), "A single cube must not fund two placements");
        var wall = new WallBuildProgress();
        var resources = new ResourceInventory();
        Check(wall.Layer == 0 && wall.Column == 0 && wall.BuiltCount == 0, "Plan starts empty");
        Check(!wall.TrySupply(resources, true), "An empty inventory cannot build a wall");
        resources.TryAdd("gray_cube", 11);
        Check(!wall.TrySupply(resources, false) && resources.Count("gray_cube") == 11 && wall.BuiltCount == 0,
            "A blocked space must not consume resources or advance the wall");
        for (int i = 0; i < 4; i++) Check(wall.TrySupply(resources, true), "First layer should accept cubes");
        Check(wall.Layer == 0 && wall.Column == 4, "Next layer must wait for all five cubes");
        Check(wall.TrySupply(resources, true) && wall.Layer == 1 && wall.Column == 0,
            "Fifth cube should open the next layer");
        for (int i = 0; i < 5; i++) Check(wall.TrySupply(resources, true), "Second layer should build");
        Check(wall.Layer == 2 && wall.Column == 0 && resources.Count("gray_cube") == 1,
            "Two complete layers must consume exactly ten cubes");
        Check(wall.TrySupply(resources, true) && !wall.TrySupply(resources, true)
            && wall.Layer == 2 && wall.Column == 1, "Partial layers persist when resources run out");
        resources.TryAdd("gray_cube", 1);
        Check(wall.TrySupply(resources, true) && wall.Column == 2, "Construction resumes after collecting supplies");
        Console.WriteLine("PASS: waves 10/20/30, goblin health/death, weapon selection, inventory costs and wall construction.");
    }
}
