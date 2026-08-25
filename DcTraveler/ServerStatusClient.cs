using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DcTraveler;

internal static class ServerStatusClient
{
    private const string Endpoint = "https://ff14act.web.sdo.com/api/serverStatus/getServerStatus";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<List<Area>> QueryAsync(
        IReadOnlyCollection<SdoArea> configuredAreas,
        CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.GetAsync(Endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<ServerStatusResponse>(
            responseStream,
            JsonOptions,
            cancellationToken);

        if (payload is null || !payload.IsSuccess || payload.Data is null)
            throw new Exception(payload?.Errormsg ?? "服务器状态接口返回无效数据");

        var configuredByName = configuredAreas
            .Where(area => !string.IsNullOrWhiteSpace(area.AreaName))
            .ToDictionary(area => area.AreaName, StringComparer.Ordinal);

        var areas = new List<Area>();
        foreach (var statusArea in payload.Data)
        {
            if (string.IsNullOrWhiteSpace(statusArea.AreaName) ||
                !configuredByName.TryGetValue(statusArea.AreaName, out var configuredArea))
            {
                continue;
            }

            var area = new Area
            {
                AreaId = int.TryParse(configuredArea.Areaid, out var areaId) ? areaId : 0,
                AreaName = configuredArea.AreaName,
                State = configuredArea.AreaStat,
                GroupList = (statusArea.Group ?? new List<ServerStatusGroup>())
                    .Where(group => !string.IsNullOrWhiteSpace(group.Name))
                    .Select((group, index) => new Group
                    {
                        AreaId = areaId,
                        AreaName = configuredArea.AreaName,
                        GroupId = index + 1,
                        GroupName = group.Name!,
                        // The status API is display-only; login still targets the area.
                        GroupCode = group.Name!,
                    })
                    .ToList(),
            };

            areas.Add(area);
        }

        return areas;
    }

    private sealed class ServerStatusResponse
    {
        public bool IsSuccess { get; set; }
        public List<ServerStatusArea>? Data { get; set; }
        public string? Errormsg { get; set; }
    }

    private sealed class ServerStatusArea
    {
        public string? AreaName { get; set; }
        public List<ServerStatusGroup>? Group { get; set; }
    }

    private sealed class ServerStatusGroup
    {
        public string? Name { get; set; }
    }
}
