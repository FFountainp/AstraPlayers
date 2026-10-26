using SPTarkov.Server.Core.Models.Spt.Mod;
using SemVersion = SemanticVersioning.Version;
using SemRange = SemanticVersioning.Range;

namespace AstraPlayers;

/// <summary>
/// 服务端 MOD 元数据。每个 MOD 程序集必须有且只有一个 IModMetadata 实现。
/// </summary>
public sealed record AstraPlayersMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.dsh.astraplayers";
    public string Name { get; init; } = "Astra Players API";
    public string Author { get; init; } = "DSH";
    public List<string> Contributors { get; init; } = [];
    public SemVersion Version { get; init; } = new("1.0.0");
    public SemRange SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string> Incompatibilities { get; init; } = [];
    public Dictionary<string, SemRange> ModDependencies { get; init; } = [];
    public string Url { get; init; } = "https://github.com/dsh/astra-players";
    public string License { get; init; } = "MIT";
}
