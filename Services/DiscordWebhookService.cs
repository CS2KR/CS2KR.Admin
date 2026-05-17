using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Services;

public sealed class DiscordWebhookService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly DiscordConfig _config;
    private readonly ILogger _logger;

    public DiscordWebhookService(DiscordConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    private static readonly Dictionary<string, int> _colors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ban"] = 0xFF4444,
        ["unban"] = 0x44FF44,
        ["ban_edit"] = 0xFFAA44,
        ["mute"] = 0xFFAA44,
        ["unmute"] = 0x44FF44,
        ["mute_edit"] = 0xFFAA44,
        ["kick"] = 0xFFAA00,
        ["slay"] = 0xFF8800,
        ["slap"] = 0xFFCC00,
        ["rcon"] = 0x888888,
        ["map"] = 0x44AAFF,
        ["admin_create"] = 0x44AAFF,
        ["admin_delete"] = 0xFF4444,
        ["reload_admins"] = 0x888888,
    };

    public void Send(string action, Dictionary<string, object?> fields)
    {
        if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.WebhookUrl)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var color = _colors.TryGetValue(action, out var c) ? c : 0x4FC3F7;
                var embedFields = fields
                    .Where(kv => kv.Value != null && !string.IsNullOrWhiteSpace(kv.Value.ToString()))
                    .Select(kv => new
                    {
                        name = kv.Key,
                        value = (kv.Value?.ToString() ?? "").Length > 1024
                            ? kv.Value!.ToString()![..1021] + "..."
                            : kv.Value!.ToString(),
                        inline = true,
                    })
                    .ToArray();

                var payload = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = $"CS2KR: {action}",
                            color,
                            fields = embedFields,
                            timestamp = DateTime.UtcNow.ToString("o"),
                            footer = new { text = "CS2KR Admin Panel" },
                        },
                    },
                };

                using var resp = await _http.PostAsJsonAsync(_config.WebhookUrl, payload);
                if (!resp.IsSuccessStatusCode)
                    _logger.LogWarning("Discord webhook 응답 {Code}", (int)resp.StatusCode);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Discord webhook 전송 실패 action={A}", action);
            }
        });
    }
}
