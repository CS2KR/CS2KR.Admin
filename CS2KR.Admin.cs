using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Events;
using CS2KR.Admin.Commands;
using CS2KR.Admin.Database;
using CS2KR.Admin.Services;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin;

[MinimumApiVersion(367)]
public sealed class CS2KRAdminPlugin : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "CS2KR Admin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "CS2KR (contact@cs2.kr)";
    public override string ModuleDescription => "CS2.KR 통합 어드민 시스템 (CS2-SimpleAdmin 대체)";

    public Config Config { get; set; } = new();

    public DbConnectionFactory Db { get; private set; } = null!;
    public AdminRepository AdminRepo { get; private set; } = null!;
    public BanRepository BanRepo { get; private set; } = null!;
    public MuteRepository MuteRepo { get; private set; } = null!;
    public EventRepository EventRepo { get; private set; } = null!;
    public ServerRepository ServerRepo { get; private set; } = null!;

    public PermissionService Permissions { get; private set; } = null!;
    public EnforcementService Enforcement { get; private set; } = null!;
    public DiscordWebhookService Discord { get; private set; } = null!;
    public EventPollService EventPoll { get; private set; } = null!;
    public ServerIdentityService Identity { get; private set; } = null!;

    public int? ServerId { get; internal set; }

    public void OnConfigParsed(Config config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Db = new DbConnectionFactory(Config.Database);

        AdminRepo = new AdminRepository(Db);
        BanRepo = new BanRepository(Db);
        MuteRepo = new MuteRepository(Db);
        EventRepo = new EventRepository(Db);
        ServerRepo = new ServerRepository(Db);

        Discord = new DiscordWebhookService(Config.Discord, Logger);
        Permissions = new PermissionService(this);
        Enforcement = new EnforcementService(this);
        Identity = new ServerIdentityService(this);
        EventPoll = new EventPollService(this);

        BanCommands.Register(this);
        MuteCommands.Register(this);
        PlayerCommands.Register(this);
        ServerCommands.Register(this);
        ChatCommands.Register(this);
        MenuCommand.Register(this);

        RegisterListeners();

        Logger.LogInformation("CS2KR Admin v{Version} 로드됨", ModuleVersion);
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        // ConVar 읽기는 메인 스레드에서 수행 (native 콜 경고 방지)
        var (address, port) = Identity.ReadBindFromConVars();

        _ = Task.Run(async () =>
        {
            try
            {
                await Identity.ResolveAsync(address, port);
                await Permissions.ReloadAsync();
                await EventPoll.InitializeCursorAsync();
                EventPoll.Start();

                Server.NextFrame(() =>
                {
                    // 메인스레드에서 현재 접속자 SteamID 수집 후 백그라운드에서 enforce
                    var sids = Utilities.GetPlayers()
                        .Where(p => p.IsValid && !p.IsBot)
                        .Select(p => p.SteamID)
                        .ToList();

                    _ = Task.Run(async () =>
                    {
                        foreach (var sid in sids) await Enforcement.OnPlayerJoinAsync(sid);
                    });
                });

                Logger.LogInformation("CS2KR Admin 초기화 완료. server_id={ServerId}", ServerId?.ToString() ?? "NULL(전역)");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "CS2KR Admin 초기화 실패");
            }
        });
    }

    public override void Unload(bool hotReload)
    {
        EventPoll?.Stop();
        Logger.LogInformation("CS2KR Admin 언로드됨");
    }

    private void RegisterListeners()
    {
        RegisterEventHandler<EventPlayerConnectFull>((ev, info) =>
        {
            var player = ev.Userid;
            if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

            var steamId = player.SteamID;
            _ = Task.Run(() => Enforcement.OnPlayerJoinAsync(steamId));

            return HookResult.Continue;
        });

        // 채팅 차단 (GAG/SILENCE)
        RegisterEventHandler<EventPlayerChat>((ev, info) =>
        {
            var player = Utilities.GetPlayerFromUserid(ev.Userid);
            if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;
            if (Enforcement.IsGagged(player.SteamID))
            {
                player.PrintToChat($"{Config.ChatPrefix}채팅이 차단되어 있습니다.");
                return HookResult.Stop;
            }
            return HookResult.Continue;
        });

        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player == null) return;
            Enforcement.OnPlayerDisconnect(player.SteamID);
        });
    }
}
