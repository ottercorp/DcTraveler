using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace DcTraveler
{
    [StructLayout(LayoutKind.Explicit, Size = 0x158)]
    public struct LobbyUIClientExposed
    {
        [FieldOffset(0x18)]
        public nint Context;
        [FieldOffset(0x158)]
        public byte State;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x1C)]
    public struct Vibration
    {
        // 左右马达控制？在北通上面似乎没区别
        // 我没PS或者XBOX手柄 :(
        [FieldOffset(0)]
        public uint unk0_0;
        [FieldOffset(4)]
        public uint unk1_0;
        [FieldOffset(8)]
        public float Scale0;

        [FieldOffset(0xC)]
        public uint unk0_1;
        [FieldOffset(0x10)]
        public uint unk1_1;
        [FieldOffset(0x14)]
        public float Scale1;

        [FieldOffset(0x18)]
        public float DurationTime;
    }
    internal unsafe static class GameFunctions
    {
        private unsafe delegate void ReturnToTitleDelegate(AgentLobby* agentLobby);
        private unsafe static ReturnToTitleDelegate returnToTitle;

        private unsafe delegate void ReleaseLobbyContextDelegate(NetworkModule* agentLobby);
        private unsafe static ReleaseLobbyContextDelegate releaseLobbyContext;

        public unsafe delegate void RequestVibrationDelegate(Vibration* vibration);
        public unsafe static RequestVibrationDelegate RequestVibration;
        static unsafe GameFunctions()
        {
            var returnToTitleAddr = Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? C6 87 ?? ?? ?? ?? ?? 33 C0 ");
            returnToTitle = Marshal.GetDelegateForFunctionPointer<ReturnToTitleDelegate>(returnToTitleAddr);

            var releaseLobbyContextAddr = Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B 85 ?? ?? ?? ?? 48 85 C0");
            releaseLobbyContext = Marshal.GetDelegateForFunctionPointer<ReleaseLobbyContextDelegate>(releaseLobbyContextAddr);

            var requestVibrationAddr = Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? 8D 4F 99");
            RequestVibration = Marshal.GetDelegateForFunctionPointer<RequestVibrationDelegate>(requestVibrationAddr);

        }

        public static void ResetTitleMovieTimer()
        {
            AgentLobby.Instance()->IdleTime = 0;
        }

        public static unsafe void RequestVibrationWhenReady()
        {
            var vibration = stackalloc Vibration[1];
            vibration->DurationTime = 1.0f;
            vibration->unk0_0 = 3;
            vibration->unk0_1 = 0;
            vibration->Scale0 = 100.0f;
            vibration->unk1_0 = 3;
            vibration->unk1_1 = 0;
            vibration->Scale1 = 100.0f;
            RequestVibration(vibration);
        }
        public static void ReturnToTitle()
        {
            returnToTitle(AgentLobby.Instance());
            Log.Information("Return to title");
        }

        public static void RefreshGameServer()
        {
            var framework = Framework.Instance();
            var networkModule = framework->GetNetworkModuleProxy()->NetworkModule;
            releaseLobbyContext(networkModule);
            var agentLobby = AgentLobby.Instance();
            var lobbyUIClient2 = (LobbyUIClientExposed*)Unsafe.AsPointer(ref agentLobby->LobbyData.LobbyUIClient);
            lobbyUIClient2->Context = 0;
            lobbyUIClient2->State = 0;
            Log.Information("Refresh Game host addresses");
        }

        public static void ChangeDevTestSid(string sid)
        {
            var agentLobby = AgentLobby.Instance();
            agentLobby->UnkUtf8Strings[0].SetString(sid);
            Log.Information("Refresh Dev.TestSid");
        }
        public static void ChangeGameServer(string lobbyHost, string saveDataHost, string gmServerHost)
        {
            //var lobbyAgent = Plugin.GameGui.GetAddonByName(name);
            var framework = Framework.Instance();
            var networkModule = framework->GetNetworkModuleProxy()->NetworkModule;
            networkModule->ActiveLobbyHost.SetString(lobbyHost);
            networkModule->LobbyHosts[0].SetString(lobbyHost);
            networkModule->SaveDataBankHost.SetString(saveDataHost);

            for (int i = 0; i < framework->DevConfig.ConfigCount; ++i)
            {
                var entry = framework->DevConfig.ConfigEntry[i];
                if (entry.Value.String == null) continue;
                string name = entry.Name.ToString();
                if (name == "GMServerHost")
                {
                    entry.Value.String->SetString(gmServerHost);
                }
                else if (name == "SaveDataBankHost")
                {
                    entry.Value.String->SetString(saveDataHost);
                }
                else if (name == "LobbyHost01")
                {
                    entry.Value.String->SetString(lobbyHost);
                }
            }
            Log.Information($"Change Game host addresses:LobbyHost:{lobbyHost},SaveDataBankHost:{saveDataHost},GmHost:{gmServerHost}");
        }

        public static string GetGameArgument(string key)
        {
            if (!key.EndsWith("="))
            {
                key = key + "=";
            }
            var gameWindow = Framework.Instance()->GameWindow;
            for (var i = 0; i < gameWindow->ArgumentCount; i++)
            {
                var arg = gameWindow->Arguments[i].ToString();
                if (arg.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(key.Length);
                }
            }
            throw new Exception($"未能从游戏参数中获取{key}");
        }
        public static unsafe void LoginInGame()
        {
            var ptr = Plugin.GameGui.GetAddonByName("_TitleMenu", 1);
            if (ptr == 0)
                return;
            var atkUnitBase = (AtkUnitBase*)ptr.Address;
            var loginGameButton = atkUnitBase->GetComponentButtonById(4);
            var loginGameButtonEvent = loginGameButton->AtkResNode->AtkEventManager.Event;
            Plugin.Framework.RunOnFrameworkThread(() => atkUnitBase->ReceiveEvent(AtkEventType.ButtonClick, 1, loginGameButtonEvent));
        }
    }
}
