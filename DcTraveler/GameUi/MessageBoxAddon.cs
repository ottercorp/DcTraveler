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

public enum MessageBoxType
{
    /// <summary>
    /// An "OK" button only message box.
    /// </summary>
    Ok,

    /// <summary>
    /// A message box with "OK" and "Cancel" buttons.
    /// </summary>
    OkCancel,

    /// <summary>
    /// A message box with "Yes" and "Cancel" buttons.
    /// </summary>
    YesCancel,

    /// <summary>
    /// A message box with "Yes" and "No" buttons.
    /// </summary>
    YesNo,

    /// <summary>
    /// A message box with "Yes," "No," and "Cancel" buttons.
    /// </summary>
    YesNoCancel,
}

public enum MessageBoxResult
{
    /// <summary>
    /// No specific result; typically used when no choice is made.
    /// </summary>
    None,

    /// <summary>
    /// The "OK" button or choice in a message box.
    /// </summary>
    Ok,

    /// <summary>
    /// The "Cancel" button or choice in a message box.
    /// </summary>
    Cancel,

    /// <summary>
    /// The "Yes" button or choice in a message box.
    /// </summary>
    Yes,

    /// <summary>
    /// The "No" button or choice in a message box.
    /// </summary>
    No,
}

public unsafe class MessageBoxAddon : NativeAddon, IDisposable
{
    private TextNode? messageNode;
    private TextNode? websiteHintNode;
    private TextButtonNode? websiteButton1;
    private TextButtonNode? websiteButton2;
    private VerticalListNode? rootNode;

    private TaskCompletionSource<MessageBoxResult>? messageTaskCompletionSource;

    private MessageBoxType pendingType = MessageBoxType.Ok;
    private string pendingMessage = string.Empty;
    private bool pendingShowWebsite = false;

    private const float WindowWidth = 400f;
    private const float ButtonWidth = 100f;
    private const float ButtonHeight = 28f;

    protected override void OnSetup(AtkUnitBase* addon)
    {
        // 根据是否显示网站按钮动态设置窗口高度
        var windowHeight = pendingShowWebsite ? 200f : 150f;
        SetWindowSize(WindowWidth, windowHeight);

        rootNode = new VerticalListNode
        {
            Size = ContentSize,
            Position = ContentStartPosition,
            ItemSpacing = 8.0f,
            Anchor = VerticalListAnchor.Top,
            FitWidth = true,
        };

        // 计算消息区域高度
        float messageHeight = ContentSize.Y - ButtonHeight - 16f;
        if (pendingShowWebsite)
        {
            // 如果显示网站按钮，需要为提示文本和网站按钮预留空间
            messageHeight -= (10f + ButtonHeight); // 提示文本高度 + 间距 + 按钮高度 + 间距
        }

        // 添加消息文本
        messageNode = new TextNode
        {
            Width = ContentSize.X,
            Height = messageHeight,
            String = pendingMessage,
            AlignmentType = AlignmentType.Center,
            FontSize = 14,
            TextColor = ColorHelper.GetColor(8),
            TextOutlineColor = ColorHelper.GetColor(7),
            TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
        };
        rootNode.AddNode(messageNode);

        // 如果需要显示网站按钮，添加提示文本和按钮
        if (pendingShowWebsite)
        {
            websiteHintNode = new TextNode
            {
                Width = ContentSize.X,
                Height = 20f,
                String = "到官网查看或处理",
                AlignmentType = AlignmentType.Center,
                FontSize = 12,
                TextColor = ColorHelper.GetColor(8),
                TextOutlineColor = ColorHelper.GetColor(7),
            };
            rootNode.AddNode(new HorizontalLineNode { Height = 2.0f });
            rootNode.AddNode(websiteHintNode);

            var websiteButtonsNode = CreateWebsiteButtons(ContentSize.X);
            rootNode.AddNode(websiteButtonsNode);
        }
        else
        {
            // 只在不显示网站按钮时添加主按钮
            var buttonsNode = CreateButtonsForType(pendingType, ContentSize.X);
            rootNode.AddNode(buttonsNode);
        }

        AddNode(rootNode);
    }

    private HorizontalListNode CreateButtonsForType(MessageBoxType type, float contentWidth)
    {
        var buttons = new List<TextButtonNode>();

        switch (type)
        {
            case MessageBoxType.Ok:
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "确定",
                    OnClick = () => CloseWithResult(MessageBoxResult.Ok),
                });
                break;

            case MessageBoxType.OkCancel:
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "确定",
                    OnClick = () => CloseWithResult(MessageBoxResult.Ok),
                });
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "取消",
                    OnClick = () => CloseWithResult(MessageBoxResult.Cancel),
                });
                break;

            case MessageBoxType.YesCancel:
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "是",
                    OnClick = () => CloseWithResult(MessageBoxResult.Yes),
                });
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "取消",
                    OnClick = () => CloseWithResult(MessageBoxResult.Cancel),
                });
                break;

            case MessageBoxType.YesNo:
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "是",
                    OnClick = () => CloseWithResult(MessageBoxResult.Yes),
                });
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "否",
                    OnClick = () => CloseWithResult(MessageBoxResult.No),
                });
                break;

            case MessageBoxType.YesNoCancel:
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "是",
                    OnClick = () => CloseWithResult(MessageBoxResult.Yes),
                });
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "否",
                    OnClick = () => CloseWithResult(MessageBoxResult.No),
                });
                buttons.Add(new TextButtonNode
                {
                    Width = ButtonWidth,
                    String = "取消",
                    OnClick = () => CloseWithResult(MessageBoxResult.Cancel),
                });
                break;
        }

        // 计算按钮居中的间距
        var totalButtonWidth = (buttons.Count * ButtonWidth) + ((buttons.Count - 1) * 16f);
        var firstItemSpacing = (contentWidth - totalButtonWidth) / 2f;

        var buttonsNode = new HorizontalListNode
        {
            Height = ButtonHeight,
            FitHeight = true,
            ItemSpacing = 16f,
            FirstItemSpacing = firstItemSpacing,
            Alignment = HorizontalListAnchor.Left,
        };

        foreach (var button in buttons)
        {
            buttonsNode.AddNode(button);
        }

        return buttonsNode;
    }

    private HorizontalListNode CreateWebsiteButtons(float contentWidth)
    {
        var websiteButtons = new List<TextButtonNode>
        {
            new TextButtonNode
            {
                Width = 140f,
                String = "超域传送",
                OnClick = () => OpenUrl("https://ff14bjz.sdo.com/RegionKanTelepo?"),
            },
            new TextButtonNode
            {
                Width = 140f,
                String = "超域返回",
                OnClick = () => OpenUrl("https://ff14bjz.sdo.com/orderList"),
            },
        };

        // 计算按钮居中的间距
        var totalButtonWidth = (websiteButtons.Count * 140f) + ((websiteButtons.Count - 1) * 16f);
        var firstItemSpacing = (contentWidth - totalButtonWidth) / 2f;

        var websiteButtonsNode = new HorizontalListNode
        {
            Height = ButtonHeight,
            FitHeight = true,
            ItemSpacing = 16f,
            FirstItemSpacing = firstItemSpacing,
            Alignment = HorizontalListAnchor.Left,
        };

        foreach (var button in websiteButtons)
        {
            websiteButtonsNode.AddNode(button);
        }

        return websiteButtonsNode;
    }

    private void CloseWithResult(MessageBoxResult result)
    {
        messageTaskCompletionSource?.TrySetResult(result);
        Close();
    }

    protected override void OnHide(AtkUnitBase* addon)
    {
        // 窗口关闭时，如果任务还没完成，设置为 None
        messageTaskCompletionSource?.TrySetResult(MessageBoxResult.None);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        messageNode = null;
        websiteHintNode = null;
        websiteButton1 = null;
        websiteButton2 = null;
        rootNode = null;
    }

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// <summary>
    /// Shows a message box with the specified title, message, and type.
    /// </summary>
    /// <param name="title">The title of the message box.</param>
    /// <param name="message">The message text to be displayed.</param>
    /// <param name="type">The type of the message box.</param>
    /// <param name="showWebsite">Whether to show website buttons.</param>
    /// <returns>Task that completes with the user's choice.</returns>
    public static Task<MessageBoxResult> Show(string title, string message, MessageBoxType type = MessageBoxType.Ok, bool showWebsite = false)
    {
        var addon = new MessageBoxAddon
        {
            InternalName = $"DcTravelerMessageBox_{Guid.NewGuid():N}",
            Title = title,
        };

        addon.pendingMessage = message;
        addon.pendingType = type;
        addon.pendingShowWebsite = showWebsite;
        addon.messageTaskCompletionSource = new TaskCompletionSource<MessageBoxResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        addon.Open();

        return addon.messageTaskCompletionSource.Task;
    }

    public new void Dispose()
    {
        messageTaskCompletionSource?.TrySetResult(MessageBoxResult.None);
        Close();
    }
}
