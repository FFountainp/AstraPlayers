namespace AstraPlayers;

/// <summary>
/// config.jsonc 对应的配置模型。SPT 的 JsonUtil 不区分属性大小写。
/// </summary>
public sealed record AstraPlayersConfig
{
    /// <summary>总开关。false 时所有 API 返回“已关闭”。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 在线判定窗口（分钟）：最近 N 分钟内有过 GameStart / KeepAlive 的玩家算在线。
    /// 默认 10 分钟，范围 1-1440。
    /// </summary>
    public int OnlineWindowMinutes { get; init; } = 10;

    /// <summary>是否在每次 API 请求时输出日志（调试用，默认关闭）。</summary>
    public bool LogRequests { get; init; } = false;

    /// <summary>入口加载后写入这里，路由读取同一份配置。</summary>
    public static AstraPlayersConfig Current { get; set; } = new();
}
