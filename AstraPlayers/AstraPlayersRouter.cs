using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Utils;

namespace AstraPlayers;

/// <summary>
/// 给 Astra bot 用的只读 HTTP API：
///   GET /astra/online    在线人数 + 在线玩家详情
///   GET /astra/players   /astra/online 的别名
///   GET /astra/status    精简状态（人数 / 名字 / 可直接展示的 message）
///   GET /astra/profiles  服务器上所有档案（含离线），带 online 标记
///   GET /astra/health    MOD 自检
/// </summary>
[Injectable]
public sealed class AstraPlayersRouter : StaticRouter
{
    public AstraPlayersRouter(
        JsonUtil jsonUtil,
        HttpResponseUtil httpResponseUtil,
        SaveServer saveServer,
        ProfileActivityService activityService,
        ILogger<AstraPlayersRouter> logger)
        : base(jsonUtil, BuildRoutes(httpResponseUtil, saveServer, activityService, logger))
    {
    }

    private static List<RouteAction> BuildRoutes(
        HttpResponseUtil httpResponseUtil,
        SaveServer saveServer,
        ProfileActivityService activityService,
        ILogger<AstraPlayersRouter> logger)
    {
        object Respond(
            string route,
            Func<Dictionary<string, object?>> build,
            bool checkEnabled = true)
        {
            AstraPlayersConfig config = AstraPlayersConfig.Current;

            if (checkEnabled && !config.Enabled)
            {
                return httpResponseUtil.NoBody(DisabledPayload());
            }

            try
            {
                Dictionary<string, object?> payload = build();

                if (config.LogRequests)
                {
                    logger.LogInformation(
                        "[AstraPlayers] {Route} -> online={Online}",
                        route,
                        payload.TryGetValue("onlineCount", out object? count) ? count : "-");
                }

                return httpResponseUtil.NoBody(payload);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AstraPlayers] {Route} 处理失败", route);
                return httpResponseUtil.NoBody(ErrorPayload());
            }
        }

        return
        [
            new RouteAction(
                "/astra/online",
                (url, info, sessionId, output, ct) =>
                    new ValueTask<object>(Respond(
                        "/astra/online",
                        () => BuildOnlinePayload(saveServer, activityService)))),

            new RouteAction(
                "/astra/players",
                (url, info, sessionId, output, ct) =>
                    new ValueTask<object>(Respond(
                        "/astra/players",
                        () => BuildOnlinePayload(saveServer, activityService)))),

            new RouteAction(
                "/astra/status",
                (url, info, sessionId, output, ct) =>
                    new ValueTask<object>(Respond(
                        "/astra/status",
                        () => BuildStatusPayload(saveServer, activityService)))),

            new RouteAction(
                "/astra/profiles",
                (url, info, sessionId, output, ct) =>
                    new ValueTask<object>(Respond(
                        "/astra/profiles",
                        () => BuildProfilesPayload(saveServer, activityService)))),

            new RouteAction(
                "/astra/health",
                (url, info, sessionId, output, ct) =>
                    new ValueTask<object>(Respond(
                        "/astra/health",
                        () => BuildHealthPayload(saveServer),
                        checkEnabled: false)))
        ];
    }

    private static Dictionary<string, object?> BuildOnlinePayload(
        SaveServer saveServer,
        ProfileActivityService activityService)
    {
        int window = CurrentWindowMinutes();
        HashSet<string> onlineIds = GetOnlineIds(activityService, window);
        List<PlayerRow> rows = BuildRows(saveServer, onlineIds);

        List<PlayerRow> online = rows
            .Where(row => row.Online)
            .OrderBy(row => row.Nickname, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> names = online.Select(row => row.Nickname).ToList();

        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["onlineCount"] = online.Count,
            ["names"] = names,
            ["players"] = online.Select(RowToDict).ToList(),
            ["windowMinutes"] = window,
            ["timestamp"] = DateTimeOffset.Now.ToString("O"),
            ["message"] = online.Count == 0
                ? "服务器当前没有玩家在线"
                : $"服务器当前在线 {online.Count} 人：{string.Join("、", names)}"
        };
    }

    private static Dictionary<string, object?> BuildStatusPayload(
        SaveServer saveServer,
        ProfileActivityService activityService)
    {
        Dictionary<string, object?> online = BuildOnlinePayload(saveServer, activityService);

        return new Dictionary<string, object?>
        {
            ["success"] = online["success"],
            ["onlineCount"] = online["onlineCount"],
            ["names"] = online["names"],
            ["windowMinutes"] = online["windowMinutes"],
            ["timestamp"] = online["timestamp"],
            ["message"] = online["message"]
        };
    }

    private static Dictionary<string, object?> BuildProfilesPayload(
        SaveServer saveServer,
        ProfileActivityService activityService)
    {
        int window = CurrentWindowMinutes();
        HashSet<string> onlineIds = GetOnlineIds(activityService, window);
        List<PlayerRow> rows = BuildRows(saveServer, onlineIds);

        rows.Sort((a, b) =>
        {
            int byOnline = b.Online.CompareTo(a.Online);
            return byOnline != 0
                ? byOnline
                : string.Compare(a.Nickname, b.Nickname, StringComparison.OrdinalIgnoreCase);
        });

        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["total"] = rows.Count,
            ["onlineCount"] = rows.Count(row => row.Online),
            ["players"] = rows.Select(RowToDict).ToList(),
            ["windowMinutes"] = window,
            ["timestamp"] = DateTimeOffset.Now.ToString("O")
        };
    }

    private static Dictionary<string, object?> BuildHealthPayload(SaveServer saveServer)
    {
        int profileCount = 0;
        try
        {
            profileCount = saveServer.GetProfiles()?.Count ?? 0;
        }
        catch
        {
            // 自检接口不因为档案读取失败而整体报错
        }

        AstraPlayersConfig config = AstraPlayersConfig.Current;

        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["mod"] = "AstraPlayers",
            ["version"] = "1.0.0",
            ["enabled"] = config.Enabled,
            ["onlineWindowMinutes"] = CurrentWindowMinutes(),
            ["profileCount"] = profileCount,
            ["activityServiceSingleton"] = AstraPlayersEntry.ActivityServiceIsSingleton,
            ["diConstructorApplied"] = AstraPlayersDi.Applied,
            ["timestamp"] = DateTimeOffset.Now.ToString("O")
        };
    }

    private static int CurrentWindowMinutes()
    {
        return Math.Clamp(AstraPlayersConfig.Current.OnlineWindowMinutes, 1, 1440);
    }

    private static HashSet<string> GetOnlineIds(
        ProfileActivityService activityService,
        int windowMinutes)
    {
        List<string>? ids = activityService.GetActiveProfileIdsWithinMinutes(windowMinutes);

        return ids is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
    }

    private static List<PlayerRow> BuildRows(
        SaveServer saveServer,
        HashSet<string> onlineIds)
    {
        List<PlayerRow> rows = [];

        Dictionary<SPTarkov.Server.Core.Models.Common.MongoId, SPTarkov.Server.Core.Models.Eft.Profile.SptProfile>? profiles =
            saveServer.GetProfiles();

        if (profiles is null)
        {
            return rows;
        }

        foreach (KeyValuePair<SPTarkov.Server.Core.Models.Common.MongoId, SPTarkov.Server.Core.Models.Eft.Profile.SptProfile> pair in profiles)
        {
            SPTarkov.Server.Core.Models.Eft.Profile.SptProfile? profile = pair.Value;
            if (profile is null)
            {
                continue;
            }

            SPTarkov.Server.Core.Models.Eft.Profile.Info? profileInfo = profile.ProfileInfo;
            if (profileInfo?.InvalidOrUnloadableProfile == true)
            {
                continue;
            }

            string profileId = pair.Key.ToString();
            string? nickname = profile.CharacterData?.PmcData?.Info?.Nickname;

            if (string.IsNullOrWhiteSpace(nickname))
            {
                nickname = profileInfo?.Username;
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                nickname = profileId;
            }

            SPTarkov.Server.Core.Models.Eft.Common.PmcData? pmc = profile.CharacterData?.PmcData;
            SPTarkov.Server.Core.Models.Eft.Common.Tables.Info? info = pmc?.Info;

            rows.Add(new PlayerRow(
                profileId,
                nickname,
                pmc?.Aid ?? 0,
                info?.Level ?? 0,
                info?.Side ?? string.Empty,
                onlineIds.Contains(profileId)));
        }

        return rows;
    }

    private static Dictionary<string, object?> RowToDict(PlayerRow row)
    {
        return new Dictionary<string, object?>
        {
            ["profileId"] = row.ProfileId,
            ["nickname"] = row.Nickname,
            ["aid"] = row.Aid,
            ["level"] = row.Level,
            ["side"] = row.Side,
            ["online"] = row.Online
        };
    }

    private static Dictionary<string, object?> DisabledPayload()
    {
        return new Dictionary<string, object?>
        {
            ["success"] = false,
            ["error"] = "AstraPlayers is disabled",
            ["message"] = "AstraPlayers 已在 config.jsonc 中关闭",
            ["timestamp"] = DateTimeOffset.Now.ToString("O")
        };
    }

    private static Dictionary<string, object?> ErrorPayload()
    {
        return new Dictionary<string, object?>
        {
            ["success"] = false,
            ["error"] = "internal error",
            ["message"] = "查询失败，请查看服务端日志",
            ["timestamp"] = DateTimeOffset.Now.ToString("O")
        };
    }

    private readonly record struct PlayerRow(
        string ProfileId,
        string Nickname,
        int Aid,
        int Level,
        string Side,
        bool Online);
}
