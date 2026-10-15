using System;

public static class TouchGestureTests
{
    static int checks;
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
    static TouchGestureState Begin(bool drag = false, bool right = true, bool horizontal = false)
    {
        var g = new TouchGestureState();
        g.Begin(10, 100, 100, 1, drag, right, 20, horizontal);
        return g;
    }
    static int Main()
    {
        try
        {
            foreach (float dpi in new[] { 0f, 80f, 160f, 240f, 320f, 480f, 640f, 1000f, float.NaN })
            foreach (float height in new[] { 320f, 480f, 720f, 1080f, 1440f })
            {
                float density = TouchGestureState.PixelDensity(dpi, height), slop = density*10;
                Check(density >= 1 && density <= 4, "bounded physical density");
                var g = Begin(); g.End(100+slop*.8f, 100, 2, slop, false);
                Check(g.LeftDown && g.LeftUp && !g.RightUp, "held jitter remains tap");
                g = Begin(); g.End(100+slop*2, 100, 1.02f, slop, false);
                Check(!g.LeftDown && !g.LeftUp && g.SuppressUi, "release-only swipe never clicks");
                g = Begin(true); g.End(100+slop*2, 100, 1.02f, slop, false);
                Check(g.LeftDown && g.LeftUp && g.DragStarted && !g.Held && g.Target == 10,
                    "fast drag keeps original card and starts even with down/up in one frame");
            }
            foreach (bool drag in new[] { false, true })
            {
                var g = Begin(drag); if (drag) g.Move(150, 100, 10);
                g.Frame(); g.End(150, 100, 2, 10, true);
                Check(!g.LeftUp && !g.RightUp && g.SuppressUi, "cancel never commits click/drop");
                Check(g.CancelDrag == drag, "cancel existing drag");
                g.Frame(); g.NoTouches(); Check(g.Current == TouchGestureState.Phase.Idle, "all lifted resets");
            }
            var state = Begin(true); state.Move(150, 100, 10); state.Frame(); state.NoTouches();
            Check(state.CancelDrag && !state.LeftUp, "missing End cancels instead of dropping");
            state = Begin(true); state.Move(150, 100, 10); state.Frame(); state.Cancel();
            Check(state.CancelDrag && !state.Held && state.SuppressUi, "second finger cancels drag");
            state.Frame(); state.End(150, 100, 2, 10, false);
            Check(!state.LeftUp && !state.RightUp, "remaining finger does not click");
            foreach (int next in new[] { 10, 11 })
            foreach (bool right in new[] { false, true })
            {
                state = Begin(false, right); state.End(100, 100, 1.1f, 10, false); state.Frame();
                state.Begin(next, 101, 100, 1.2f, false, right, 20);
                state.End(101, 100, 1.25f, 10, false);
                bool expected = next == 10 && right;
                Check(state.RightUp == expected && state.LeftUp != expected,
                    "double tap requires same target and explicit right-click support");
            }
            state = Begin(); state.End(100, 100, 1.1f, 10, false); state.Frame();
            state.Begin(10, 101, 100, 1.2f, true, true, 20); state.Move(150, 100, 10);
            state.End(150, 100, 1.3f, 10, false);
            Check(!state.RightUp && state.DragStarted, "second tap dragged is never right-click");
            state = Begin(true, true, true); state.Move(101, 140, 10);
            Check(!state.LeftDown && state.SuppressUi, "vertical search scroll never creates a card");
            state = Begin(true, true, true); state.Move(140, 101, 10);
            Check(state.LeftDown && state.Held, "horizontal search drag picks card");
            state = Begin(); state.Move(150, 100, 10); state.End(100, 100, 2, 10, false);
            Check(!state.LeftDown, "swipe out then back remains canceled");
            Console.WriteLine("PASS touch gesture assertions=" + checks);
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
