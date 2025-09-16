using Dalamud.Utility;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DcTraveler.GameUi
{
    internal unsafe static class LobbyDKT
    {
        public static unsafe void Open()
        {
            var raptureAtkModule = RaptureAtkModule.Instance();
            var targetBytes = Encoding.UTF8.GetBytes("LobbyDKT");
            var AddonNameId = raptureAtkModule->AddonNames.FindIndex(x => x.AsSpan().SequenceEqual(targetBytes.AsSpan()));
            if (AddonNameId != -1) {
                var values = stackalloc AtkValue[1];
                raptureAtkModule->OpenAddon((uint)AddonNameId, 1, values, null, 5, 0, 11);
            }
        }

        public static unsafe void Close()
        {
            var addonLobbyDKT = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("LobbyDKT").Address;
            if (addonLobbyDKT != null)
            {
                addonLobbyDKT->Close(true);
            }
        }
        public static unsafe void SetMessage(string message)
        {
            var addonLobbyDKT = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("LobbyDKT").Address;
            if (addonLobbyDKT != null) {
                addonLobbyDKT->AtkValues[0].SetManagedString(message);
                addonLobbyDKT->OnRefresh(addonLobbyDKT->AtkValuesCount, addonLobbyDKT->AtkValues);
            }
        }
        public static int CurrentStatusCode = -1;
        public static int CurrentEstimatedTime = -1;
        public static unsafe void SetStatus(int statusCode, int estimatedTime)
        {
            if (statusCode == CurrentStatusCode && estimatedTime == CurrentEstimatedTime)
            {
                return;
            }
            CurrentEstimatedTime = estimatedTime;
            CurrentStatusCode = statusCode;
            var statusText = "";
            var timeText = $"{estimatedTime}";

            if (statusCode == 0 || statusCode == 1)
            {
                statusText = "角色检查中";
            }
            else if (statusCode == 3 || statusCode == 4)
            {
                statusText = "处理中";
            }
            else if (statusCode == 5)
            {
                statusText = "已完成";
            }
            var text = $"""
                角色正在进行超域传送，请等待传送完成。
                预测需要时间：{timeText} 分钟
                目前状态: {statusText}
                """;
            SetMessage(text);
        }
    }
}
