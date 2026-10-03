using NUnit.Framework;

// 10-second default timeout applied to every test in this assembly. Generous
// for edit-mode tests (most finish in milliseconds); anything longer is either
// broken or doing something it shouldn't. NOTE: [Timeout] cannot interrupt a
// test that has deadlocked the main thread on a monitor (e.g. a misuse of
// .GetAwaiter().GetResult() that needs the main thread to resume) — NUnit's
// timeout thread can't preempt the Unity main thread in that state. It does
// catch runaway Task.Delay, infinite loops, and other well-behaved hangs.
[assembly: Timeout(10000)]
