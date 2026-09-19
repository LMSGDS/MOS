using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MosDock;

sealed class LocalExamState
{
    public string AttemptId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Program { get; set; } = "word";
    public string Title { get; set; } = "";
    public string Mode { get; set; } = "training";
    public string Status { get; set; } = "running";
    public string LocalPath { get; set; } = "";
    public string? PendingCheckpoint { get; set; }
    public string? PendingSubmit { get; set; }
    public int ProgressPct { get; set; }
    public string UpdatedAt { get; set; } = LocalExamStore.NowIso();
    public string? ArtifactSha256 { get; set; }
    public double? LocalVerified { get; set; }
    public double? LocalPending { get; set; }
}

/// <summary>
/// Offline-first: state + telemetry trên máy học sinh, đẩy ngầm khi có mạng.
/// </summary>
static class LocalExamStore
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    static string Dir => Path.Combine(ExamSession.DataDir, "local");

    public static string NowIso() => DateTime.UtcNow.ToString("o");

    public static string Sha256File(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    public static void SaveCurrent(
        int progressPct = 0,
        string? pendingCheckpoint = null,
        string? pendingSubmit = null,
        double? verified = null,
        double? pending = null)
    {
        if (string.IsNullOrWhiteSpace(ExamSession.AttemptId))
        {
            return;
        }

        var existing = Load(ExamSession.AttemptId) ?? new LocalExamState();
        existing.AttemptId = ExamSession.AttemptId!;
        existing.ProjectId = ExamSession.ProjectId ?? existing.ProjectId;
        existing.Program = ExamSession.Program;
        existing.Title = ExamSession.ProjectTitle ?? existing.Title;
        existing.Mode = ExamSession.Mode;
        existing.Status = string.IsNullOrWhiteSpace(pendingSubmit) ? existing.Status ?? "running" : "submitted";
        existing.LocalPath = ExamSession.LocalPath ?? existing.LocalPath;
        existing.ProgressPct = progressPct;
        existing.UpdatedAt = NowIso();
        existing.PendingCheckpoint = pendingCheckpoint ?? existing.PendingCheckpoint;
        existing.PendingSubmit = pendingSubmit ?? existing.PendingSubmit;
        existing.LocalVerified = verified ?? existing.LocalVerified;
        existing.LocalPending = pending ?? existing.LocalPending;
        if (!string.IsNullOrWhiteSpace(existing.LocalPath) && File.Exists(existing.LocalPath))
        {
            existing.ArtifactSha256 = Sha256File(existing.LocalPath);
        }

        Save(existing);
    }

    public static void Save(LocalExamState state)
    {
        Directory.CreateDirectory(Dir);
        state.UpdatedAt = string.IsNullOrWhiteSpace(state.UpdatedAt) ? NowIso() : state.UpdatedAt;
        var json = JsonSerializer.Serialize(state, JsonOpts);
        var dest = Path.Combine(Dir, Safe(state.AttemptId) + ".json");
        var raw = Encoding.UTF8.GetBytes(json);
        var sealedBytes = Seal(raw);
        File.WriteAllBytes(dest, sealedBytes);
    }

    public static LocalExamState? Load(string attemptId)
    {
        var path = Path.Combine(Dir, Safe(attemptId) + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var raw = Open(File.ReadAllBytes(path));
            return JsonSerializer.Deserialize<LocalExamState>(raw, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public static LocalExamState? FindRunning(string projectId)
    {
        if (!Directory.Exists(Dir) || string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }

        LocalExamState? newest = null;
        foreach (var file in Directory.GetFiles(Dir, "*.json"))
        {
            try
            {
                var state = JsonSerializer.Deserialize<LocalExamState>(Open(File.ReadAllBytes(file)), JsonOpts);
                if (state is null || state.ProjectId != projectId || state.Status is "submitted" or "abandoned")
                {
                    continue;
                }

                if (newest is null || string.CompareOrdinal(state.UpdatedAt, newest.UpdatedAt) > 0)
                {
                    newest = state;
                }
            }
            catch
            {
                // bỏ file hỏng
            }
        }

        return newest;
    }

    public static IEnumerable<LocalExamState> Pending()
    {
        if (!Directory.Exists(Dir))
        {
            yield break;
        }

        foreach (var file in Directory.GetFiles(Dir, "*.json"))
        {
            LocalExamState? state = null;
            try
            {
                state = JsonSerializer.Deserialize<LocalExamState>(Open(File.ReadAllBytes(file)), JsonOpts);
            }
            catch
            {
                continue;
            }

            if (state is not null)
            {
                yield return state;
            }
        }
    }

    public static void MarkSubmitted(string attemptId)
    {
        var state = Load(attemptId);
        if (state is null)
        {
            return;
        }

        state.Status = "submitted";
        state.PendingSubmit = null;
        state.PendingCheckpoint = null;
        state.UpdatedAt = NowIso();
        Save(state);
    }

    public static void ClearPending(string attemptId, bool checkpoint, bool submit)
    {
        var state = Load(attemptId);
        if (state is null)
        {
            return;
        }

        if (checkpoint)
        {
            state.PendingCheckpoint = null;
        }

        if (submit)
        {
            state.PendingSubmit = null;
        }

        Save(state);
    }

    static string Safe(string attemptId)
    {
        var chars = attemptId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }

    static string KeyPath => Path.Combine(Dir, ".key");

    static byte[] Key()
    {
        Directory.CreateDirectory(Dir);
        if (File.Exists(KeyPath))
        {
            return File.ReadAllBytes(KeyPath);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(KeyPath, key);
        try
        {
            File.SetAttributes(KeyPath, FileAttributes.Hidden);
        }
        catch
        {
            // Windows-only attribute
        }

        return key;
    }

    static byte[] Seal(byte[] plain)
    {
        var key = Key();
        var nonce = RandomNumberGenerator.GetBytes(16);
        var xor = new byte[plain.Length];
        for (var i = 0; i < plain.Length; i++)
        {
            xor[i] = (byte)(plain[i] ^ key[i % key.Length] ^ nonce[i % nonce.Length]);
        }

        using var sha = SHA256.Create();
        var mac = sha.ComputeHash(key.Concat(nonce).Concat(xor).ToArray());
        return nonce.Concat(mac).Concat(xor).ToArray();
    }

    static byte[] Open(byte[] sealedBytes)
    {
        if (sealedBytes.Length > 2 && sealedBytes[0] == (byte)'{')
        {
            return sealedBytes;
        }

        if (sealedBytes.Length < 48)
        {
            throw new InvalidDataException("local");
        }

        var key = Key();
        var nonce = sealedBytes[..16];
        var mac = sealedBytes[16..48];
        var xor = sealedBytes[48..];
        using var sha = SHA256.Create();
        var expect = sha.ComputeHash(key.Concat(nonce).Concat(xor).ToArray());
        if (!CryptographicOperations.FixedTimeEquals(mac, expect))
        {
            throw new InvalidDataException("mac");
        }

        var plain = new byte[xor.Length];
        for (var i = 0; i < xor.Length; i++)
        {
            plain[i] = (byte)(xor[i] ^ key[i % key.Length] ^ nonce[i % nonce.Length]);
        }

        return plain;
    }
}
