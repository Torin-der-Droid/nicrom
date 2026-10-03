using System.Runtime.CompilerServices;

// Expose PromptMotion.Generator.Editor internals to the test assembly so tests
// can exercise helpers (e.g. MotionFbxPostprocessor.IsPathUnderOutputFolder)
// without them having to live on the package's public API surface.
[assembly: InternalsVisibleTo("PromptMotion.Tests.Editor")]
