using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.Graphics.Render.ModelRenderer;

namespace DcTraveler.Windows
{
    public class WorldSelectResult
    {
        public bool IsOk { get; set; }
        public Area Source { get; set; }
        public Area Target { get; set; }
    }

    public class SelectWorldResult
    {
        public Group Source { get; set; }
        public Group Target { get; set; }
    }
    internal class WorldSelectorWindows : Window, IDisposable
    {
        public WorldSelectorWindows() : base("超域传送", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize)
        {
        }

        private bool showSourceWorld = true;
        private bool showTargetWorld = true;
        private bool isBack = false;
        private int currentDcIndex = 0;
        private int currentWorldIndex = 0;
        private string[] dc = new string[0];
        private List<string[]> world = new();
        private int targetDcIndex = 0;
        private int targetWorldIndex = 0;
        private List<Area> areas = new();

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
            if (showSourceWorld)
            {
                ImGui.BeginTable("##TableCurrent", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoBordersInBodyUntilResize);
                ImGui.TableSetupColumn("当前大区", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("当前服务器", ImGuiTableColumnFlags.WidthFixed, 300);
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.ListBox("##CurrentDc", ref currentDcIndex, dc, dc.Length);
                ImGui.TableNextColumn();
                ImGui.ListBox("##CurrentServer", ref currentWorldIndex, world[currentDcIndex], world[currentDcIndex].Length);
                ImGui.EndTable();
            }

            if (showTargetWorld)
            {
                ImGui.BeginTable("##TableCurrent", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoBordersInBodyUntilResize);
                ImGui.TableSetupColumn("目标大区", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("目标服务器", ImGuiTableColumnFlags.WidthFixed, 300);
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.ListBox("##TargettDc", ref targetDcIndex, dc, dc.Length);
                ImGui.TableNextColumn();
                ImGui.ListBox("##TargetServer", ref targetWorldIndex, world[targetDcIndex], world[targetDcIndex].Length);
                ImGui.EndTable();
            }
            var sameDc = (currentDcIndex == targetDcIndex);
            if (sameDc)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button(isBack ? "返回" : "传送"))
            {
                this.selectWorldTaskCompletionSource?.SetResult(
                    new SelectWorldResult()
                    {
                        Source = areas[currentDcIndex].GroupList[currentWorldIndex],
                        Target = areas[targetDcIndex].GroupList[targetWorldIndex]
                    });
                this.IsOpen = false;
            }
            if (sameDc)
            {
                ImGui.EndDisabled();
            }
            ImGui.SameLine();
            if (ImGui.Button("取消"))
            {
                this.selectWorldTaskCompletionSource?.SetResult(null);
                this.IsOpen = false;
            }
        }
        private TaskCompletionSource<SelectWorldResult>? selectWorldTaskCompletionSource;
        public Task<SelectWorldResult> OpenTravelWindow(bool showSourceWorld, bool showTargetWorld, bool isBack, List<Area> areas, string? currentDcName = null, string? currentWorldCode = null, string? targetDcName = null, string? targetWorldCode = null)
        {
            this.selectWorldTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            this.areas = areas;
            this.showSourceWorld = showSourceWorld;
            this.showTargetWorld = showTargetWorld;
            this.isBack = isBack;
            this.currentDcIndex = 0;
            this.currentWorldIndex = 0;
            this.targetDcIndex = 0;
            this.targetWorldIndex = 0;
            this.dc = new string[areas.Count];
            for (int i = 0; i < areas.Count; i++)
            {
                this.dc[i] = areas[i].AreaName;
                this.world.Add(new string[areas[i].GroupList.Count]);
                if (currentDcName == areas[i].AreaName)
                    this.currentDcIndex = i;
                else if (targetDcName == areas[i].AreaName)
                    this.targetDcIndex = i;
                for (int j = 0; j < areas[i].GroupList.Count; j++)
                {
                    this.world[i][j] = areas[i].GroupList[j].GroupName;
                    if (currentDcName == areas[i].AreaName && areas[i].GroupList[j].GroupCode == currentWorldCode)
                        this.currentWorldIndex = j;
                    else if (targetDcName == areas[i].AreaName && areas[i].GroupList[j].GroupCode == targetWorldCode)
                        this.targetWorldIndex = j;
                }
            }
            this.IsOpen = true;
            return this.selectWorldTaskCompletionSource.Task;
        }

        public void Dispose()
        {
        }
    }
}
