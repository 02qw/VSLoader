using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ContextMenuCapabilityTrustService
{
    private static readonly object SyncRoot = new();
    private readonly string trustPath;
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public ContextMenuCapabilityTrustService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VSLoader",
            "context-menu-capability-trust.json"))
    {
    }

    public ContextMenuCapabilityTrustService(string trustPath)
    {
        this.trustPath = trustPath;
    }

    public bool IsTrusted(ContextMenuCapabilityDefinition definition)
    {
        lock (SyncRoot)
        {
            var state = Load();
            var hash = ComputeHash(definition);
            return state.Entries.Any(entry =>
                string.Equals(entry.Id, definition.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Hash, hash, StringComparison.Ordinal));
        }
    }

    public SaveResult Trust(ContextMenuCapabilityDefinition definition)
    {
        try
        {
            lock (SyncRoot)
            {
                var state = Load();
                state.Entries.RemoveAll(entry => string.Equals(entry.Id, definition.Id, StringComparison.OrdinalIgnoreCase));
                state.Entries.Add(new TrustEntry { Id = definition.Id, Hash = ComputeHash(definition) });
                Save(state);
            }

            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail($"保存命令信任状态失败：{ex.Message}");
        }
    }

    public SaveResult Revoke(string capabilityId)
    {
        try
        {
            lock (SyncRoot)
            {
                var state = Load();
                state.Entries.RemoveAll(entry => string.Equals(entry.Id, capabilityId, StringComparison.OrdinalIgnoreCase));
                Save(state);
            }

            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail($"清除命令信任状态失败：{ex.Message}");
        }
    }

    internal static string ComputeHash(ContextMenuCapabilityDefinition definition)
    {
        var config = definition.PowerShell ?? new PowerShellCapabilityConfig();
        var content = string.Join(
            "\n",
            definition.Kind ?? string.Empty,
            config.Script ?? string.Empty,
            config.WorkingDirectoryMode ?? string.Empty,
            config.ExecutionMode ?? string.Empty,
            definition.RequiresExistingTargetPath ? "1" : "0");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    private TrustState Load()
    {
        try
        {
            if (!File.Exists(trustPath))
            {
                return new TrustState();
            }

            var state = JsonSerializer.Deserialize<TrustState>(File.ReadAllText(trustPath), jsonOptions);
            if (state is null)
            {
                return new TrustState();
            }

            state.Entries ??= [];
            return state;
        }
        catch
        {
            return new TrustState();
        }
    }

    private void Save(TrustState state)
    {
        var directory = Path.GetDirectoryName(trustPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(trustPath, JsonSerializer.Serialize(state, jsonOptions));
    }

    private sealed class TrustState
    {
        public List<TrustEntry> Entries { get; set; } = [];
    }

    private sealed class TrustEntry
    {
        public string Id { get; set; } = string.Empty;

        public string Hash { get; set; } = string.Empty;
    }
}
