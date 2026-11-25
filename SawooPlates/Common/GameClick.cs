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
    public static List<string> Describe(AtkUnitBase* addon, bool withText = false)
    {
        var texts = new List<string>();
        if (addon != null)
            DescribeIn(&addon->UldManager, texts, depth: 0, withText);
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

    private static void DescribeIn(AtkUldManager* uld, List<string> texts, int depth, bool withText)
    {
        if (depth > 6)
            return;

        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node == null)
                continue;

            if (withText && node->Type == NodeType.Text)
            {
                var text = ((AtkTextNode*)node)->NodeText.ToString().Trim();
                if (text.Length > 0)
                    texts.Add($"{new string('>', depth)}text {node->NodeId} '{text}'");
                continue;
            }

            if ((int)node->Type < 1000)
                continue;

            var component = ((AtkComponentNode*)node)->Component;
            if (component == null)
                continue;

            var type = component->GetComponentType();
            if (type == ComponentType.Button)
                texts.Add($"{new string('>', depth)}button {node->NodeId} '{ButtonText((AtkComponentButton*)component)}'{(node->IsVisible() ? "" : " (hidden)")}");
            else if (type is ComponentType.List or ComponentType.TreeList)
            {
                var list = (AtkComponentList*)component;
                for (var item = 0; item < list->GetItemCount(); item++)
                    texts.Add($"{new string('>', depth)}list {node->NodeId} item {item} '{ListItemText(list, item)}'");
                continue;
            }

            if (withText && type != ComponentType.Button)
                texts.Add($"{new string('>', depth)}component {node->NodeId} {type}");

            DescribeIn(&component->UldManager, texts, depth + 1, withText);
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

/// <summary>Drop-down lists and checkboxes in game windows, found by node id.</summary>
public static unsafe class GameControls
{
    public static AtkComponentDropDownList* DropDown(AtkUnitBase* addon, uint nodeId)
    {
        var component = Component(addon, nodeId);
        return component != null && component->GetComponentType() == ComponentType.DropDownList ? (AtkComponentDropDownList*)component : null;
    }

    public static AtkComponentCheckBox* CheckBox(AtkUnitBase* addon, uint nodeId)
    {
        var component = Component(addon, nodeId);
        return component != null && component->GetComponentType() == ComponentType.CheckBox ? (AtkComponentCheckBox*)component : null;
    }

    private static AtkComponentBase* Component(AtkUnitBase* addon, uint nodeId)
    {
        if (addon == null)
            return null;

        var node = addon->GetNodeById(nodeId);
        return node != null && (int)node->Type >= 1000 ? ((AtkComponentNode*)node)->Component : null;
    }

    /// <summary>Index of the entry with this text, or -1.</summary>
    public static int IndexOf(AtkComponentDropDownList* dropDown, string text)
    {
        var list = dropDown->List;
        if (list == null)
            return -1;

        for (var i = 0; i < list->GetItemCount(); i++)
        {
            if (string.Equals(list->GetItemLabel(i).ToString().Trim(), text, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    /// <summary>The text shown on the closed drop-down.</summary>
    public static string Shown(AtkComponentDropDownList* dropDown)
    {
        var box = dropDown->Checkbox;
        if (box == null || box->AtkComponentButton.ButtonTextNode == null)
            return string.Empty;
        return box->AtkComponentButton.ButtonTextNode->NodeText.ToString().Trim();
    }

    /// <summary>
    /// Picks an entry the way a click on it does. <paramref name="attempt"/> 0 clicks the entry in the list;
    /// 1 selects it and tells the window through the events registered on the drop-down.
    /// </summary>
    public static void Select(AtkUnitBase* addon, AtkComponentDropDownList* dropDown, int index, int attempt)
    {
        if (attempt == 0)
        {
            dropDown->List->DispatchItemEvent(index, AtkEventType.ListItemClick);
            return;
        }

        dropDown->SelectItem(index);

        var node = (AtkResNode*)dropDown->OwnerNode;
        var data = stackalloc AtkEventData[1];
        for (var evt = node->AtkEventManager.Event; evt != null; evt = evt->NextEvent)
        {
            var type = evt->State.EventType;
            if (type is not (AtkEventType.ListItemClick or AtkEventType.ListItemSelect or AtkEventType.ValueUpdate))
                continue;

            *data = default;
            data->ListItemData.SelectedIndex = index;
            addon->ReceiveEvent(type, (int)evt->Param, evt, data);
        }
    }

    /// <summary>Clicks a checkbox, like the mouse does.</summary>
    public static bool Toggle(AtkUnitBase* addon, AtkComponentCheckBox* box)
    {
        var node = (AtkResNode*)box->OwnerNode;
        var data = stackalloc AtkEventData[1];
        for (var evt = node->AtkEventManager.Event; evt != null; evt = evt->NextEvent)
        {
            if (evt->State.EventType != AtkEventType.ButtonClick)
                continue;

            *data = default;
            addon->ReceiveEvent(AtkEventType.ButtonClick, (int)evt->Param, evt, data);
            return true;
        }

        return false;
    }
}
