using System.Runtime.CompilerServices;

namespace SF6_Plugin_Core.Services;

public static class FrameCallbackPoller
{
    private readonly record struct PollJob(
        Func<bool> Condition,
        Action OnReady,
        Action? OnAborted,
        int MaxFrames,
        StrongBox<int> FramesWaited
    );

    private static readonly ConcurrentQueue<PollJob> s_jobs = new();

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