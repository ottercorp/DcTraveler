using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using Serilog;

namespace DcTraveler.GameUi;

public unsafe class DcGroupSelectorAddon : NativeAddon, IDisposable
{
    private const float WindowWidth = 600f;
    private const float WindowHeight = 320f;
    private const float ColumnSpacing = 8f;
    private const float RowHeight = 24f;

    private static Plugin? pendingPlugin;
    private VerticalListNode? rootNode;
    private List<Area> areas = new();

    protected override void OnSetup(AtkUnitBase* addon)
    {
        SetWindowSize(WindowWidth, WindowHeight);

        // Center the window on screen
        var screenSize = new System.Numerics.Vector2(
            FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance()->Width,
            FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance()->Height
        );
        var centerPosition = new System.Numerics.Vector2(
            screenSize.X / 8f,
            screenSize.Y / 8f
        );
        SetWindowPosition(centerPosition);

        areas = DcTravelClient.CachedAreas;

        rootNode = new VerticalListNode
        {
            Size = ContentSize,
            Position = ContentStartPosition,
            ItemSpacing = 8.0f,
            Anchor = VerticalListAnchor.Top,
            FitWidth = true,
        };

        // Check if on title screen
        if (Plugin.GameGui.GetAddonByName("_TitleMenu", 1) == 0)
        {
            var errorNode = new TextNode
            {
                Width = ContentSize.X,
                Height = ContentSize.Y,
                String = "必须在标题画面打开",
                AlignmentType = AlignmentType.Center,
                FontSize = 14,
                TextColor = ColorHelper.GetColor(3),
                TextOutlineColor = ColorHelper.GetColor(7),
            };
            rootNode.AddNode(errorNode);
            AddNode(rootNode);
            return;
        }

        // Check if areas loaded
        if (areas == null || areas.Count == 0)
        {
            var errorNode = new TextNode
            {
                Width = ContentSize.X,
                Height = ContentSize.Y,
                String = "服务器信息加载失败",
                AlignmentType = AlignmentType.Center,
                FontSize = 14,
                TextColor = ColorHelper.GetColor(3),
                TextOutlineColor = ColorHelper.GetColor(7),
            };
            rootNode.AddNode(errorNode);
            AddNode(rootNode);
            return;
        }

        // Create horizontal layout for area columns
        var columnsNode = new HorizontalListNode
        {
            Width = ContentSize.X,
            Height = ContentSize.Y,
            ItemSpacing = ColumnSpacing,
            Alignment = HorizontalListAnchor.Left,
        };

        float columnWidth = (ContentSize.X - (ColumnSpacing * (areas.Count - 1))) / areas.Count;

        foreach (var area in areas)
        {
            var columnNode = CreateAreaColumn(area, columnWidth);
            columnsNode.AddNode(columnNode);
        }

        rootNode.AddNode(columnsNode);
        AddNode(rootNode);
    }

    private VerticalListNode CreateAreaColumn(Area area, float width)
    {
        var columnNode = new VerticalListNode
        {
            Width = width,
            Height = ContentSize.Y,
            ItemSpacing = 0f,
            Anchor = VerticalListAnchor.Top,
        };

        // Area name as button (clickable)
        var areaButton = new TextButtonNode
        {
            Width = width,
            Height = 32f,
            String = area.AreaName,
            OnClick = () => OnAreaClicked(area.AreaName),
        };
        columnNode.AddNode(areaButton);

        // Server list as text (non-clickable)
        foreach (var group in area.GroupList)
        {
            var serverText = new TextNode
            {
                Width = width,
                Height = RowHeight,
                String = group.GroupName,
                AlignmentType = AlignmentType.Center,
                FontSize = 12,
                TextColor = ColorHelper.GetColor(3),
                TextOutlineColor = ColorHelper.GetColor(7),
            };
            columnNode.AddNode(serverText);
        }

        return columnNode;
    }

    private void OnAreaClicked(string areaName)
    {
        if (pendingPlugin == null) return;

        var plugin = pendingPlugin;

        Task.Run(() =>
        {
            try
            {
                plugin.SelectDcAndLogin(areaName).Wait();
            }
            catch (Exception ex)
            {
                MessageBoxAddon.Show("选择大区", $"大区切换失败:\n\n{ex.Message}", showWebsite: false).Wait();
                Log.Error(ex.ToString());
            }
        });

        Close();
    }

    protected override void OnHide(AtkUnitBase* addon)
    {
        pendingPlugin = null;
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        rootNode?.Dispose();
    }

    public static Task Show(Plugin plugin)
    {
        pendingPlugin = plugin;

        var addon = new DcGroupSelectorAddon
        {
            InternalName = $"DcTravelerDcGroupSelector_{Guid.NewGuid():N}",
            Title = "大区选择",
        };

        addon.Open();

        return Task.CompletedTask;
    }

    public new void Dispose()
    {
        Close();
    }
}
