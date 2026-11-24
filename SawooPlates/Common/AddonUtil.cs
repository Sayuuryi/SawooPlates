using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Common;

public static unsafe class AddonUtil
{
    public static T* GetAddon<T>(string name, int index = 1) where T : unmanaged
    {
        var raptureAtkModule = RaptureAtkModule.Instance();
        if (raptureAtkModule == null)
            return null;

        var addon = raptureAtkModule->RaptureAtkUnitManager.GetAddonByName(name, index);
        var ready = addon != null && addon->IsReady;
        return ready ? (T*)addon : null;
    }

    public static T* GetAddon<T>(ushort addonId) where T : unmanaged
    {
        var raptureAtkModule = RaptureAtkModule.Instance();
        if (raptureAtkModule == null)
            return null;

        var addon = raptureAtkModule->RaptureAtkUnitManager.GetAddonById(addonId);
        var ready = addon != null && addon->IsReady;
        return ready ? (T*)addon : null;
    }

    public static T* GetAddon<T>(AgentId agentId) where T : unmanaged
    {
        var agentModule = AgentModule.Instance();
        if (agentModule == null)
            return null;

        var agent = agentModule->GetAgentByInternalId(agentId);
        var active = agent != null && agent->IsAgentActive();
        return active ? GetAddon<T>((ushort)agent->GetAddonId()) : null;
    }

    public static bool TryGetAddon<T>(string name, out T* addon) where T : unmanaged
        => (addon = GetAddon<T>(name)) != null;

    public static bool TryGetAddon<T>(AgentId agentId, out T* addon) where T : unmanaged
        => (addon = GetAddon<T>(agentId)) != null;

    public static bool IsAddonOpen(string name)
        => TryGetAddon<AtkUnitBase>(name, out var addon) && addon->IsVisible;

    public static bool IsAddonOpen(AgentId agentId)
        => TryGetAddon<AtkUnitBase>(agentId, out var addon) && addon->IsVisible;
}
