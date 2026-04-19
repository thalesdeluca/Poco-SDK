using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Custom Poco listeners: attach to the same GameObject as PocoManager and
// assign it to PocoManager.pocoListenersBase (PocoBootstrap does this).
// Methods marked with [PocoMethod(...)] are reachable via the `Invoke` RPC:
//   poco.agent.rpc.call("Invoke", listener="UIClick", data={"name": "BtnStart"})
public class PocoUIClickExtension : PocoListenersBase
{
    // Reliable UI click — dispatches PointerDown/Up/Click via EventSystem,
    // bypassing Poco's flaky screen-coord touch simulation for Canvas Buttons.
    [PocoMethod("UIClick")]
    public object UIClick(string name)
    {
        if (string.IsNullOrEmpty(name))
            return new Dictionary<string, object> { { "success", false }, { "error", "no target" } };

        GameObject go = ResolveGameObject(name);
        if (go == null)
            return new Dictionary<string, object> { { "success", false }, { "error", "not found: " + name } };

        if (EventSystem.current == null)
            return new Dictionary<string, object> { { "success", false }, { "error", "no EventSystem in scene" } };

        var cam = Camera.main;
        Vector2 screenPos = cam != null
            ? (Vector2)RectTransformUtility.WorldToScreenPoint(cam, go.transform.position)
            : new Vector2(Screen.width / 2f, Screen.height / 2f);

        var data = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = screenPos,
        };
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);

        return new Dictionary<string, object> { { "success", true }, { "target", go.name } };
    }

    static GameObject ResolveGameObject(string target)
    {
        if (int.TryParse(target, out int id))
        {
            var obj = Resources.InstanceIDToObject(id);
            if (obj is GameObject goById) return goById;
            if (obj is Component c) return c.gameObject;
        }
        return GameObject.Find(target);
    }
}
