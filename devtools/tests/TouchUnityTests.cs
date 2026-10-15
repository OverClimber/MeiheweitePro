using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Runs against the actual Android candidate, including its NGUI click dispatch.
public static class TouchUnityTests
{
    static int checks, clicks;
    static void Click(GameObject go) { clicks++; }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static object Call(Type type, string method, params object[] args)
    { return type.GetMethod(method, Static).Invoke(null, args); }
    static void Field(object o, string name, object value) { o.GetType().GetField(name).SetValue(o, value); }
    public static void Run()
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
                string p = Path.Combine(@"@MANAGED@", new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            var assembly = Assembly.LoadFrom(@"@DLL@");
            var ui = assembly.GetType("UICamera", true);
            var adapter = assembly.GetType("TouchInputAdapter", true);
            var cameraGo = new GameObject("touch-camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0,0,-100);
            camera.orthographic = true; camera.orthographicSize = Screen.height/2f;
            camera.cullingMask = 1 << 11;
            var events = cameraGo.AddComponent(ui);
            ui.GetField("eventType").SetValue(events, Enum.Parse(ui.GetNestedType("EventType"), "World_3D"));
            var buttonType = assembly.GetType("UIButton", true);
            var go = new GameObject("small-button"); go.layer = 11;
            var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(16,16,1);
            go.AddComponent(buttonType);
            Physics.SyncTransforms();
            Vector2 center = camera.WorldToScreenPoint(Vector3.zero);
            Check((bool)Call(adapter,"ResolveUi",center), "exact button hit");
            Check((bool)Call(adapter,"ResolveUi",center+new Vector2(14,0)), "small button near miss assisted");
            Check(!(bool)Call(adapter,"ResolveUi",center+new Vector2(90,0)), "distant miss never expands");
            collider.enabled = false;
            Check(!(bool)Call(adapter,"ResolveUi",center), "disabled button never assisted");
            collider.enabled = true;
            var second = new GameObject("adjacent-button"); second.layer=11;
            second.transform.position = new Vector3(32,0,0);
            second.AddComponent<BoxCollider>().size = new Vector3(16,16,1);
            second.AddComponent(buttonType);
            Physics.SyncTransforms();
            Check(!(bool)Call(adapter,"ResolveUi",center+new Vector2(16,0)), "ambiguous gap stays unselected");
            Check((bool)Call(adapter,"ResolveUi",center+new Vector2(32,0)), "adjacent exact hit wins");
            var overlay = new GameObject("modal-blocker"); overlay.layer=11;
            overlay.transform.position = new Vector3(0,0,-2);
            overlay.AddComponent<BoxCollider>().size = new Vector3(100,100,1);
            Physics.SyncTransforms();
            Check((bool)Call(adapter,"ResolveUi",center), "modal receives direct hit");
            Check((GameObject)adapter.GetField("target",Static).GetValue(null) == overlay,
                "assistance cannot click through modal");

            object gesture = adapter.GetField("gesture",Static).GetValue(null);
            var touchType = ui.GetNestedType("MouseOrTouch");
            var notification = ui.GetNestedType("ClickNotification");
            var callback = ui.GetField("onClick",Static);
            callback.SetValue(null, Delegate.CreateDelegate(callback.FieldType, typeof(TouchUnityTests).GetMethod("Click",Static)));
            foreach (bool suppress in new[] { true, false })
            {
                gesture.GetType().GetMethod(suppress ? "Cancel" : "NoTouches").Invoke(gesture,null);
                var touch = Activator.CreateInstance(touchType);
                Field(touch,"pressed",go); Field(touch,"current",go); Field(touch,"dragged",go);
                Field(touch,"clickNotification",Enum.Parse(notification,"Always"));
                ui.GetField("currentTouch",Static).SetValue(null,touch);
                clicks=0;
                ui.GetMethod("ProcessRelease",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events,new object[]{false,100f});
                Check(clicks == (suppress ? 0 : 1), "actual NGUI OnClick suppression="+suppress);
            }
            var stranded = ui.GetMethod("GetTouch",Static).Invoke(null,new object[]{17,true});
            Field(stranded,"pressed",go); Field(stranded,"current",go); Field(stranded,"dragged",go);
            Field(stranded,"clickNotification",Enum.Parse(notification,"Always"));
            clicks=0;
            ui.GetMethod("CancelMobileTouches",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events,null);
            Check(clicks==0, "missing touch never clicks during cleanup");
            Check(((System.Collections.IList)ui.GetField("activeTouches",Static).GetValue(null)).Count==0,
                "cancellation releases NGUI touch records");
            var panelGo = new GameObject("virtual-list"); panelGo.layer=11;
            var panel = panelGo.AddComponent(assembly.GetType("UIPanel",true));
            var bar = new GameObject("scrollbar").AddComponent(assembly.GetType("UIScrollBar",true));
            Func<string[],GameObject> producer = args => new GameObject("row");
            Action<GameObject,string[]> binder = (row,args) => {};
            var list = Activator.CreateInstance(assembly.GetType("VirtualScrollView",true),
                new object[]{panel,bar,producer,86f,binder});
            var scroll = panelGo.GetComponent(assembly.GetType("UIScrollView",true));
            Check((bool)scroll.GetType().GetField("can_be_draged").GetValue(scroll),
                "synced virtual list enables native mobile scrolling");
            var window = new GameObject("oversized-settings");
            var body = new GameObject("asymmetric-window-body");
            body.transform.SetParent(window.transform, false);
            body.transform.localPosition = new Vector3(120, -180, 0);
            var widget = body.AddComponent(assembly.GetType("UISprite", true));
            widget.GetType().GetProperty("width").SetValue(widget, 1000, null);
            widget.GetType().GetProperty("height").SetValue(widget, 1600, null);
            var layout = assembly.GetType("WindowServant2D", true);
            foreach (var viewport in new[] { new Rect(12, 16, 776, 448), new Rect(24, 8, 912, 464), new Rect(0, 0, 1920, 1080) })
            {
                window.transform.position = (Vector3)Call(layout, "ArrangeWindow", window, camera, viewport, true, Vector3.one);
                var corners = (Vector3[])widget.GetType().GetProperty("worldCorners").GetValue(widget, null);
                foreach (var corner in corners)
                {
                    Vector3 point = camera.WorldToScreenPoint(corner);
                    Check(point.x >= viewport.xMin - .1f && point.x <= viewport.xMax + .1f
                        && point.y >= viewport.yMin - .1f && point.y <= viewport.yMax + .1f,
                        "window fits viewport including offset content");
                }
                Vector3 scaleBefore = window.transform.localScale;
                window.transform.position = (Vector3)Call(layout, "ArrangeWindow", window, camera, viewport, true, Vector3.one);
                Check(Vector3.Distance(scaleBefore, window.transform.localScale) < .0001f, "layout never compounds scale");
                window.transform.position = (Vector3)Call(layout, "ArrangeWindow", window, camera, viewport, false, Vector3.one);
                corners = (Vector3[])widget.GetType().GetProperty("worldCorners").GetValue(widget, null);
                foreach (var corner in corners)
                    Check(camera.WorldToScreenPoint(corner).y >= Screen.height, "hidden window is entirely offscreen");
            }
            File.WriteAllText(@"@RESULT@", "passed assertions="+checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(@"@RESULT@", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
}
