using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Services.Profile;

namespace AstraPlayers;

/// <summary>
/// MOD 入口：读配置、确认在线活动服务已经变成单例、打印启动日志。
/// </summary>
[Injectable(InjectionType.Singleton, 100)]
public sealed class AstraPlayersEntry : IOnLoad
{
    private const string ConfigFileName = "config.jsonc";

    private readonly ILogger<AstraPlayersEntry> _logger;
    private readonly ModHelper _modHelper;
    private readonly IServiceProvider _services;

    public AstraPlayersEntry(
        ILogger<AstraPlayersEntry> logger,
        ModHelper modHelper,
        IServiceProvider services)
    {
        _logger = logger;
        _modHelper = modHelper;
        _services = services;
    }

    /// <summary>启动自检结果：ProfileActivityService 是否确实是同一个实例。</summary>
    public static bool ActivityServiceIsSingleton { get; private set; }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        string modPath = _modHelper.GetAbsolutePathToModFolder(typeof(AstraPlayersEntry).Assembly);

        try
        {
            AstraPlayersConfig? loaded =
                _modHelper.GetJsonDataFromModFile<AstraPlayersConfig>(modPath, ConfigFileName);

            if (loaded is not null)
            {
                AstraPlayersConfig.Current = loaded;
            }
            else
            {
                _logger.LogWarning("[AstraPlayers] {File} 为空，使用默认配置", ConfigFileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AstraPlayers] 读取 {File} 失败，使用默认配置", ConfigFileName);
        }

        AstraPlayersConfig config = AstraPlayersConfig.Current;
        int window = Math.Clamp(config.OnlineWindowMinutes, 1, 1440);

        ProfileActivityService? first = _services.GetService<ProfileActivityService>();
        ProfileActivityService? second = _services.GetService<ProfileActivityService>();
        ActivityServiceIsSingleton = first is not null && ReferenceEquals(first, second);

        if (!config.Enabled)
        {
            _logger.LogInformation("[AstraPlayers] 配置 Enabled=false，MOD 已加载但 API 返回关闭状态");
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "[AstraPlayers] v1.0.0 已加载 | 在线窗口 {Window} 分钟 | 玩家活动服务单例={Singleton} | DI 构造器={DiApplied}",
            window,
            ActivityServiceIsSingleton,
            AstraPlayersDi.Applied);

        if (!ActivityServiceIsSingleton)
        {
            _logger.LogWarning(
                "[AstraPlayers] 玩家活动服务不是单例，在线人数可能一直为 0。请检查 AstraPlayersDi 是否被 SPT 调用。");
        }

        if (!AstraPlayersDi.Applied)
        {
            _logger.LogWarning(
                "[AstraPlayers] AstraPlayersDi 的 IOnDIConstruct 没有执行，可能是 SPT 版本不匹配。");
        }

        return Task.CompletedTask;
    }
}
