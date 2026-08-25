using System;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DcTraveler.GameUi;

/// <summary>
/// Restores the Data-Center-Select button on the CN FFXIV title menu and routes
/// its click into the plugin's own <see cref="DcGroupSelectorAddon"/> window.
///
/// Three surgical interventions, no per-frame work, no injected nodes:
///
///   1. One-byte binary patch in CN AddonTitleMenu::UpdateButtonLabels.
///      CN emits  add edx, 68h  (104, then call InitializeTimeline);
///      intl emits add edx, 66h (102) at the same site. Flipping the single
///      immediate makes CN behave identically to intl, so the layout timeline
///      naturally drives the intl main-menu arrangement (DCSelect at its slot).
///
///   2. Sig-hook AddonTitleMenu::OnRefresh, clear bit 1 of argv[0].UInt before
///      forwarding, restore it after. CN-only code does
///        if (argv[0].UInt &amp; 2) m_btnDCSelect-&gt;SetEnabledState(false);
///      Stripping the bit makes that branch a no-op; restoring leaves the
///      caller's argv unchanged for any later readers.
///
///   3. Sig-hook AddonTitleMenu::ReceiveEvent. The 6 title buttons register
///      event type 25 with param index+1 (node IDs 4..9 => params 1..6), so the
///      DCSelect button (node ID 5) fires (type 25, param 2). We intercept that
///      one case, mark the AtkEvent handled (mirroring the native path
///      sub_14063E850: *(int*)(atkEvent + 0x28) |= 0x10000), open the plugin's
///      DC selector, and skip Original so the native CN DC flow never runs.
///      Every other event is forwarded untouched.
///
/// Signatures/addresses verified against CN client 2026.05.01.0000.0000:
///   UpdateButtonLabels patch site @ 0x141176604 (+6), OnRefresh @ 0x141175F60,
///   ReceiveEvent @ 0x141176240.
/// </summary>
internal unsafe sealed class TitleMenuFix : IDisposable
{
    private const uint TitleMenuButtonContainerNodeId = 3;
    private const uint DcSelectButtonNodeId            = 5;
    private const int  ComponentNodeType               = 1001;
    private const string DcSelectButtonLabel            = "大区";

    // Anchor on the surrounding bytes; flip the immediate at offset 6.
    //   0x141176604: F6 D8           neg al
    //   0x141176606: 1B D2           sbb edx, edx
    //   0x141176608: 83 C2 68        add edx, 68h         <- byte we flip
    //   0x14117660B: E8 ?? ?? ?? ??  call InitializeTimeline
    private const string PatchAnchor     = "F6 D8 1B D2 83 C2 68 E8 ?? ?? ?? ??";
    private const int    PatchByteOffset = 6;
    private const byte   PatchByteOrig   = 0x68; // 104
    private const byte   PatchByteFixed  = 0x66; // 102

    // Stable signature for CN AddonTitleMenu::OnRefresh @ 0x141175F60.
    private const string OnRefreshSignature =
        "48 89 5C 24 ?? 56 48 83 EC ?? F6 81 ?? ?? ?? ?? ?? 49 8B F0 48 8B D9 0F 84 ?? ?? ?? ?? " +
        "49 8B C8 48 89 7C 24 ?? E8 ?? ?? ?? ?? 8B F8 A8 ?? 74 ?? BA";

    // Stable signature for CN AddonTitleMenu::ReceiveEvent @ 0x141176240.
    private const string ReceiveEventSignature =
        "48 89 5C 24 ?? 57 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? " +
        "48 8B D9 49 8B F9 48 8B 4C 24";

    private const uint DisableDCSelectBit = 0x2u;
    private const int  AtkValueUIntOffset = 0x8;  // AtkValue { Type @0, ..., UInt @8 }

    // Title-menu buttons register event type 25; DCSelect (node ID 5) is the 2nd
    // button, so it fires with param 2. AtkEvent flags live at +0x28; 0x10000 is
    // the "handled" bit the native code sets after processing a button click.
    private const uint DcSelectEventType    = 25;
    private const int  DcSelectEventParam   = 2;
    private const int  AtkEventFlagsOffset  = 0x28;
    private const int  AtkEventHandledFlag  = 0x10000;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte OnRefreshDelegate(IntPtr addon, int valueCount, IntPtr values);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReceiveEventDelegate(IntPtr addon, uint eventType, int eventParam, IntPtr atkEvent, IntPtr atkEventData);

    private readonly Plugin plugin;

    private Hook<OnRefreshDelegate>? onRefreshHook;
    private Hook<ReceiveEventDelegate>? receiveEventHook;
    private IntPtr bytePatchAddr;
    private byte   bytePatchOriginalByte;
    private bool   bytePatchApplied;

    public TitleMenuFix(Plugin plugin)
    {
        this.plugin = plugin;
        ApplyBytePatch();
        InstallOnRefreshHook();
        InstallReceiveEventHook();
    }

    public void Dispose()
    {
        receiveEventHook?.Disable();
        receiveEventHook?.Dispose();
        onRefreshHook?.Disable();
        onRefreshHook?.Dispose();
        RevertBytePatch();
    }

    private void InstallOnRefreshHook()
    {
        try
        {
            onRefreshHook = Plugin.GameInteropProvider.HookFromSignature<OnRefreshDelegate>(
                OnRefreshSignature, OnRefreshDetour);
            onRefreshHook.Enable();
            Plugin.Log.Information(
                $"[TitleMenuFix] hooked OnRefresh @ 0x{(long)onRefreshHook.Address:X}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[TitleMenuFix] OnRefresh hook failed");
        }
    }

    private byte OnRefreshDetour(IntPtr addon, int valueCount, IntPtr values)
    {
        if (valueCount <= 0 || values == IntPtr.Zero)
        {
            var ret = onRefreshHook!.Original(addon, valueCount, values);
            ApplyDcSelectButtonIcon((AtkUnitBase*)addon);
            return ret;
        }

        var uintAddr = values + AtkValueUIntOffset;
        var original = (uint)Marshal.ReadInt32(uintAddr);
        Marshal.WriteInt32(uintAddr, (int)(original & ~DisableDCSelectBit));
        try
        {
            var ret = onRefreshHook!.Original(addon, valueCount, values);
            ApplyDcSelectButtonIcon((AtkUnitBase*)addon);
            return ret;
        }
        finally
        {
            Marshal.WriteInt32(uintAddr, (int)original);
        }
    }

    private static void ApplyDcSelectButtonIcon(AtkUnitBase* addon)
    {
        if (addon == null) return;

        var textNode = FindDcSelectButtonText(addon);
        if (textNode == null) return;

        var icon = ((char)Dalamud.Game.Text.SeIconChar.BoxedLetterD).ToString();

        // Do not read and re-encode NodeText here: it contains SeString control
        // payloads after the first update, which would accumulate on refresh.
        // The CN native label is fixed and remains the game's default "大区".
        var seString = new SeStringBuilder()
            .AddUiForeground(539)
            .Append(icon)
            .AddUiForegroundOff()
            .Append(" ")
            .Append(DcSelectButtonLabel)
            .Build();
        textNode->SetText(seString.Encode());
    }

    private static AtkTextNode* FindDcSelectButtonText(AtkUnitBase* addon)
    {
        var containerNode = addon->GetNodeById(TitleMenuButtonContainerNodeId);
        if (containerNode == null) return null;

        var currentNode = containerNode->ChildNode;
        while (currentNode != null)
        {
            if (currentNode->NodeId == DcSelectButtonNodeId &&
                currentNode->Type == unchecked((NodeType)ComponentNodeType))
            {
                var componentNode = (AtkComponentNode*)currentNode;
                if (componentNode->Component == null) return null;

                var button = (AtkComponentButton*)componentNode->Component;
                var uldManager = &button->AtkComponentBase.UldManager;
                for (uint i = 0; i < uldManager->NodeListCount; i++)
                {
                    var childNode = uldManager->NodeList[i];
                    if (childNode == null || childNode->Type != NodeType.Res || childNode->ChildNode == null)
                        continue;

                    if (childNode->ChildNode->Type == NodeType.Text)
                        return (AtkTextNode*)childNode->ChildNode;
                }

                return null;
            }

            currentNode = currentNode->PrevSiblingNode;
        }

        return null;
    }

    private void InstallReceiveEventHook()
    {
        try
        {
            receiveEventHook = Plugin.GameInteropProvider.HookFromSignature<ReceiveEventDelegate>(
                ReceiveEventSignature, ReceiveEventDetour);
            receiveEventHook.Enable();
            Plugin.Log.Information(
                $"[TitleMenuFix] hooked ReceiveEvent @ 0x{(long)receiveEventHook.Address:X}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[TitleMenuFix] ReceiveEvent hook failed");
        }
    }

    private void ReceiveEventDetour(IntPtr addon, uint eventType, int eventParam, IntPtr atkEvent, IntPtr atkEventData)
    {
        // DCSelect button clicked: open our own DC selector instead of the
        // native CN flow. Every other event falls through untouched.
        if (eventType == DcSelectEventType && eventParam == DcSelectEventParam)
        {
            try
            {
                if (atkEvent != IntPtr.Zero)
                {
                    var flagsAddr = atkEvent + AtkEventFlagsOffset;
                    Marshal.WriteInt32(flagsAddr, Marshal.ReadInt32(flagsAddr) | AtkEventHandledFlag);
                }

                plugin.OpenDcSelectWindow();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "[TitleMenuFix] failed to open DC selector");
            }

            return; // swallow: do not run the native DC-select action
        }

        receiveEventHook!.Original(addon, eventType, eventParam, atkEvent, atkEventData);
    }

    // Dalamud.SafeMemory.WriteBytes uses WriteProcessMemory under the hood,
    // which transparently handles PAGE_EXECUTE_READ pages, so no explicit
    // VirtualProtect / FlushInstructionCache needed.

    private void ApplyBytePatch()
    {
        try
        {
            var target  = Plugin.SigScanner.ScanText(PatchAnchor) + PatchByteOffset;
            var current = Marshal.ReadByte(target);
            if (current != PatchByteOrig)
            {
                Plugin.Log.Warning(
                    $"[TitleMenuFix] byte patch refused @ 0x{(long)target:X}: " +
                    $"current=0x{current:X2}, expected 0x{PatchByteOrig:X2}");
                return;
            }

            if (!SafeMemory.WriteBytes(target, new[] { PatchByteFixed }))
            {
                Plugin.Log.Error($"[TitleMenuFix] SafeMemory.WriteBytes failed @ 0x{(long)target:X}");
                return;
            }

            bytePatchAddr         = target;
            bytePatchOriginalByte = current;
            bytePatchApplied      = true;
            Plugin.Log.Information(
                $"[TitleMenuFix] byte patch applied @ 0x{(long)target:X}: " +
                $"0x{current:X2} -> 0x{PatchByteFixed:X2}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[TitleMenuFix] failed to apply byte patch");
        }
    }

    private void RevertBytePatch()
    {
        if (!bytePatchApplied) return;
        SafeMemory.WriteBytes(bytePatchAddr, new[] { bytePatchOriginalByte });
        bytePatchApplied = false;
    }
}
