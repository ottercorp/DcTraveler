using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DcTraveler
{
    public class Area
    {
        [JsonPropertyName("state")]
        public int State { get; set; }
        [JsonPropertyName("areaId")]
        public int AreaId { get; set; }
        [JsonPropertyName("areaName")]
        public string AreaName { get; set; }
        [JsonPropertyName("groups")]
        public List<Group> GroupList { get; set; }
        public void SetAreaForGroup()
        {
            foreach (var group in GroupList)
            {
                group.AreaName = this.AreaName;
                group.AreaId = this.AreaId;
            }
        }
    }

    public class Character
    {
        [JsonPropertyName("roleId")]
        public string ContentId { get; set; }
        [JsonPropertyName("roleName")]
        public string Name { get; set; }
        public int AreaId { get; set; }
        public int GroupId { get; set; }
        public string ToQueryString()
        {
            // Shit!
            return $"{{\"roleId\":\"{ContentId}\",\"roleName\":\"{Name}\",\"key\":0}}";
        }
    }
    public class Group
    {
        public int AreaId { get; set; }
        public string AreaName { get; set; }
        [JsonPropertyName("groupId")]
        public int GroupId { get; set; }
        [JsonPropertyName("amount")]
        public int Amount { get; set; }
        [JsonPropertyName("groupName")]
        public string GroupName { get; set; }
        [JsonPropertyName("queueTime")]
        public int? QueueTime { get; set; }
        [JsonPropertyName("groupCode")]
        public string GroupCode { get; set; }
    }
    public class OrderSatus
    {
        // 5 成功
        // 2 需要确认
        // 0,1 检查中
        // 3,4 处理中
        // -1 预检查失败
        // -5 传送失败
        public int Status { get; set; }
        public string CheckMessage { get; set; }
        public string MigrationMessage { get; set; }
    }

    public class MigrationOrders
    {
        public int TotalCount { get; set; }
        public int TotalPageNum { get; set; }
        public MigrationOrder[] Orders { get; set; }
    }
    public class MigrationOrder
    {
        [JsonPropertyName("orderId")]
        public string OrderId { get; set; }
        [JsonPropertyName("roleId")]
        public string ContentId { get; set; }
        [JsonPropertyName("groupId")]
        public int GroupId { get; set; }
        [JsonPropertyName("groupCode")]
        public string GroupCode { get; set; }
        [JsonPropertyName("groupName")]
        public string GroupName { get; set; }
        [JsonPropertyName("createTime")]
        public string CreateTime { get; set; }
    }

    public class RpcRequest
    {
        public required string Method { get; set; }
        public required object[] Params { get; set; }
    }

    public class RpcResponse
    {
        public required object Result { get; set; }
        public required string Error { get; set; }
    }

    internal sealed class DcTravelClient
    {
        private string apiUrl = string.Empty;
        private HttpClient httpClient { get; set; }
        public static List<Area> CachedAreas { get; set; } = new List<Area>();
        public static bool IsValid = false;
        public DcTravelClient(int port, bool useEncrypt = true)
        {
            this.apiUrl = $"http://127.0.0.1:{port}/dctravel/";
            Log.Information($"DcTravelClient API URL:{this.apiUrl}");
            this.httpClient = new HttpClient();
            Task.Run(() =>
            {
                CachedAreas = this.QueryGroupListTravelSource().GetAwaiter().GetResult();
                IsValid = true;
            });
        }

        //Response:{"Result":"GM017624122025063000313700001006","Error":null}

        public async Task<T> RequestApi<T>(object[] objs, [CallerMemberName] string? method = null)
        {
            var rpcRequest = new RpcRequest { Method = method!, Params = objs };
            var jsonPayload = JsonSerializer.Serialize(rpcRequest);
            Log.Debug($"Request:{jsonPayload}");
            var request = new HttpRequestMessage(HttpMethod.Post, this.apiUrl) { Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json") };
            var response = await this.httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Log.Debug($"Response:{content}");
            var rpcResponse = JsonSerializer.Deserialize<RpcResponse>(content);
            if (rpcResponse?.Error != null)
            {
                throw new Exception(rpcResponse.Error);
            }
            if (rpcResponse!.Result is JsonElement element)
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)element.GetString();
                }
                else
                {
                    return element.Deserialize<T>();
                }
            }
            else
            {
                return (T)Convert.ChangeType(rpcResponse.Result, typeof(T));
            }
        }
        public async Task<List<Area>> QueryGroupListTravelSource()          
        {
            return await RequestApi<List<Area>>(new object[] { });
        }

        public async Task<List<Area>> QueryGroupListTravelTarget(int areaId, int groupId)
        {
            return await RequestApi<List<Area>>(new object[] { areaId, groupId });
        }

        public async Task<List<Character>> QueryRoleList(int areaId, int groupId)
        {
            return await RequestApi<List<Character>>(new object[] { areaId, groupId });
        }
        public async Task<int> QueryTravelQueueTime(int areaId, int groupId)
        {
            return await RequestApi<int>(new object[] { areaId, groupId });
        }
        public async Task<string> TravelOrder(Group targetGroup, Group sourceGroup, Character character)
        {
            return await RequestApi<string>(new object[] { targetGroup, sourceGroup, character });
        }

        public async Task<OrderSatus> QueryOrderStatus(string orderId)
        {
            return await RequestApi<OrderSatus>(new object[] { orderId });
        }

        public async Task<MigrationOrders> QueryMigrationOrders(int pageIndex = 1)
        {
            return await RequestApi<MigrationOrders>(new object[] { pageIndex });
        }

        public async Task<string> TravelBack(string orderId, int currentGroupId, string currentGroupCode, string currentGroupName)
        {
            return await RequestApi<string>(new object[] { orderId, currentGroupId, currentGroupCode, currentGroupName });
        }
        public async Task<string> RefreshGameSessionId()
        {
            return await RequestApi<string>(new object[] { });
        }

        public async Task MigrationConfirmOrder(string orderId, bool confirmed)
        {
            await RequestApi<string>(new object[] { orderId, confirmed });
        }
    }
}
