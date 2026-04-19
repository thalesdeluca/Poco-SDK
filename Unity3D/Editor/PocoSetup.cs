#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Tools > Poco > Setup Current Scene
//
// One-click install of Poco into the currently-open scene:
//   1. Adds PocoManager to the Main Camera (or a "Poco" GameObject if none).
//   2. Adds PocoUIClickExtension to the same GameObject.
//   3. Wires PocoManager.pocoListenersBase -> the extension, so the
//      [PocoMethod] listeners (UIClick, UIDrag, UIScroll, ...) get registered
//      in PocoManager.Awake via PocoListenerUtils.SubscribePocoListeners.
//
// Shipped inside Unity3D/Editor/ so cloning the SDK gives you the menu item
// automatically. If you vendor only uguiWithTMPro into an existing project,
// copy Unity3D/Editor/PocoSetup.cs alongside Unity3D/*.cs under an Editor/
// folder inside your Assets.
public static class PocoSetup
{
    [MenuItem("Tools/Poco/Setup Current Scene")]
    public static void SetupCurrentScene()
    {
        var pocoType = FindTypeByName("PocoManager");
        if (pocoType == null)
        {
            EditorUtility.DisplayDialog("Poco Setup",
                "PocoManager type not found. Make sure Poco-SDK Unity3D/ is under your Assets/.",
                "OK");
            return;
        }

        var uiClickType = FindTypeByName("PocoUIClickExtension");

        var scene = EditorSceneManager.GetActiveScene();
        GameObject host = Camera.main != null ? Camera.main.gameObject : null;
        if (host == null) host = GameObject.Find("Poco") ?? new GameObject("Poco");

        bool dirty = false;

        if (host.GetComponent(pocoType) == null)
        {
            host.AddComponent(pocoType);
            dirty = true;
            Debug.Log($"[PocoSetup] PocoManager added to {host.name}");
        }

        Component uiClick = uiClickType != null ? host.GetComponent(uiClickType) : null;
        if (uiClickType != null && uiClick == null)
        {
            uiClick = host.AddComponent(uiClickType);
            dirty = true;
            Debug.Log($"[PocoSetup] PocoUIClickExtension added to {host.name}");
        }

        var pm = host.GetComponent(pocoType);
        if (pm != null && uiClick != null)
        {
            var so = new SerializedObject(pm);
            var prop = so.FindProperty("pocoListenersBase");
            if (prop != null && prop.objectReferenceValue != uiClick)
            {
                prop.objectReferenceValue = uiClick;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
                Debug.Log("[PocoSetup] PocoManager.pocoListenersBase wired");
            }
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PocoSetup] Done. Press Play — Poco listens on :5001.");
        }
        else
        {
            Debug.Log("[PocoSetup] Already set up, nothing to do.");
        }
    }

    static Type FindTypeByName(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }
}
#endif
