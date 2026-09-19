namespace MosDock;

/// <summary>
/// Worker ngầm: đẩy LocalExamStore + OfflineQueue khi có JWT và mạng.
/// </summary>
static class BackgroundSync
{
    static System.Threading.Timer? _timer;
    static int _busy;

    public static void Start()
    {
        _timer ??= new System.Threading.Timer(_ => _ = TickSafe(), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(20));
    }

    public static void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    static async Task TickSafe()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        try
        {
            await TickAsync();
        }
        catch
        {
            // lần sau
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    public static async Task TickAsync()
    {
        if (string.IsNullOrWhiteSpace(Portal.Token))
        {
            return;
        }

        await OfflineQueue.FlushAsync();
        foreach (var state in LocalExamStore.Pending())
        {
            try
            {
                await PushStateAsync(state);
                await PushPendingFileAsync(state);
            }
            catch
            {
                // giữ local, thử lại
            }
        }
    }

    static async Task PushStateAsync(LocalExamState state)
    {
        if (string.IsNullOrWhiteSpace(state.AttemptId) || state.AttemptId.StartsWith("local-", StringComparison.Ordinal))
        {
            return;
        }

        using var posted = await Portal.PostJsonAsync(
            $"/api/v1/attempts/{state.AttemptId}/state",
            new
            {
                progress_pct = state.ProgressPct,
                updated_at = state.UpdatedAt,
                local_grade = new
                {
                    verified = state.LocalVerified,
                    pending = state.LocalPending,
                    max_score = 100,
                },
            },
            Portal.SyncHeaders(state.LocalPath, state.UpdatedAt));
    }

    static async Task PushPendingFileAsync(LocalExamState state)
    {
        if (!string.IsNullOrWhiteSpace(state.PendingSubmit) && File.Exists(state.PendingSubmit))
        {
            using var submitted = await Portal.PostFileAsync(
                $"/api/v1/attempts/{state.AttemptId}/submit",
                state.PendingSubmit,
                headers: Portal.SyncHeaders(state.PendingSubmit, state.UpdatedAt));
            LocalExamStore.MarkSubmitted(state.AttemptId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(state.PendingCheckpoint) && File.Exists(state.PendingCheckpoint))
        {
            using var posted = await Portal.PostFileAsync(
                $"/api/v1/attempts/{state.AttemptId}/checkpoints",
                state.PendingCheckpoint,
                headers: Portal.SyncHeaders(state.PendingCheckpoint, state.UpdatedAt));
            if (posted.RootElement.TryGetProperty("stale", out var stale) && stale.ValueKind == JsonValueKind.True)
            {
                return;
            }

            LocalExamStore.ClearPending(state.AttemptId, checkpoint: true, submit: false);
        }
    }
}
