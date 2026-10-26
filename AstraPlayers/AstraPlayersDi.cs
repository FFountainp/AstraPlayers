using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Services.Profile;

namespace AstraPlayers;

/// <summary>
/// SPT 4.x 的 [Injectable] 默认生命周期是 Transient，而 ProfileActivityService
/// 内部保存着“哪些玩家最近活跃”的字典。如果按 Transient 注入，
/// GameController 写入的在线状态和查询路由读到的会是两个不同实例。
///
/// 这里在容器构建前把它替换成 Singleton，让登录、KeepAlive 和本 MOD 的查询
/// 共用同一份在线状态。IOnDIConstruct 在核心服务全部注册完之后执行。
/// </summary>
public sealed class AstraPlayersDi : IOnDIConstruct
{
    public static bool Applied { get; private set; }

    public static Task OnDIConstructAsync(
        IServiceCollection serviceCollection,
        CancellationToken cancellationToken)
    {
        serviceCollection.Replace(
            ServiceDescriptor.Singleton(typeof(ProfileActivityService), typeof(ProfileActivityService)));
        Applied = true;
        return Task.CompletedTask;
    }
}
