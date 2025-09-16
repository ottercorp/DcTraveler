using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DcTraveler.Windows
{
    internal class WaitingWindow : Window, IDisposable
    {
        private DateTime openTime = DateTime.Now;
        public int Status = 0;
        public WaitingWindow() : base("WaitingOrder", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)
        {
            //Position = ImGui.GetScrr
        }

        public override void PreDraw()
        {
            var viewport = ImGui.GetMainViewport();
            var center = viewport.GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            //Log.Information("Middle");
            base.PreDraw();
        }
        public override void Draw()
        {
            Plugin.Font.Push();
            ImGui.Text("正在跨域传送中....");
            ImGui.Text($"已等待时间:{DateTime.Now - openTime}");
            ImGui.Text("目前状态:");
            ImGui.SameLine();
            if (Status == 0 || Status == 1)
            {
                ImGui.Text("角色检查中");
            }
            else if (Status == 3 || Status == 4)
            {
                ImGui.Text("处理中");
            }
            Plugin.Font.Pop();
        }

        public void Open()
        {
            this.IsOpen = true;
            this.openTime = DateTime.Now;
        }
        public void Dispose()
        {
        }
    }
}
