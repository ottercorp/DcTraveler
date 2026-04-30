using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace DcTraveler.GameUi;

public class SelectWorldResult
{
    public Group? Source { get; set; }
    public Group? Target { get; set; }
}

public unsafe class WorldSelectorAddon : NativeAddon, IDisposable
{
    private static readonly string[] DcStates = { "通畅", "热门", "火爆?" };

    // 记住上次的选择（按模式分别存储）
    private static int LastSourceAreaIndex = 0;
    private static int LastSourceServerIndex = 0;
    private static int LastTargetAreaIndex = 0;
    private static int LastTargetServerIndex = 0;

    private List<Area> pendingAreas = new();
    private int pendingAreaIndex = 0;
    private int pendingServerIndex = 0;
    private bool pendingIsBack = false;
    private bool pendingIsSourceMode = false;

    private int currentAreaIndex = -1;
    private int currentServerIndex = -1;

    // UI 节点引用 - 每次 OnSetup 都会重新创建
    private TextNode? titleLabel;
    private VerticalListNode? areaListNode;
    private VerticalListNode? serverListNode;
    private TextButtonNode? confirmButton;

    private TaskCompletionSource<SelectWorldResult?>? selectWorldTaskCompletionSource;

    private const float ListWidth = 140f;
    private const float ListHeight = 250f;
    private const float ItemHeight = 24f;

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        // 清除旧的节点引用（窗口重新打开时）
        titleLabel = null;
        areaListNode = null;
        serverListNode = null;
        confirmButton = null;

        // 应用待处理的数据
        currentAreaIndex = pendingAreaIndex;
        currentServerIndex = pendingServerIndex;

        // 构建大区按钮列表
        var areaButtons = new List<NodeBase>();
        for (var i = 0; i < pendingAreas.Count; i++)
        {
            var area = pendingAreas[i];
            var index = i;
            var isSelected = i == currentAreaIndex;

            string displayName;
            if (!pendingIsSourceMode && area.State >= 0 && area.State < DcStates.Length)
            {
                displayName = $"{area.AreaName}  [{DcStates[area.State]}]";
            }
            else
            {
                displayName = area.AreaName;
            }

            areaButtons.Add(new ListButtonNode
            {
                String = displayName,
                Height = ItemHeight,
                Selected = isSelected,
                OnClick = () => OnAreaSelected(index),
            });
        }

        // 构建服务器按钮列表
        var serverButtons = new List<NodeBase>();
        if (currentAreaIndex >= 0 && currentAreaIndex < pendingAreas.Count)
        {
            var groupList = pendingAreas[currentAreaIndex].GroupList;
            for (var i = 0; i < groupList.Count; i++)
            {
                var group = groupList[i];
                var index = i;
                var isSelected = i == currentServerIndex;

                serverButtons.Add(new ListButtonNode
                {
                    String = group.GroupName,
                    Height = ItemHeight,
                    Selected = isSelected,
                    OnClick = () => OnServerSelected(index),
                });
            }
        }

        // 使用嵌套布局
        AddNode(new VerticalListNode
        {
            Size = ContentSize,
            Position = ContentStartPosition,
            FitWidth = true,
            ItemSpacing = 4.0f,
            Anchor = VerticalListAnchor.Top,
            InitialNodes =
            [
                // 标题
                titleLabel = new TextNode
                {
                    Height = 24.0f,
                    FontSize = 14,
                    String = pendingIsSourceMode ? "选择当前服务器" : "选择目标服务器",
                    AlignmentType = AlignmentType.Center,
                    TextColor = ColorHelper.GetColor(3),
                    TextOutlineColor = ColorHelper.GetColor(7),
                },
                // 列表区域（水平排列）
                new HorizontalListNode
                {
                    Height = ListHeight,
                    FitHeight = true,
                    ItemSpacing = 8.0f,
                    Alignment = HorizontalListAnchor.Left,
                    InitialNodes =
                    [
                        // 大区列表
                        new VerticalListNode
                        {
                            Width = ListWidth,
                            FitWidth = true,
                            ItemSpacing = 2.0f,
                            Anchor = VerticalListAnchor.Top,
                            InitialNodes =
                            [
                                new TextNode
                                {
                                    Height = 20.0f,
                                    FontSize = 12,
                                    String = "大区",
                                    AlignmentType = AlignmentType.Center,
                                    TextColor = ColorHelper.GetColor(2),
                                    TextOutlineColor = ColorHelper.GetColor(7),
                                },
                                new HorizontalLineNode{
                                    Height = 2.0f,
                                },
                                areaListNode = new VerticalListNode
                                {
                                    Height = ListHeight - 24.0f,
                                    FitWidth = true,
                                    ItemSpacing = 2.0f,
                                    Anchor = VerticalListAnchor.Top,
                                    ClipListContents = true,
                                    InitialNodes = areaButtons,
                                },
                            ],
                        },
                        // 服务器列表
                        new VerticalListNode
                        {
                            Width = ListWidth,
                            FitWidth = true,
                            ItemSpacing = 2.0f,
                            Anchor = VerticalListAnchor.Top,
                            InitialNodes =
                            [
                                new TextNode
                                {
                                    Height = 20.0f,
                                    FontSize = 12,
                                    String = "服务器",
                                    AlignmentType = AlignmentType.Center,
                                    TextColor = ColorHelper.GetColor(2),
                                    TextOutlineColor = ColorHelper.GetColor(7),
                                },
                                new HorizontalLineNode{
                                    Height = 2.0f,
                                },
                                serverListNode = new VerticalListNode
                                {
                                    Height = ListHeight - 24.0f,
                                    FitWidth = true,
                                    ItemSpacing = 2.0f,
                                    Anchor = VerticalListAnchor.Top,
                                    ClipListContents = true,
                                    InitialNodes = serverButtons,
                                },
                            ],
                        },
                    ],
                },
                // 按钮区域 - 使用居中对齐
                new HorizontalListNode
                {
                    Height = 28.0f,
                    FitHeight = true,
                    ItemSpacing = 16.0f,
                    FirstItemSpacing = 36.0f,
                    Alignment = HorizontalListAnchor.Left,
                    InitialNodes =
                    [
                        confirmButton = new TextButtonNode
                        {
                            Width = 100.0f,
                            String = pendingIsBack ? "返回" : "传送",
                            OnClick = OnConfirmClicked,
                        },
                        new TextButtonNode
                        {
                            Width = 100.0f,
                            String = "取消",
                            OnClick = OnCancelClicked,
                        },
                    ],
                },
            ],
        });
    }

    protected override void OnShow(AtkUnitBase* addon)
    {
        // OnSetup 已经填充了列表，这里不需要做额外的事情
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        // 窗口销毁时清除节点引用
        titleLabel = null;
        areaListNode = null;
        serverListNode = null;
        confirmButton = null;
    }

    private void OnAreaSelected(int index)
    {
        currentAreaIndex = index;
        currentServerIndex = 0;

        // 立即保存大区选择
        if (pendingIsSourceMode)
        {
            LastSourceAreaIndex = currentAreaIndex;
            LastSourceServerIndex = currentServerIndex;
        }
        else
        {
            LastTargetAreaIndex = currentAreaIndex;
            LastTargetServerIndex = currentServerIndex;
        }

        // 更新大区选中状态
        if (areaListNode != null)
        {
            var i = 0;
            foreach (var node in areaListNode.GetNodes<ListButtonNode>())
            {
                node.Selected = (i == currentAreaIndex);
                i++;
            }
        }

        // 重建服务器列表
        if (serverListNode != null)
        {
            serverListNode.Clear();

            if (currentAreaIndex >= 0 && currentAreaIndex < pendingAreas.Count)
            {
                var groupList = pendingAreas[currentAreaIndex].GroupList;
                for (var i = 0; i < groupList.Count; i++)
                {
                    var group = groupList[i];
                    var idx = i;
                    var isSelected = i == currentServerIndex;

                    serverListNode.AddNode(new ListButtonNode
                    {
                        String = group.GroupName,
                        Height = ItemHeight,
                        Selected = isSelected,
                        OnClick = () => OnServerSelected(idx),
                    });
                }
            }
        }
    }

    private void OnServerSelected(int index)
    {
        currentServerIndex = index;

        // 立即保存服务器选择
        if (pendingIsSourceMode)
        {
            LastSourceServerIndex = currentServerIndex;
        }
        else
        {
            LastTargetServerIndex = currentServerIndex;
        }

        if (serverListNode != null)
        {
            var i = 0;
            foreach (var node in serverListNode.GetNodes<ListButtonNode>())
            {
                node.Selected = (i == currentServerIndex);
                i++;
            }
        }
    }

    private void OnConfirmClicked()
    {
        Group? selectedGroup = null;
        if (currentAreaIndex >= 0 && currentAreaIndex < pendingAreas.Count)
        {
            if (currentServerIndex >= 0 && currentServerIndex < pendingAreas[currentAreaIndex].GroupList.Count)
            {
                selectedGroup = pendingAreas[currentAreaIndex].GroupList[currentServerIndex];
            }
        }

        var result = new SelectWorldResult();
        if (pendingIsSourceMode)
        {
            result.Source = selectedGroup;
        }
        else
        {
            result.Target = selectedGroup;
        }

        selectWorldTaskCompletionSource?.TrySetResult(result);
        Close();
    }

    private void OnCancelClicked()
    {
        selectWorldTaskCompletionSource?.TrySetResult(null);
        Close();
    }

    protected override void OnHide(AtkUnitBase* addon)
    {
        // 窗口关闭时，如果任务还没完成，设置为 null
        selectWorldTaskCompletionSource?.TrySetResult(null);
    }

    public Task<SelectWorldResult?> OpenTravelWindow(bool showSourceWorld, bool showTargetWorld, bool isBack, ref readonly List<Area> areas, Group? sourceGroup = null, Group? targetGroup = null)
    {
        // 创建新的 TaskCompletionSource
        selectWorldTaskCompletionSource = new TaskCompletionSource<SelectWorldResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // 保存待处理的参数
        pendingAreas = areas;
        pendingIsBack = isBack;
        pendingIsSourceMode = showSourceWorld && !showTargetWorld;

        // 初始化选择的索引
        var targetIndexGroup = pendingIsSourceMode ? sourceGroup : targetGroup;
        pendingAreaIndex = pendingAreas.FindIndex(x => x.AreaId == targetIndexGroup?.AreaId);
        if (pendingAreaIndex == -1)
        {
            // 如果没有找到目标组，使用上次的选择
            if (pendingIsSourceMode)
            {
                pendingAreaIndex = LastSourceAreaIndex;
                pendingServerIndex = LastSourceServerIndex;
            }
            else
            {
                pendingAreaIndex = LastTargetAreaIndex;
                pendingServerIndex = LastTargetServerIndex;
            }

            // 确保索引有效
            if (pendingAreaIndex < 0 || pendingAreaIndex >= pendingAreas.Count)
            {
                pendingAreaIndex = 0;
            }
            if (pendingAreaIndex >= 0 && pendingAreaIndex < pendingAreas.Count)
            {
                if (pendingServerIndex < 0 || pendingServerIndex >= pendingAreas[pendingAreaIndex].GroupList.Count)
                {
                    pendingServerIndex = 0;
                }
            }
            else
            {
                pendingServerIndex = 0;
            }
        }
        else
        {
            pendingServerIndex = pendingAreas[pendingAreaIndex].GroupList.FindIndex(x => x.GroupId == targetIndexGroup?.GroupId);
            pendingServerIndex = pendingServerIndex == -1 ? 0 : pendingServerIndex;
        }

        // 打开窗口
        Open();

        return selectWorldTaskCompletionSource.Task;
    }

    public new void Dispose()
    {
        selectWorldTaskCompletionSource?.TrySetResult(null);
        Close();
    }
}
