using System.Diagnostics;
using WPFLLM.Models;

namespace WPFLLM.Services;

/// <summary>
/// Extensions for measuring pipeline step timings
/// </summary>
public static class TimingExtensions
{
    /// <summary>
    /// Measure async operation and add timing to trace
    /// </summary>
    public static async Task<T> MeasureAsync<T>(
        this RagTrace trace,
        string name,
        Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        var result = await action();
        sw.Stop();
        trace.Timings.Add(new RagTiming(name, sw.ElapsedMilliseconds));
        return result;
    }

    /// <summary>
    /// Measure async operation without return value
    /// </summary>
    public static async Task MeasureAsync(
        this RagTrace trace,
        string name,
        Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        trace.Timings.Add(new RagTiming(name, sw.ElapsedMilliseconds));
    }

    /// <summary>
    /// Measure sync operation and add timing to trace
    /// </summary>
    public static T Measure<T>(
        this RagTrace trace,
        string name,
        Func<T> action)
    {
        var sw = Stopwatch.StartNew();
        var result = action();
        sw.Stop();
        trace.Timings.Add(new RagTiming(name, sw.ElapsedMilliseconds));
        return result;
    }

    /// <summary>
    /// Measure sync operation without return value
    /// </summary>
    public static void Measure(
        this RagTrace trace,
        string name,
        Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        trace.Timings.Add(new RagTiming(name, sw.ElapsedMilliseconds));
    }
}
