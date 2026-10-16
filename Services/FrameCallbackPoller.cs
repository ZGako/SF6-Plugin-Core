using System.Runtime.CompilerServices;

namespace SF6_Plugin_Core.Services;

/// <summary>
/// A utility class that allows for polling a condition on the main thread, 
/// executing callbacks when the condition is met or when a timeout occurs.
/// </summary>
internal static class FrameCallbackPoller
{
    private readonly record struct PollJob(
        Func<bool> Condition,
        Action OnReady,
        Action? OnAborted,
        int MaxFrames,
        StrongBox<int> FramesWaited
    );

    private static readonly ConcurrentQueue<PollJob> s_jobs = new();

    /// <summary>
    /// Queues a polling job to wait until a condition is met (such as an asynchronous prefab asset becoming ready), 
    /// executing <paramref name="onReady"/> on success or <paramref name="onAborted"/> upon timeout.
    /// </summary>
    /// <param name="condition">The condition evaluated every frame.</param>
    /// <param name="onReady">Callback invoked when the condition returns true.</param>
    /// <param name="onAborted">Optional callback invoked if the job times out or throws an exception.</param>
    /// <param name="maxFrames">Maximum number of frames to wait before timing out (default: 600).</param>
    public static void WaitUntil(
        Func<bool> condition,
        Action onReady,
        Action? onAborted = null,
        int maxFrames = 600)
    {
        if (condition())
        {
            onReady();
            return;
        }

        s_jobs.Enqueue(new PollJob(condition, onReady, onAborted, maxFrames, new StrongBox<int>(0)));
    }

    [Callback(typeof(UpdateBehavior), CallbackType.Pre)]
    public static void OnUpdateBehavior()
    {
        if (s_jobs.IsEmpty) return;

        int count = s_jobs.Count;
        for (int i = 0; i < count; i++)
        {
            if (!s_jobs.TryDequeue(out var job)) break;

            bool isReady = false;
            try
            {
                isReady = job.Condition();
            }
            catch (Exception ex)
            {
                API.LogError($"[MainThreadPoller] Condition threw exception: {ex}");
                SafeInvoke(job.OnAborted);
                continue;
            }

            if (isReady)
            {
                SafeInvoke(job.OnReady);
            }
            else if (++job.FramesWaited.Value >= job.MaxFrames)
            {
                API.LogWarning($"[MainThreadPoller] Job timed out after {job.MaxFrames} frames.");
                SafeInvoke(job.OnAborted);
            }
            else
            {
                s_jobs.Enqueue(job);
            }
        }
    }

    internal static void Clear()
    {
        while (s_jobs.TryDequeue(out var job))
        {
            SafeInvoke(job.OnAborted);
        }
    }

    private static void SafeInvoke(Action? action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception ex)
        {
            API.LogError($"[MainThreadPoller] Callback threw exception: {ex}");
        }
    }
}