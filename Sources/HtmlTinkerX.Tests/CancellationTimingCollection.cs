using Xunit;

namespace HtmlTinkerX.Tests;

// These checks use short watchdogs to detect operations left waiting on a driver or
// stream. Run them without unrelated test work competing for their continuations.
[CollectionDefinition("Cancellation timing", DisableParallelization = true)]
public sealed class CancellationTimingCollection {
}
