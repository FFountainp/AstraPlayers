# Astra Players API v1.0.0

给 **Astra bot** 查询 SPT 服务端在线玩家用的服务端 MOD。

- 适用：**SPT 4.1.5**（塔科夫 0.16.9.40743），服务端 .NET 10
- 类型：纯服务端 MOD（`SPT_Runtime\user\mods\AstraPlayers`）
- 性质：只读 API，不修改玩家档案、不改 `SPT_Data`
- 接口：`/astra/online`、`/astra/players`、`/astra/status`、`/astra/profiles`、`/astra/health`

---

## 1. 安装

1. 关闭 `SPT.Server.exe`。
2. 把压缩包解压到塔科夫离线版根目录，例如 `D:\tkf`。
3. 确认存在：

   ```
   D:\tkf\SPT_Runtime\user\mods\AstraPlayers\AstraPlayers.dll
   D:\tkf\SPT_Runtime\user\mods\AstraPlayers\config.jsonc
   ```

4. 启动 `D:\tkf\SPT_Runtime\SPT.Server.exe`。
5. 控制台出现下面这行即加载成功：

   ```
   [AstraPlayers] v1.0.0 已加载 | 在线窗口 10 分钟 | 玩家活动服务单例=True | DI 构造器=True
   ```

> 第一次启动如果 `玩家活动服务单例=False`，说明 SPT 版本差异导致在线状态无法共享；本机 4.1.5 实测为 `True`。
>
> 如果你手动双击 `SPT.Server.exe` 时提示缺少 `Microsoft.AspNetCore.App 10.0`，请安装 **ASP.NET Core Runtime 10.0**，或者按平时的方式通过 SPT 启动器启动服务端。

---

## 2. 快速测试

SPT 4.1.5 的 HTTP 服务实际是 **HTTPS**（自签证书），并且默认返回 **zlib 压缩**的 JSON。
请求时加 `responsecompressed: 0` 可以关闭压缩，直接拿到明文 JSON：

```powershell
curl.exe -k -H "responsecompressed: 0" https://127.0.0.1:6969/astra/online
```

或者只看可直接发到群里的文本：

```powershell
(curl.exe -k -s -H "responsecompressed: 0" https://127.0.0.1:6969/astra/status | ConvertFrom-Json).message
```

实测返回（当前服务端没有档案/没有玩家在线）：

```json
{
  "success": true,
  "onlineCount": 0,
  "names": [],
  "players": [],
  "windowMinutes": 10,
  "timestamp": "2026-10-26T18:50:17.4527902+08:00",
  "message": "服务器当前没有玩家在线"
}
```

有玩家在线时（示例）：

```json
{
  "success": true,
  "onlineCount": 1,
  "names": ["PlayerName"],
  "players": [
    {
      "profileId": "65f0a1b2c3d4e5f607182930",
      "nickname": "PlayerName",
      "aid": 1234567,
      "level": 42,
      "side": "Usec",
      "online": true
    }
  ],
  "windowMinutes": 10,
  "timestamp": "2026-10-26T19:02:00.0000000+08:00",
  "message": "服务器当前在线 1 人：PlayerName"
}
```

---

## 3. API 一览

基础地址：`https://127.0.0.1:6969`

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | `/astra/online` | **主接口**：在线人数 + 在线玩家详情 |
| GET | `/astra/players` | `/astra/online` 的别名 |
| GET | `/astra/status` | 精简版：`onlineCount` / `names` / `message` |
| GET | `/astra/profiles` | 服务器上所有档案（含离线），带 `online` 标记 |
| GET | `/astra/health` | MOD 自检（版本、在线窗口、单例状态、档案数） |

字段说明：

- `success`：接口是否成功
- `onlineCount`：当前在线人数
- `names`：在线玩家昵称数组
- `players`：在线玩家对象数组
  - `profileId`：档案 ID
  - `nickname`：游戏内 PMC 昵称（读不到时回退到账号名/档案 ID）
  - `aid`：账号 ID
  - `level`：PMC 等级
  - `side`：`Usec` / `Bear` / 空字符串
  - `online`：是否在线
- `windowMinutes`：在线判定窗口（分钟）
- `message`：可直接发给玩家的中文文本
- `timestamp`：服务器本地时间（ISO 8601）

### `/astra/health` 实测

```json
{
  "success": true,
  "mod": "AstraPlayers",
  "version": "1.0.0",
  "enabled": true,
  "onlineWindowMinutes": 10,
  "profileCount": 0,
  "activityServiceSingleton": true,
  "diConstructorApplied": true,
  "timestamp": "2026-10-26T18:50:17.3924851+08:00"
}
```

---

## 4. Astra bot 接入

### 推荐请求

- URL：`https://127.0.0.1:6969/astra/online`
- Method：`GET`
- Header：`responsecompressed: 0`
- TLS：SPT 使用自签证书，客户端需要忽略证书校验，或信任：
  `D:\tkf\SPT_Runtime\user\certs\server.crt`
- 轮询间隔：建议 30–60 秒

如果 Astra bot 有“HTTP 请求”节点，直接填上面的 URL 和 Header，然后从返回 JSON 里取：

- 在线人数：`onlineCount`
- 玩家名列表：`names`
- 直接发群：`message`

### curl

```bash
curl -k -H "responsecompressed: 0" https://127.0.0.1:6969/astra/online
```

### Python

```python
import requests, urllib3
urllib3.disable_warnings()

r = requests.get(
    "https://127.0.0.1:6969/astra/online",
    headers={"responsecompressed": "0"},
    verify=False,
    timeout=5,
)
data = r.json()
print(data["message"])
print(data["names"])
```

### Node.js

```js
const https = require("https");

https.get(
  {
    hostname: "127.0.0.1",
    port: 6969,
    path: "/astra/online",
    headers: { responsecompressed: "0" },
    rejectUnauthorized: false,
  },
  (res) => {
    let body = "";
    res.on("data", (chunk) => (body += chunk));
    res.on("end", () => {
      const data = JSON.parse(body);
      console.log(data.message);
    });
  }
);
```

### 如果 Astra bot 不能加 Header

SPT 默认返回的响应体是 **zlib 压缩**的，而且不带 `Content-Encoding` 头。
此时可以手动解压：

```python
import json, zlib, requests, urllib3
urllib3.disable_warnings()

raw = requests.get("https://127.0.0.1:6969/astra/online", verify=False).content
data = json.loads(zlib.decompress(raw).decode("utf-8"))
print(data["message"])
```

如果你使用的 bot 平台既不支持自定义 Header，也不能解 zlib，告诉我，我可以再补一个独立监听端口、返回纯文本/标准 JSON 的版本。

### 如果 Astra bot 不在本机

SPT 默认只监听 `127.0.0.1:6969`。Astra bot 必须和 SPT 在同一台机器上；
如果 bot 在另一台机器/容器里，需要自己做反向代理或内网穿透（推荐用 Caddy/nginx 加认证）。
**不要直接改 `SPT_Data\configs\http.json`**：`SPT_Data` 有 `checks.dat` 完整性清单，手改可能被判定为文件损坏。
也不要直接把 6969 端口暴露到公网；这个 API 没有内置鉴权。

---

## 5. 配置

`D:\tkf\SPT_Runtime\user\mods\AstraPlayers\config.jsonc`

```jsonc
{
  "enabled": true,

  // 在线判定窗口（分钟）
  // 最近 N 分钟内有过 GameStart / KeepAlive 的玩家算在线
  // 范围 1-1440
  "onlineWindowMinutes": 10,

  // 每次请求输出日志（调试用，默认关闭）
  "logRequests": false
}
```

修改后重启服务端生效。

---

## 6. 在线判定说明 / 限制

- 数据来源是 SPT 服务端的 `ProfileActivityService`：玩家登录游戏、以及客户端 KeepAlive 时都会更新活动时间。
- 默认窗口 10 分钟。玩家关掉游戏后，最多还会显示“在线”约 10 分钟；想更灵敏可以调成 3–5 分钟。
- 服务端重启后，活动记录清空；玩家重新登录/下次 KeepAlive 后会重新出现。
- 这是 **SPT 服务端视角**的在线玩家。
  - 同一个 SPT 服务端有多个不同档案的客户端登录时，会显示多个玩家。
  - **Fika 的 P2P 联机玩家**不一定都会在 SPT 服务端留下会话；如果他们要出现在这个接口里，需要 Fika 服务端侧的数据。当前机器没有安装 Fika 服务端，所以本 MOD 只统计 SPT 服务端自己的会话。
- 昵称优先读 PMC 的 `Info.Nickname`；读不到时回退到账号 `Username`，再回退到档案 ID。
- 标记为 `InvalidOrUnloadableProfile` 的档案会被跳过。

---

## 7. 排错

| 现象 | 原因 / 处理 |
|---|---|
| `404 Not Found` | MOD 没加载或服务端没重启；确认 DLL 在 `SPT_Runtime\user\mods\AstraPlayers`，控制台有 `[AstraPlayers]` 日志 |
| 返回乱码/以 `x` 开头 | 没加 `responsecompressed: 0`，拿到的是 zlib 压缩体；加 Header 或用 zlib 解压 |
| HTTPS 证书报错 | 用 `curl -k`、Python `verify=False`、Node `rejectUnauthorized:false`，或信任 `user\certs\server.crt` |
| `onlineCount` 一直是 0 | 没有客户端登录；或玩家已关闭游戏超过窗口时间；看 `/astra/health` 的 `activityServiceSingleton` 是否为 `true` |
| `onlineCount` 有人但 `names` 空 | 玩家档案还没加载/昵称字段为空；看 `/astra/profiles` 和 `profileCount` |
| `profileCount = 0` | `SPT_Runtime\user\profiles` 里没有档案；进一次游戏创建/加载档案后再看 |
| 服务端起不来 | 先移除 `user\mods\AstraPlayers` 目录，确认原服务端能启动；再看控制台日志 |
| 启动日志显示 `玩家活动服务单例=False` | SPT 版本差异；把日志发我，需要适配 |

服务端日志在 `SPT.Server.exe` 控制台；`[AstraPlayers]` 开头的行是本 MOD 的输出。

---

## 8. 卸载

关闭服务端，删除：

```
D:\tkf\SPT_Runtime\user\mods\AstraPlayers
```

重启服务端即可。

---

## 9. 源码构建

源码在 `AstraPlayers/` 目录，需要 .NET 10 SDK：

```powershell
cd AstraPlayers
dotnet build -c Release
```

产物：`AstraPlayers/bin/Release/AstraPlayers.dll`。

`AstraPlayers.csproj` 会从 NuGet 还原 `SPTarkov.Server.Core` / `SPTarkov.DI` / `SPTarkov.Common` 4.1.x 作为编译引用，不会把它们打进发布包。

仓库里的 `dist/AstraPlayers_v1.0.0.zip` 是可直接安装的发布包。

---

## 10. 版本

- **v1.0.0**（2026-10-26）：首个版本。在线人数、在线玩家昵称/等级/阵营、全部档案查询、Astra bot 友好的 `message` 和未压缩 JSON Header。

适用：SPT 4.1.5-RELEASE / 塔科夫 0.16.9.40743 / .NET 10。
