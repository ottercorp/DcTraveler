using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// https://github.com/HexaEngine/Hexa.NET.ImGui.Widgets/blob/master/Hexa.NET.ImGui.Widgets/MessageBox.cs
namespace DcTraveler.Windows
{
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

    internal class MessageBoxWindow : Window, IDisposable
    {
        /// <summary>
        /// Gets or sets the title of the message box.
        /// </summary>
        public string Title;

        /// <summary>
        /// Gets or sets the message text to be displayed in the message box.
        /// </summary>
        public string Message;

        /// <summary>
        /// Gets the type of the message box, which determines the available actions.
        /// </summary>
        public MessageBoxType Type;

        /// <summary>
        /// Gets or sets the result of the message box (e.g., user response).
        /// </summary>
        public MessageBoxResult Result;

        public bool ShowWebsite = false;

        /// <summary>
        /// Gets or sets optional user data associated with the message box.
        /// </summary>
        public object? Userdata;

        /// <summary>
        /// Gets or sets an optional callback action to be invoked when the message box is closed.
        /// </summary>
        public Action<MessageBoxWindow, object?>? Callback;
        private TaskCompletionSource<MessageBoxResult> messageTaskCompletionSource;

        public readonly WindowSystem WindowSystem;
        /// <summary>
        /// Initializes a new instance of the MessageBox struct with the provided parameters.
        /// </summary>
        /// <param name="title">The title of the message box.</param>
        /// <param name="message">The message text to be displayed.</param>
        /// <param name="type">The type of the message box.</param>
        /// <param name="userdata">Optional user data associated with the message box.</param>
        /// <param name="callback">Optional callback action to be invoked when the message box is closed.</param>
        public MessageBoxWindow(WindowSystem windowSystem, string title, string message, MessageBoxType type, object? userdata = null, Action<MessageBoxWindow, object?>? callback = null) : base(title, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.Popup | ImGuiWindowFlags.Modal | ImGuiWindowFlags.NoCollapse, true)
        {
            Title = title;
            Message = message;
            Type = type;
            Userdata = userdata;
            Callback = callback;
            WindowSystem = windowSystem;
            ShowCloseButton = false;
            AllowPinning = false;
            AllowClickthrough = false;
            messageTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>
        /// Shows a message box with the specified title, message, and type.
        /// </summary>
        /// <param name="title">The title of the message box.</param>
        /// <param name="message">The message text to be displayed.</param>
        /// <param name="type">The type of the message box.</param>
        /// <param name="parent">Optional parent element for centering.</param>
        /// <returns>The created message box instance.</returns>
        public static Task<MessageBoxResult> Show(WindowSystem WindowSystem, string title, string message, MessageBoxType type = MessageBoxType.Ok, bool showWebsite=false)
        {
            var guid = Guid.NewGuid();
            MessageBoxWindow box = new(WindowSystem, $"{title}##{guid}", message, type);
            box.ShowWebsite = showWebsite;
            box.IsOpen = true;
            WindowSystem.AddWindow(box);
            return box.messageTaskCompletionSource.Task;
        }

        /// <summary>
        /// Shows a message box with the specified title, message, user data, callback, and type.
        /// </summary>
        /// <param name="title">The title of the message box.</param>
        /// <param name="message">The message text to be displayed.</param>
        /// <param name="userdata">Optional user data associated with the message box.</param>
        /// <param name="callback">Optional callback action to be invoked when the message box is closed.</param>
        /// <param name="type">The type of the message box.</param>
        /// <returns>The created message box instance.</returns>
        public static Task<MessageBoxResult> Show(WindowSystem WindowSystem, string title, string message, object? userdata, Action<MessageBoxWindow, object?> callback, MessageBoxType type = MessageBoxType.Ok)
        {
            var guid = Guid.NewGuid();
            MessageBoxWindow box = new(WindowSystem, $"{title}##{guid}", message, type, userdata, callback);
            box.IsOpen = true;
            box.IsOpen = true;
            WindowSystem.AddWindow(box);
            return box.messageTaskCompletionSource.Task;
        }
        public override void PreDraw()
        {
            ImGui.OpenPopup(Title);
            Vector2 center = ImGui.GetIO().DisplaySize * 0.5f;
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new(0.5f));
        }

        private static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        //private bool IsOpen = true;
        public override void Draw()
        {
            ImGui.Text(Message);

            ImGui.Separator();

            switch (Type)
            {
                case MessageBoxType.Ok:
                    if (ImGui.Button("Ok"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.Ok;
                    }
                    break;

                case MessageBoxType.OkCancel:
                    if (ImGui.Button("Ok"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.Ok;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.Cancel;
                    }
                    break;

                case MessageBoxType.YesCancel:
                    if (ImGui.Button("Yes"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.Yes;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.Cancel;
                    }
                    break;

                case MessageBoxType.YesNo:
                    if (ImGui.Button("Yes"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.Yes;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("No"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.No;
                    }
                    break;

                case MessageBoxType.YesNoCancel:
                    if (ImGui.Button("Yes"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.Yes;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("No"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.No;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        IsOpen = false;
                        Result = MessageBoxResult.Cancel;
                    }
                    break;
            }
            if (ShowWebsite)
            {
                ImGui.Text("似乎出错了,请去官方页面收到处理。");
                if (ImGui.Button("打开[超域传送]"))
                {
                    OpenUrl("https://ff14bjz.sdo.com/RegionKanTelepo?");
                }
                ImGui.SameLine();
                if (ImGui.Button("打开[超域返回]"))
                {
                    OpenUrl("https://ff14bjz.sdo.com/orderList");
                }
            }
            if (!IsOpen)
            {
                Callback?.Invoke(this, Userdata);
                this.messageTaskCompletionSource.SetResult(Result);
                //Log.Information($"{Result}");
                this.Close();
            }
        }
        /// <summary>
        /// Draws the message box and handles user interactions.
        /// </summary>
        /// <returns>True if the message box is still shown; otherwise, false.</returns>

        private void Close()
        {
            this.WindowSystem.RemoveWindow(this);
        }

        public void Dispose()
        {
        }
    }
}
