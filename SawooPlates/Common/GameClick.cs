using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Common;

/// <summary>
/// Clicks buttons and list entries in game windows by their text, the same way a mouse click does:
/// it sends the event the game registered on that button to the window that owns it.
/// </summary>
public static unsafe class GameClick
{
    /// <summary>Clicks the first visible button or list entry whose text matches. Returns false when none matches.</summary>
    public static bool Click(AtkUnitBase* addon, Func<string, bool> matches)
    {
        if (addon == null || !addon->IsVisible)
            return false;

        return ClickIn(addon, &addon->UldManager, matches, depth: 0);
    }

    /// <summary>Texts of the visible buttons and list entries, for the log when nothing matched.</summary>
    public static List<string> Describe(AtkUnitBase* addon)
    {
        var texts = new List<string>();
        if (addon != null)
            DescribeIn(&addon->UldManager, texts, depth: 0);
        return texts;
    }

    private static bool ClickIn(AtkUnitBase* addon, AtkUldManager* uld, Func<string, bool> matches, int depth)
    {
        if (depth > 6)
            return false;

        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node == null || (int)node->Type < 1000 || !node->IsVisible())
                continue;

            var component = ((AtkComponentNode*)node)->Component;
            if (component == null)
                continue;

            switch (component->GetComponentType())
            {
                case ComponentType.Button:
                    if (matches(ButtonText((AtkComponentButton*)component)) && SendClick(addon, node, AtkEventType.ButtonClick))
                        return true;
                    break;

                case ComponentType.List:
                case ComponentType.TreeList:
                    var list = (AtkComponentList*)component;
                    for (var item = 0; item < list->GetItemCount(); item++)
                    {
                        if (matches(ListItemText(list, item)))
                        {
                            list->DispatchItemEvent(item, AtkEventType.ListItemClick);
                            return true;
                        }
                    }

                    continue; // list entries are handled; don't look inside the renderers
            }

            if (ClickIn(addon, &component->UldManager, matches, depth + 1))
                return true;
        }

        return false;
    }

    private static void DescribeIn(AtkUldManager* uld, List<string> texts, int depth)
    {
        if (depth > 6)
            return;

        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node == null || (int)node->Type < 1000)
                continue;

            var component = ((AtkComponentNode*)node)->Component;
            if (component == null)
                continue;

            var type = component->GetComponentType();
            if (type == ComponentType.Button)
                texts.Add($"button {node->NodeId} '{ButtonText((AtkComponentButton*)component)}'{(node->IsVisible() ? "" : " (hidden)")}");
            else if (type is ComponentType.List or ComponentType.TreeList)
            {
                var list = (AtkComponentList*)component;
                for (var item = 0; item < list->GetItemCount(); item++)
                    texts.Add($"list {node->NodeId} item {item} '{ListItemText(list, item)}'");
                continue;
            }

            DescribeIn(&component->UldManager, texts, depth + 1);
        }
    }

    private static string ButtonText(AtkComponentButton* button)
        => button->ButtonTextNode != null ? button->ButtonTextNode->NodeText.ToString().Trim() : string.Empty;

    private static string ListItemText(AtkComponentList* list, int index)
    {
        var label = list->GetItemLabel(index).ToString();
        if (!string.IsNullOrWhiteSpace(label))
            return label.Trim();

        var renderer = list->GetItemRenderer(index);
        return renderer != null ? ButtonText((AtkComponentButton*)renderer) : string.Empty;
    }

    private static bool SendClick(AtkUnitBase* addon, AtkResNode* node, AtkEventType type)
    {
        for (var evt = node->AtkEventManager.Event; evt != null; evt = evt->NextEvent)
        {
            if (evt->State.EventType != type)
                continue;

            var data = stackalloc AtkEventData[1];
            *data = default;
            addon->ReceiveEvent(type, (int)evt->Param, evt, data);
            return true;
        }

        return false;
    }
}
