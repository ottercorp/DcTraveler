using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node.Simple;
using Serilog;

namespace DcTraveler.GameUi;

public unsafe class DcGroupSelectorAddon : NativeAddon, IDisposable
{
    private const float WindowWidth = 600f;
    private const float WindowHeight = 320f;
    private const float ColumnSpacing = 8f;
    private const float RowHeight = 24f;

    private static Plugin? pendingPlugin;
    private static DcGroupSelectorAddon? CurrentInstance;
    private VerticalListNode? rootNode;
    private List<Area> areas = new();
    private readonly List<SimpleNineGridNode> overlayNodes = new();
    private readonly List<IconImageNode> bgImageNodes = new();
    private readonly Dictionary<IconImageNode, uint> originalIconIds = new();

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        SetWindowSize(WindowWidth, WindowHeight);

        // Center the window on screen
        var screenSize = new System.Numerics.Vector2(
            FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance()->Width,
            FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance()->Height
        );
        var centerPosition = new System.Numerics.Vector2(
            (screenSize.X / 2f) - (WindowWidth / 2f),
            (screenSize.Y / 2f) - (WindowHeight / 2f)
        );
        SetWindowPosition(centerPosition);

        if (pendingPlugin?.DcTravelClient?.CachedAreas is { Count: > 0 } cachedAreas)
        {
            areas = cachedAreas;
        }
        else if (pendingPlugin?.ServerStatusAreas is { Count: > 0 } serverStatusAreas)
        {
            areas = serverStatusAreas;
        }
        else
        {
            areas = Plugin.SdoAreas
                .Select(area => new Area
                {
                    AreaId = int.TryParse(area.Areaid, out var areaId) ? areaId : 0,
                    AreaName = area.AreaName,
                    GroupList = new List<Group>(),
                })
                .ToList();
        }

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

        // On the title screen: hide _TitleMenu so it never overlaps or gets
        // mis-clicked while the selector is open. Restored in OnHide/OnFinalize.
        SetTitleMenuVisible(false);

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
        var bgSize = Math.Min(columnWidth, ContentSize.Y); // Square size
        float currentX = 0;

        overlayNodes.Clear();
        bgImageNodes.Clear();
        originalIconIds.Clear();

        // 5% chance to show easter egg icons
        var useEasterEgg = Random.Shared.NextDouble() < 0.05;
        uint[] easterEggIcons = [234003u, 234742u];
        if (useEasterEgg)
        {
            Random.Shared.Shuffle(easterEggIcons);
            WindowNode?.SetTitle("河狸选择");
        }
        var easterEggIndex = 0;

        foreach (var area in areas)
        {
            // Background image
            uint iconId;
            if (useEasterEgg)
            {
                iconId = easterEggIcons[easterEggIndex % easterEggIcons.Length];
                easterEggIndex++;
            }
            else
            {
                iconId = area.AreaId switch
                {
                    1 => 234006u,
                    6 => 234001u,
                    7 => 234002u,
                    8 => 234022u,
                    _ => 234003u, // default
                };
            }
            var bgImage = new IconImageNode
            {
                IconId = iconId,
                Size = new System.Numerics.Vector2(bgSize, bgSize),
                Position = ContentStartPosition + new System.Numerics.Vector2(currentX + (columnWidth - bgSize) / 2, ContentSize.Y - bgSize),
                FitTexture = true,
                Alpha = 0.2f,
            };
            AddNode(bgImage);
            bgImageNodes.Add(bgImage);
            originalIconIds[bgImage] = iconId;

            // Gradient overlay (rotated)
            var overlay = new SimpleNineGridNode
            {
                TexturePath = "ui/uld/ListItemA.tex",
                TextureCoordinates = new System.Numerics.Vector2(0.0f, 0.0f),
                TextureSize = new System.Numerics.Vector2(64.0f, 22.0f),
                LeftOffset = 16,
                RightOffset = 16,
                TopOffset = 8,
                BottomOffset = 8,
                Size = new System.Numerics.Vector2(ContentSize.Y, columnWidth),
                Position = ContentStartPosition + new System.Numerics.Vector2(currentX, ContentSize.Y),
                MultiplyColor = new System.Numerics.Vector3(0, 0, 0),
                Alpha = 1f,
                RotationDegrees = -90f,
            };
            AddNode(overlay);
            overlayNodes.Add(overlay);

            // Content column
            Plugin.Log.Debug($"Creating column for area: {area.AreaName} {area.AreaId}");
            var columnNode = CreateAreaColumn(area, columnWidth, overlay, bgImage, useEasterEgg);
            columnsNode.AddNode(columnNode);

            currentX += columnWidth + ColumnSpacing;
        }

        rootNode.AddNode(columnsNode);
        AddNode(rootNode);
    }

    private VerticalListNode CreateAreaColumn(Area area, float width, SimpleNineGridNode overlay, IconImageNode bgImage, bool useEasterEgg)
    {
        var columnNode = new VerticalListNode
        {
            Width = width,
            Height = ContentSize.Y,
            ItemSpacing = 0f,
            Anchor = VerticalListAnchor.Top,
        };

        // Enable clickable cursor and register events on collision node
        columnNode.CollisionNode.ShowClickableCursor = true;
        columnNode.CollisionNode.AddEvent(AtkEventType.MouseClick, () => OnAreaClicked(area.AreaName));
        columnNode.CollisionNode.AddEvent(AtkEventType.MouseOver, () =>
        {
            overlay.MultiplyColor = new System.Numerics.Vector3(16, 16, 16); // Lighten on hover
            bgImage.Alpha = 0.4f; // Increase background alpha on hover
        });
        columnNode.CollisionNode.AddEvent(AtkEventType.MouseOut, () =>
        {
            overlay.MultiplyColor = new System.Numerics.Vector3(0, 0, 0); // Back to normal
            bgImage.Alpha = 0.2f; // Reset background alpha
        });

        // Area name as header
        var displayName = useEasterEgg && area.AreaName.Length > 0
            ? area.AreaName[..^1] + "狸"
            : area.AreaName;
        var areaHeader = new TextNode
        {
            Width = width,
            Height = 32f,
            String = displayName,
            AlignmentType = AlignmentType.Center,
            FontSize = 14,
            TextColor = ColorHelper.GetColor(2),
            TextOutlineColor = ColorHelper.GetColor(7),
        };
        columnNode.AddNode(areaHeader);
        columnNode.AddNode(new HorizontalLineNode { Height = 2.0f, Width = width, ScaleX = 0.8f, OriginX = width / 2f });

        if (area.GroupList.Count == 0)
        {
            var selectHint = new TextNode
            {
                Width = width,
                Height = RowHeight,
                String = "点击切换登录大区",
                AlignmentType = AlignmentType.Center,
                FontSize = 12,
                TextColor = ColorHelper.GetColor(8),
                TextOutlineColor = ColorHelper.GetColor(7),
            };
            columnNode.AddNode(selectHint);
        }
        else
        {
            foreach (var group in area.GroupList)
            {
                var serverText = new TextNode
                {
                    Width = width,
                    Height = RowHeight,
                    String = group.GroupName,
                    AlignmentType = AlignmentType.Center,
                    FontSize = 12,
                    TextColor = ColorHelper.GetColor(8),
                    TextOutlineColor = ColorHelper.GetColor(7),
                };
                columnNode.AddNode(serverText);
            }
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
        SetTitleMenuVisible(true);
        pendingPlugin = null;
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        SetTitleMenuVisible(true);
        rootNode?.Dispose();
    }

    /// <summary>
    /// Toggle the game's _TitleMenu addon visibility so it never coexists with
    /// this selector. Flips the addon root-node visibility only (no native hide
    /// callbacks/animation fired), which is fully reversible and also blocks the
    /// menu's buttons from being hit-tested while hidden.
    /// </summary>
    private static void SetTitleMenuVisible(bool visible)
    {
        var titleMenuPtr = Plugin.GameGui.GetAddonByName("_TitleMenu", 1).Address;
        if (titleMenuPtr == nint.Zero) return;

        var titleMenu = (AtkUnitBase*)titleMenuPtr;
        if (titleMenu->RootNode != null)
            titleMenu->RootNode->ToggleVisibility(visible);
    }

    public static Task Show(Plugin plugin)
    {
        // If window is already open, just return (window is already visible)
        if (CurrentInstance != null && CurrentInstance.IsOpen)
        {
            return Task.CompletedTask;
        }

        pendingPlugin = plugin;

        var addon = new DcGroupSelectorAddon
        {
            InternalName = $"DcTravelerDcGroupSelector_{Guid.NewGuid():N}",
            Title = "大区选择",
        };

        CurrentInstance = addon;
        addon.Open();

        return Task.CompletedTask;
    }

    public new void Dispose()
    {
        CurrentInstance = null;
        Close();
    }
}
