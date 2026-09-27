using Xunit;

// Only one copy of the application may drive the desktop at a time: parallel classes would fight
// over keyboard focus and the foreground window.
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
