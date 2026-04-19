using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Extra Poco RPCs that dispatch events via EventSystem.ExecuteEvents or set
// UI component values directly. Poco's default input path (screen-coord touch
// simulation) frequently misses Canvas UI because:
//   - Button.onClick requires matching PointerDown+Up on the same Graphic.
//   - IBeginDrag only fires once touch moves past EventSystem.pixelDragThreshold.
//   - IDropHandler only fires if pointerDrag is still set when Up fires.
//   - IScrollHandler never fires from synthesized touches at all.
// Routing through ExecuteEvents or setting component values bypasses all of that.
//
// Setup: attach to the same GameObject as PocoManager, then drag into
// PocoManager.pocoListenersBase (PocoBootstrap handles both).
public class PocoUIClickExtension : PocoListenersBase
{
    // ----- click -------------------------------------------------------------

    [PocoMethod("UIClick")]
    public object UIClick(string name)
    {
        var go = ResolveGameObject(name);
        if (go == null) return Err("not found: " + name);
        if (EventSystem.current == null) return Err("no EventSystem in scene");

        var data = NewPointerData(go);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
        return Ok(go);
    }

    // ----- drag & drop -------------------------------------------------------

    [PocoMethod("UIDrag")]
    public object UIDrag(string from_name, string to_name, int steps)
    {
        var src = ResolveGameObject(from_name);
        var dst = ResolveGameObject(to_name);
        if (src == null) return Err("source not found: " + from_name);
        if (dst == null) return Err("target not found: " + to_name);
        if (EventSystem.current == null) return Err("no EventSystem in scene");
        if (steps < 2) steps = 10;

        var cam = Camera.main;
        Vector2 p0 = ScreenPos(cam, src);
        Vector2 p1 = ScreenPos(cam, dst);

        var data = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = p0,
            pressPosition = p0,
            pointerPress = src,
            rawPointerPress = src,
            pointerDrag = src,
            useDragThreshold = false,
        };

        ExecuteEvents.Execute(src, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(src, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(src, data, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(src, data, ExecuteEvents.beginDragHandler);

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 pos = Vector2.Lerp(p0, p1, t);
            data.delta = pos - data.position;
            data.position = pos;
            ExecuteEvents.Execute(src, data, ExecuteEvents.dragHandler);
        }

        data.position = p1;
        ExecuteEvents.Execute(src, data, ExecuteEvents.endDragHandler);
        ExecuteEvents.ExecuteHierarchy(dst, data, ExecuteEvents.dropHandler);
        ExecuteEvents.Execute(src, data, ExecuteEvents.pointerUpHandler);

        return new Dictionary<string, object>
        {
            { "success", true },
            { "from", src.name },
            { "to", dst.name },
            { "steps", steps },
        };
    }

    // ----- scroll ------------------------------------------------------------

    [PocoMethod("UIScroll")]
    public object UIScroll(string name, float dx, float dy)
    {
        var go = ResolveGameObject(name);
        if (go == null) return Err("not found: " + name);
        if (EventSystem.current == null) return Err("no EventSystem in scene");

        var data = NewPointerData(go);
        data.scrollDelta = new Vector2(dx, dy);
        ExecuteEvents.ExecuteHierarchy(go, data, ExecuteEvents.scrollHandler);
        return Ok(go);
    }

    // ----- hover (tooltips, highlights) --------------------------------------

    [PocoMethod("UIHover")]
    public object UIHover(string name, bool enter)
    {
        var go = ResolveGameObject(name);
        if (go == null) return Err("not found: " + name);
        if (EventSystem.current == null) return Err("no EventSystem in scene");

        var data = NewPointerData(go);
        var handler = enter ? ExecuteEvents.pointerEnterHandler : ExecuteEvents.pointerExitHandler;
        ExecuteEvents.Execute(go, data, handler);
        return Ok(go);
    }

    // ----- select + submit (keyboard-style confirm) --------------------------

    [PocoMethod("UISelect")]
    public object UISelect(string name)
    {
        var go = ResolveGameObject(name);
        if (go == null) return Err("not found: " + name);
        if (EventSystem.current == null) return Err("no EventSystem in scene");
        EventSystem.current.SetSelectedGameObject(go);
        return Ok(go);
    }

    [PocoMethod("UISubmit")]
    public object UISubmit(string name)
    {
        var go = ResolveGameObject(name);
        if (go == null) return Err("not found: " + name);
        if (EventSystem.current == null) return Err("no EventSystem in scene");
        EventSystem.current.SetSelectedGameObject(go);
        var data = new BaseEventData(EventSystem.current);
        ExecuteEvents.Execute(go, data, ExecuteEvents.submitHandler);
        return Ok(go);
    }

    // ----- direct value setters (more reliable than simulating drags) --------

    [PocoMethod("UISetSlider")]
    public object UISetSlider(string name, float value)
    {
        var go = ResolveGameObject(name);
        if (go == null) return Err("not found: " + name);
        var slider = go.GetComponent<Slider>();
        if (slider == null) return Err("no Slider component on " + name);
        slider.value = value;
        return Ok(go);
    }

    [PocoMethod("UISetToggle")]
    public object UISetToggle(string name, bool isOn)
    {
        var go = ResolveGameObject(name);
        if (go == null) return Err("not found: " + name);
        var toggle = go.GetComponent<Toggle>();
        if (toggle == null) return Err("no Toggle component on " + name);
        toggle.isOn = isOn;
        return Ok(go);
    }

    [PocoMethod("UISetDropdown")]
    public object UISetDropdown(string name, int index)
    {
        var go = ResolveGameObject(name);
        if (go == null) return Err("not found: " + name);
        var dd = go.GetComponent<Dropdown>();
        if (dd == null)
        {
            var tmp = go.GetComponent("TMPro.TMP_Dropdown");
            if (tmp == null) return Err("no Dropdown / TMP_Dropdown on " + name);
            var prop = tmp.GetType().GetProperty("value");
            if (prop == null) return Err("TMP_Dropdown.value not accessible");
            prop.SetValue(tmp, index);
            return Ok(go);
        }
        dd.value = index;
        return Ok(go);
    }

    // ----- helpers -----------------------------------------------------------

    static PointerEventData NewPointerData(GameObject go)
    {
        return new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = ScreenPos(Camera.main, go),
        };
    }

    static Vector2 ScreenPos(Camera cam, GameObject go)
    {
        if (cam == null) return new Vector2(Screen.width / 2f, Screen.height / 2f);
        return (Vector2)RectTransformUtility.WorldToScreenPoint(cam, go.transform.position);
    }

    static GameObject ResolveGameObject(string target)
    {
        if (string.IsNullOrEmpty(target)) return null;
        if (int.TryParse(target, out int id))
        {
            var obj = Resources.InstanceIDToObject(id);
            if (obj is GameObject goById) return goById;
            if (obj is Component c) return c.gameObject;
        }
        return GameObject.Find(target);
    }

    static Dictionary<string, object> Ok(GameObject go) =>
        new Dictionary<string, object> { { "success", true }, { "target", go.name } };

    static Dictionary<string, object> Err(string err) =>
        new Dictionary<string, object> { { "success", false }, { "error", err } };
}
