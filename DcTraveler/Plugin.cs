using Dalamud;
using Dalamud.Game;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Hooking;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DcTraveler.GameUi;
using DcTraveler.Windows;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Markup;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonRelicNoteBook;
using Task = System.Threading.Tasks.Task;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;
namespace DcTraveler;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface DalamudPluginInterface { get; private set; } = null!;
    [PluginService] internal static ITitleScreenMenu TitleScreenMenu { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    //private const string CommandName = "/pmycommand";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("DcTraveler");
    //private ConfigWindow ConfigWindow { get; init; }
    //private MainWindow MainWindow { get; init; }
    private WorldSelectorWindows WorldSelectorWindows { get; init; }
    //private WaitingWindow WaitingWindow { get; init; }
    private DcGroupSelctorWindow DcGroupSelctorWindow { get; init; }

    internal DcTravelClient? DcTravelClient = null;
    internal static SdoArea[] SdoAreas = null!;
    internal static IFontHandle Font { get; private set; } = null!;
    internal string? InitException { get; private set; }
    internal TitleScreenButton TitleScreenButton { get; private set; }
    public unsafe Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        SetupFont();

        //MainWindow = new MainWindow(this);
        //WindowSystem.AddWindow(MainWindow);

        WorldSelectorWindows = new WorldSelectorWindows();
        //WaitingWindow = new WaitingWindow();
        DcGroupSelctorWindow = new DcGroupSelctorWindow(this);

        WindowSystem.AddWindow(WorldSelectorWindows);
        //WindowSystem.AddWindow(WaitingWindow);
        WindowSystem.AddWindow(DcGroupSelctorWindow);
        PluginInterface.UiBuilder.Draw += DrawUI;
        this.TitleScreenButton = new TitleScreenButton(DalamudPluginInterface, TitleScreenMenu, TextureProvider, this);

        ContextMenu.OnMenuOpened += this.OnContextMenuOpened;
        var port = 0;
        try
        {
            port = int.Parse(GameFunctions.GetGameArgument("XL.DcTraveler"));
            Log.Information($"Use port:{port}");
            var hostInfoString = GameFunctions.GetGameArgument("XL.LobbyHosts");
            byte[] decodedBytes = Convert.FromBase64String(hostInfoString);
            string decodedJsonString = Encoding.UTF8.GetString(decodedBytes);
            SdoAreas = JsonConvert.DeserializeObject<SdoArea[]>(decodedJsonString);
            Log.Information($"Got {SdoAreas!.Length} area hosts");
            DcTravelClient = new DcTravelClient(port);
        }
        catch (Exception ex)
        {
            InitException = ex.Message;
            Log.Error(ex.ToString());
        }
        //MainWindow.IsOpen = true;
    }

    public static void SetupFont()
    {
        Font?.Dispose();
        Font = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(buildToolkit =>
        {
            buildToolkit.OnPreBuild(tk =>
            {
                var config = new SafeFontConfig { SizePx = 40 };
                var font = tk.AddDalamudAssetFont(DalamudAsset.NotoSansScMedium, config);
                config.MergeFont = font;
                tk.AddGameSymbol(config);
                tk.SetFontScaleMode(font, FontScaleMode.UndoGlobalScale);
            });
        });
    }

    internal void OpenDcSelectWindow()
    {
        DcGroupSelctorWindow.Open();
    }

    private unsafe void OnContextMenuOpened(IMenuOpenedArgs args)
    {
        if (args.AddonPtr != 0 || args.MenuType != ContextMenuType.Default)
        {
            return;
        }
        if (GameGui.GetAddonByName("_CharaSelectListMenu", 1) == 0)
        {
            return;
        }
        var agentLobby = AgentLobby.Instance();
        var selectedCharacterContentId = agentLobby->SelectedCharacterContentId;
        var currentCharacterEntry = agentLobby->LobbyData.CharaSelectEntries[agentLobby->SelectedCharacterIndex].Value;
        var currentWorldId = currentCharacterEntry->CurrentWorldId;
        var homeWorldId = currentCharacterEntry->HomeWorldId;
        var currentCharacterName = currentCharacterEntry->NameString;
        var isDcTravling = currentCharacterEntry->LoginFlags == CharaSelectCharacterEntryLoginFlags.Unk32 || currentCharacterEntry->LoginFlags == CharaSelectCharacterEntryLoginFlags.DCTraveling;
        if (isDcTravling)
        {
            args.AddMenuItem(new MenuItem
            {
                Name = "超域返回",
                OnClicked = (clickedArgs) => Travel(homeWorldId, currentWorldId, selectedCharacterContentId, true, (currentCharacterEntry->LoginFlags == CharaSelectCharacterEntryLoginFlags.Unk32), currentCharacterName),
                Prefix = Dalamud.Game.Text.SeIconChar.CrossWorld,
                PrefixColor = 48,
                IsEnabled = true
            });
        }
        else
        {
            args.AddMenuItem(new MenuItem
            {
                Name = "超域传送",
                OnClicked = (clickedArgs) => Travel(0, currentWorldId, selectedCharacterContentId, false, false, currentCharacterName),
                Prefix = Dalamud.Game.Text.SeIconChar.CrossWorld,
                PrefixColor = 48,
                IsEnabled = (currentWorldId == homeWorldId)
            });
        }
    }

    private void Travel(int targetWorldId, int currentWorldId, ulong contentId, bool isBack, bool needSelectCurrentWorld, string currentCharacterName)
    {
        var title = isBack ? "超域返回" : "超域传送";

        if (InitException != null)
        {
            MessageBoxWindow.Show(WindowSystem, title, InitException!);
            return;
        }
        if (DcTravelClient == null || !DcTravelClient.IsValid)
        {
            MessageBoxWindow.Show(WindowSystem, title, "无法连接超域API服务,请检查XL。");
            Log.Error("Can not connect to XL");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                var worldSheet = DataManager.GetExcelSheet<World>();
                var currentWorld = worldSheet.GetRow((uint)currentWorldId);
                var currentDcGroupName = currentWorld.DataCenter.Value.Name.ToString();
                var orderId = string.Empty;
                var targetDcGroupName = string.Empty;
                var estimatedTime = 0;
                if (isBack)
                {
                    var targetWorld = worldSheet.GetRow((uint)targetWorldId);
                    targetDcGroupName = targetWorld.DataCenter.Value.Name.ToString();
                    var currentGroup = DcTravelClient.CachedAreas.First(x => x.AreaName == currentDcGroupName).GroupList.First(x => x.GroupCode == currentWorld.InternalName.ToString());
                    if (needSelectCurrentWorld)
                    {
                        var selectWorld = await WorldSelectorWindows.OpenTravelWindow(true, false, true, DcTravelClient.CachedAreas, currentDcGroupName, currentGroup.GroupCode, targetDcGroupName, currentWorld.Name.ToString());
                        if (selectWorld == null)
                        {
                            return;
                        }
                        currentGroup = selectWorld.Source;
                    }
                    Log.Information($"正在返回:{currentWorld.Name}@{currentDcGroupName} -> {targetWorld.Name}@{targetDcGroupName}");
                    MigrationOrder order;
                    order = GetTravelingOrder(contentId);
                    Log.Information($"Find back order: {order.OrderId}");
                    await Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
                    orderId = await DcTravelClient.TravelBack(order.OrderId, currentGroup.GroupId, currentGroup.GroupCode, currentGroup.GroupName);
                    Log.Information($"Get an order: {orderId}");
                }
                else
                {
                    var areas = await DcTravelClient.QueryGroupListTravelTarget(7, 5);
                    var selectWorld = await WorldSelectorWindows.OpenTravelWindow(false, true, false, areas, currentDcGroupName, currentWorld.InternalName.ToString());
                    var chara = new Character() { ContentId = contentId.ToString(), Name = currentCharacterName };
                    targetDcGroupName = selectWorld.Target.AreaName;
                    Log.Information($"正在传送:{currentWorld.Name}@{currentDcGroupName} -> {selectWorld.Target.GroupName}@{targetDcGroupName}");
                    //var waitTime = await DcTravelClient.QueryTravelQueueTime(selectWorld.Target.AreaId, selectWorld.Target.GroupId);
                    //if (waitTime > 0)
                    //{
                    //    // e.queueTime > 0 ? "(预计需" + parseInt(30 * (parseInt(e.queueTime / 30) + 1)) + "分钟内)" : "(无需等待)")
                    //    estimatedTime = (waitTime / 30 + 1) * 30;
                    //}
                    //Log.Info($"预计花费时间:{estimatedTime} 分钟");
                    //var costMsgBox = await MessageBoxWindow.Show(WindowSystem, title, $"预计时间:{estimatedTime} 分钟内", MessageBoxType.YesNo);
                    var costMsgBox = await MessageBoxWindow.Show(WindowSystem, title, $"是否进行跨域传送?", MessageBoxType.YesNo);
                    if (costMsgBox == MessageBoxResult.Yes)
                    {
                        await Framework.RunOnFrameworkThread(GameFunctions.ReturnToTitle);
                        orderId = await DcTravelClient.TravelOrder(selectWorld.Target, selectWorld.Source, chara);
                        Log.Information($"Get an order: {orderId}");
                    }
                    else
                    {
                        Log.Info($"取消咯");
                        return;
                    }
                }
                LobbyDKT.Open();
                await WaitingForOrder(orderId, estimatedTime);
                UIGlobals.PlaySoundEffect(67);
                GameFunctions.RequestVibrationWhenReady();
                await SelectDcAndLogin(targetDcGroupName);
            }
            catch (Exception ex)
            {
                await MessageBoxWindow.Show(WindowSystem, title, $"{title}失败:\n{ex}", showWebsite: true);
                Log.Error(ex.ToString());
            }
            finally
            {
                LobbyDKT.Close();
            }
        });
    }

    public async Task WaitingForOrder(string orderId, int estimatedTime)
    {
        OrderSatus status;
        while (true)
        {
            GameFunctions.ResetTitleMovieTimer();
            status = await DcTravelClient!.QueryOrderStatus(orderId);
            Log.Information($"Current status:{status.Status}");
            LobbyDKT.SetStatus(status.Status, estimatedTime);
            if (status.Status == 5)
            {
                return;
            }
            else if (status.Status == 2)
            {
                var confirmResult = await MessageBoxWindow.Show(WindowSystem, "传送确认", "请确认传送", MessageBoxType.OkCancel);
                await DcTravelClient.MigrationConfirmOrder(orderId, confirmResult == MessageBoxResult.Ok);
                if (confirmResult != MessageBoxResult.Ok)
                {
                    throw new Exception($"传送失败, 已取消");
                }
            }
            else if (status.Status < 0)
            {
                throw new Exception($"传送失败,{status.CheckMessage} {status.MigrationMessage}");
            }
            await Task.Delay(2000);
        }
    }

    private MigrationOrder GetTravelingOrder(ulong contentId)
    {
        var contentIdStr = contentId.ToString();
        var maxPageNum = 1;
        var currentPageNum = 1;
        while (true)
        {
            var orders = DcTravelClient!.QueryMigrationOrders(currentPageNum).Result;
            var order = orders.Orders.FirstOrDefault(x => x.ContentId == contentIdStr);
            if (order == null)
            {
                maxPageNum = orders.TotalPageNum;
                currentPageNum++;
                if (currentPageNum > maxPageNum)
                {
                    Log.Error($"Fail to find order for {contentId}");
                    throw new Exception("无法找到返回订单!");
                }
            }
            else
            {
                return order;
            }
        }
    }

    public async Task SelectDcAndLogin(string name)
    {
        var newTicket = await DcTravelClient!.RefreshGameSessionId();
        ChangeToSdoArea(name);
        GameFunctions.ChangeDevTestSid(newTicket);
        GameFunctions.LoginInGame();
    }

    public void ChangeToSdoArea(string groupName)
    {
        var targetArea = SdoAreas!.FirstOrDefault(x => x.AreaName == groupName);
        GameFunctions.ChangeGameServer(targetArea!.AreaLobby, targetArea!.AreaConfigUpload, targetArea!.AreaGm);
        GameFunctions.RefreshGameServer();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        ContextMenu.OnMenuOpened -= this.OnContextMenuOpened;
        this.TitleScreenButton?.Dispose();
        WorldSelectorWindows.Dispose();
        //MainWindow?.Dispose();
    }

    private void DrawUI() => WindowSystem.Draw();
}
