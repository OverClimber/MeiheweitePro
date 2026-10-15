using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

// This editor fixture loads the candidate DLL. It never imports production
// scenes or writes decks. Paths are substituted by drag_geometry_gate.py.
public static class DeckDragUnityTests
{
    static Type geometry, manager, card;
    static int checks;
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        checks++;
    }
    static object Call(Type type, string name, params object[] args)
    {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    }
    static Vector3 Slot(int index, int count, float z)
    {
        object[] args = { index, count, z, Vector3.zero, Vector3.zero };
        Call(manager, "RowSlotPose", args);
        return (Vector3)args[3];
    }
    static Component NewCard(int type)
    {
        var go = new GameObject("fixture-card");
        var component = go.AddComponent(card);
        ((Behaviour)component).enabled = false;
        object data = card.GetField("cardData").GetValue(component);
        data.GetType().GetField("Type").SetValue(data, type);
        return component;
    }
    public static void Run()
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
                string path = Path.Combine(@"@MANAGED@", new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            var assembly = Assembly.LoadFrom(@"@DLL@");
            geometry = assembly.GetType("DeckDragGeometry", true);
            manager = assembly.GetType("DeckManager", true);
            card = assembly.GetType("MonoCardInDeckManager", true);
            foreach (int fps in new[] { 30, 60, 120, 240 })
            {
                float x = 0, factor = (float)Call(geometry, "FollowFactor", 1f / fps);
                for (int i = 0; i < fps / 10; i++) x += (1 - x) * factor;
                Check(Math.Abs(x - (1 - Math.Exp(-4))) < .00001, "follow independent of FPS " + fps);
            }
            foreach (float height in new[] { 480f, 720f, 1080f, 2160f, 4320f })
            {
                float slop = Mathf.Clamp(height / 180, 5, 12);
                Check(!(bool)Call(geometry, "Moved", Vector2.zero, new Vector2(slop - .1f, 0), height), "jitter stays click");
                Check((bool)Call(geometry, "Moved", Vector2.zero, new Vector2(slop + .1f, 0), height), "release frame motion starts drag");
            }
            Check(!(bool)Call(geometry, "Outside", new Vector3(16.3f, 0, 0)), "edge tolerance");
            Check((bool)Call(geometry, "Outside", new Vector3(16.5f, 0, 0)), "intentional removal");
            foreach (Vector3 direction in new[] { Vector3.right, Vector3.up })
            {
                object[] args = { new Ray(new Vector3(0, 23, -17.5f), direction), .6f, Vector3.zero };
                Check(!(bool)Call(geometry, "TryProject", args), "parallel/behind ray rejected");
            }
            var camera = new GameObject("fixture-camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 23, -17.5f);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographicSize = 18;
            foreach (bool ortho in new[] { false, true })
            foreach (Rect viewport in new[] { new Rect(0, 0, 1, 1), new Rect(.2f, 0, .65f, 1) })
            {
                camera.orthographic = ortho; camera.rect = viewport;
                for (int total = 1; total <= 80; total++)
                {
                    var counts = (int[])Call(assembly.GetType("UIHelper"), "get_decklieshuArray", total);
                    int index = 0;
                    for (int row = 0; row < 4; row++)
                    for (int col = 0; col < counts[row] && index < total; col++, index++)
                    {
                        Vector3 position = Slot(col, counts[row], 11.8f - row * 4);
                        Vector3 screen = camera.WorldToScreenPoint(position);
                        object[] project = { camera.ScreenPointToRay(screen), .6f, Vector3.zero };
                        Check((bool)Call(geometry, "TryProject", project), "valid camera projection");
                        Check((int)Call(manager, "NearestMainIndex", (Vector3)project[2], total) == index,
                            "rendered main slot maps back: " + total + "/" + index);
                    }
                }
            }
            for (int total = 1; total <= 40; total++)
            for (int i = 0; i < total; i++)
                Check((int)Call(manager, "NearestSingleRowIndex", Slot(i, total, -12), total) == i, "extra/side slot roundtrip");
            Check((int)Call(manager, "NearestMainIndex", new Vector3(12.5f, .6f, 7.8f), 12) == 11,
                "sparse row stays in chosen row instead of jumping upward");

            // Invoke the real mutation method with cards whose visual positions
            // intentionally disagree with their lists (an in-progress animation).
            var dm = FormatterServices.GetUninitializedObject(manager);
            var deckField = manager.GetField("deck");
            object deck = Activator.CreateInstance(deckField.FieldType);
            deckField.SetValue(dm, deck);
            var main = (IList)deck.GetType().GetProperty("IMain").GetValue(deck);
            var extra = (IList)deck.GetType().GetProperty("IExtra").GetValue(deck);
            var side = (IList)deck.GetType().GetProperty("ISide").GetValue(deck);
            var first = NewCard(1); var moved = NewCard(1); var fusion = NewCard(0x41);
            main.Add(first); main.Add(moved); extra.Add(fusion);
            first.transform.position = new Vector3(100, 0, -12); // must never be reclassified
            var snap = manager.GetMethod("SnapDraggedCardIntoDeck", BindingFlags.NonPublic | BindingFlags.Instance);
            snap.Invoke(dm, new object[] { moved, new Vector3(0, .6f, -12) });
            Check(main.Count == 1 && main[0] == first && side.Count == 1 && side[0] == moved && extra[0] == fusion,
                "only captured card changes bucket; unrelated moving card retained");
            snap.Invoke(dm, new object[] { fusion, new Vector3(0, .6f, -12) });
            snap.Invoke(dm, new object[] { fusion, new Vector3(0, .6f, 8) });
            Check(extra.Count == 1 && extra[0] == fusion && main.Count == 1 && side.Count == 1,
                "extra type routes back from side without duplicates or loss");
            // A click on an existing card must leave both order and dirty state
            // unchanged. Use the real release handler, not a copied equivalent.
            camera.orthographic = true;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            assembly.GetType("Program").GetField("camera_game_main").SetValue(null, camera);
            manager.GetField("cardInDragging").SetValue(dm, first);
            card.GetMethod("beginDrag").Invoke(first, new object[] { false });
            manager.GetMethod("ES_mouseUp").Invoke(dm, null);
            Check(main.Count == 1 && main[0] == first && side[0] == moved && extra[0] == fusion,
                "real click/release preserves deck membership");
            Check(manager.GetField("cardInDragging").GetValue(dm) == null, "release clears capture");
            // A new serialized field would break replacement DLL compatibility.
            foreach (var f in card.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                Check(!Attribute.IsDefined(f, typeof(SerializeField)), "gesture state is not serialized");
            File.WriteAllText(@"@RESULT@", "passed assertions=" + checks);
            Debug.Log("DECK_DRAG_PASS assertions=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
