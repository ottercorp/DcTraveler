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
using Dalamud.Interface.Utility.Raii;

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
        public Group? Source { get; set; }
        public Group? Target { get; set; }
    }
    internal class WorldSelectorWindows : Window, IDisposable
    {
        public WorldSelectorWindows() : base("超域传送", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize)
        {
        }

        private bool showSourceWorld = true;
        private bool showTargetWorld = true;
        private bool isBack = false;
        //private int currentDcIndex = 0;
        //private int currentWorldIndex = 0;
        //private int targetDcIndex = 0;
        //private int targetWorldIndex = 0;
        private List<Area> areas = new();
        private int sourceAreaIndex = -1;
        private int sourceServerIndex = -1;
        private int targetAreaIndex = -1;
        private int targetServerIndex = -1;
        private static readonly string[] DcStates = { "通畅", "热门", "火爆?" };
        private static readonly Vector4[] DcStatesColor = { new Vector4(0, 255, 0, 255), new Vector4(255, 255, 0, 255), new Vector4(255, 0, 0, 255) };
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
            var dcColumnWidth = ImGui.CalcTextSize("猫小胖 (火爆)").Length() * 1.2f;
            var serverColumnWidth = ImGui.CalcTextSize("海猫茶屋").Length() * 1.2f;
            var height = ImGui.GetTextLineHeightWithSpacing() * 8 * 1.2f;

            if (showSourceWorld)
            {
                using var table = ImRaii.Table("##TableSource", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoBordersInBodyUntilResize);

                ImGui.TableSetupColumn("当前大区", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("当前服务器", ImGuiTableColumnFlags.WidthFixed, 300);
                ImGui.TableHeadersRow();
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                using (_ = ImRaii.ListBox("##TargetDc", new Vector2(dcColumnWidth, height)))
                {
                    for (var i = 0; i < this.areas.Count; i++)
                    {
                        if (ImGui.Selectable(this.areas[i].AreaName, i == this.sourceAreaIndex))
                        {
                            this.sourceAreaIndex = i;
                        }
                    }
                }
                ImGui.TableNextColumn();
                using (_ = ImRaii.ListBox("##TargetServer", new Vector2(serverColumnWidth, height)))
                {
                    for (var i = 0; i < this.areas[this.sourceAreaIndex].GroupList.Count; i++)
                    {
                        if (ImGui.Selectable(this.areas[this.sourceAreaIndex].GroupList[i].GroupName, i == this.sourceServerIndex))
                        {
                            this.sourceServerIndex = i;
                        }
                    }
                }
            }

            if (showTargetWorld)
            {
                using var table = ImRaii.Table("##TableTarget", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoBordersInBodyUntilResize);

                ImGui.TableSetupColumn("目标大区", ImGuiTableColumnFlags.WidthFixed, dcColumnWidth);
                ImGui.TableSetupColumn("目标服务器", ImGuiTableColumnFlags.WidthFixed, serverColumnWidth);
                ImGui.TableHeadersRow();
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                using (_ = ImRaii.ListBox("##TargetDc", new Vector2(dcColumnWidth, height)))
                {
                    for (var i = 0; i < this.areas.Count; i++)
                    {
                        //using var _ = ImRaii.Disabled(this.areas[i].State == 2);
                        using var color = ImRaii.PushColor(ImGuiCol.Text, DcStatesColor[this.areas[i].State]);
                        if (ImGui.Selectable($"{this.areas[i].AreaName} ({DcStates[this.areas[i].State]})", i == this.targetAreaIndex))
                        {
                            this.targetAreaIndex = i;
                        }
                    }
                }
                ImGui.TableNextColumn();
                using (_ = ImRaii.ListBox("##TargetServer", new Vector2(serverColumnWidth, height)))
                {
                    for (var i = 0; i < this.areas[this.targetAreaIndex].GroupList.Count; i++)
                    {
                        //using var _ = ImRaii.Disabled(this.areas[this.targetAreaIndex].State == 2);
                        //using var color = ImRaii.PushColor(ImGuiCol.Text, DcStatesColor[this.areas[this.targetAreaIndex].State]);
                        if (ImGui.Selectable(this.areas[this.targetAreaIndex].GroupList[i].GroupName, i == this.targetServerIndex))
                        {
                            this.targetServerIndex = i;
                        }
                    }
                }
            }
            //using (ImRaii.Disabled(this.areas[this.targetAreaIndex].State == 2))
            //{
            if (ImGui.Button(isBack ? "返回" : "传送"))
            {
                Group? currentGroup = null;
                if (this.sourceAreaIndex >= 0 && this.sourceAreaIndex <  this.areas.Count)
                {
                    if (this.sourceServerIndex >= 0 && this.sourceServerIndex < this.areas[this.sourceAreaIndex].GroupList.Count)
                    {
                        currentGroup = this.areas[this.sourceAreaIndex].GroupList[this.sourceServerIndex];
                    }
                }
                Group? targetGroup = null;
                if (this.targetAreaIndex >= 0 && this.targetAreaIndex < this.areas.Count)
                {
                    if (this.targetServerIndex >= 0 && this.targetServerIndex < this.areas[this.targetAreaIndex].GroupList.Count)
                    {
                        targetGroup = this.areas[this.targetAreaIndex].GroupList[this.targetServerIndex];
                    }
                }
                this.selectWorldTaskCompletionSource?.SetResult(
                    new SelectWorldResult()
                    {
                        Source = currentGroup,
                        Target = targetGroup
                    });
                this.IsOpen = false;
            }
            //}

            ImGui.SameLine();
            if (ImGui.Button("取消"))
            {
                this.selectWorldTaskCompletionSource?.SetResult(null);
                this.IsOpen = false;
            }
        }

        private TaskCompletionSource<SelectWorldResult>? selectWorldTaskCompletionSource;
        public Task<SelectWorldResult> OpenTravelWindow(bool showSourceWorld, bool showTargetWorld, bool isBack, ref readonly List<Area> areas, Group? sourceGroup = null, Group? targetGroup = null)
        {
            this.selectWorldTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            this.areas = areas;
            this.showSourceWorld = showSourceWorld;
            this.showTargetWorld = showTargetWorld;
            this.isBack = isBack;

            this.sourceAreaIndex = this.areas.FindIndex(x => x.AreaId == sourceGroup?.AreaId);
            if (this.sourceAreaIndex == -1)
            {
                this.sourceAreaIndex = 0;
                this.sourceServerIndex = 0;
            }
            else
            {
                this.sourceServerIndex = this.areas[this.sourceAreaIndex].GroupList.FindIndex(x => x.GroupId == sourceGroup?.GroupId);
                this.sourceServerIndex = this.sourceServerIndex == -1 ? 0 : this.sourceServerIndex;
            }

            this.targetAreaIndex = this.areas.FindIndex(x => x.AreaId == targetGroup?.AreaId);
            if (this.targetAreaIndex == -1)
            {
                //for (this.targetAreaIndex = 0; this.targetAreaIndex < areas.Count; this.targetAreaIndex++)
                //{
                //    var area = areas[this.targetAreaIndex];
                //    if (area.State != 2)
                //    {
                //        break;
                //    }
                //}
                this.targetAreaIndex = 0;
                this.targetServerIndex = 0;
            }
            else
            {
                this.targetServerIndex = this.areas[this.targetAreaIndex].GroupList.FindIndex(x => x.GroupId == targetGroup?.GroupId);
                this.targetServerIndex = this.targetServerIndex == -1 ? 0 : this.targetServerIndex;
            }
            this.IsOpen = true;
            return this.selectWorldTaskCompletionSource.Task;
        }

        public void Dispose()
        {
        }
    }
}
