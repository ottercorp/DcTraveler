using Dalamud.Interface.Utility.Table;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace DcTraveler.Windows
{
    internal class DcGroupSelctorWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        public DcGroupSelctorWindow(Plugin plugin) : base("大区选择", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)
        {
            this.plugin = plugin;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(375, 220),
                MaximumSize = new Vector2(600, float.MaxValue)
            };
        }

        private void DrawDcGroup(Area area, float width)
        {
            ImGui.BeginChild(area.AreaName, new Vector2(width, 0), false);
            var tableStartPos = ImGui.GetCursorScreenPos();
            if (ImGui.BeginTable($"{area.AreaName} Content", 1, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn(area.AreaName);
                ImGui.TableHeadersRow();

                for (int i = 0; i < area.GroupList.Count; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(area.GroupList[i].GroupName);
                }
                ImGui.EndTable();
            }
            var a = ImGui.GetItemRectMax();
            var tableEndPos = ImGui.GetCursorScreenPos();
            var tableSize = tableEndPos - tableStartPos + new Vector2(width, 0);
            ImGui.SetCursorScreenPos(tableStartPos);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)); // 背景透明
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.15f, 0.6f, 1f, 0.30f)); // 悬停透明
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.35f, 0.8f, 0.50f)); // 激活透明

            if (ImGui.Button($"##{area.AreaName} Click", tableSize))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await plugin.SelectDcAndLogin(area.AreaName);
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxWindow.Show(plugin.WindowSystem, "选择大区", $"大区切换失败:\n{ex}", showWebsite: false);
                        Log.Error(ex.ToString());
                    }
                });
                this.IsOpen = false;
            }
            ImGui.PopStyleColor(3);
            ImGui.EndChild();
        }

        public override void Draw()
        {
            if (Plugin.SdoAreas == null || Plugin.SdoAreas.Count() == 0)
            {
                ImGui.Text("服务器信息加载失败");
                return;
            }
            if (Plugin.GameGui.GetAddonByName("_TitleMenu", 1) == 0)
            {
                ImGui.Text("必须在标题画面打开");
                return;
            }
            float tableWidth = ImGui.GetContentRegionAvail().X / DcTravelClient.CachedAreas.Count() - 10;
            foreach (var dc in DcTravelClient.CachedAreas)
            {
                this.DrawDcGroup(dc, tableWidth);
                ImGui.SameLine();
            }
        }
        public void Open()
        {
            this.IsOpen = true;
        }

        public void Dispose()
        {
        }
    }
}
