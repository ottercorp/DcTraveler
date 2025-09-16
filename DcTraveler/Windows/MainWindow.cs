using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using System;
using System.Numerics;
using static DcTraveler.GameFunctions;

namespace DcTraveler.Windows;

public class MainWindow : Window, IDisposable
{
    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("My Amazing Window##With a hidden ID")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }
    private int unk0 =3;
    private int unk1 =0;
    private float unk2 = 100f;
    private int unk3=3;
    private int unk4=0;
    private float unk5 = 100f;
    private float durationTime = 1.0f;
    public unsafe override void Draw()
    {
        ImGui.InputInt("UNK0", ref unk0);
        ImGui.InputInt("UNK1", ref unk1);
        ImGui.InputFloat("UNK2", ref unk2);
        ImGui.InputInt("UNK3", ref unk3);
        ImGui.InputInt("UNK4", ref unk4);
        ImGui.InputFloat("UNK5", ref unk5);
        ImGui.InputFloat("DurationTime", ref durationTime);
        if (ImGui.Button("TEST"))
        {
            //var v = stackalloc Vibration[1];
            //v->unk0 = (uint)unk0;
            //v->unk1 = (uint)unk1;
            //v->unk2 = unk2;
            //v->unk3 = (uint)unk3;
            //v->unk4 = (uint)unk4;
            //v->unk5 = unk5;
            //v->DurationTime = durationTime;
            //GameFunctions.RequestVibration(v);
        }
        return;
    }
}
