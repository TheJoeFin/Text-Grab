using BenchmarkDotNet.Running;

namespace Tests;

public class BenchmarkRunnerTests
{
    [Fact(Skip = "Manual: run benchmarks on demand only.")]
    public void RunAllBenchmarks()
    {
        // Runs all benchmarks in the Tests assembly. Intended to be invoked manually.
        _ = BenchmarkRunner.Run(typeof(BenchmarkRunnerTests).Assembly);
    }
}
