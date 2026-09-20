// ---------------------------------------------------------------------------
// LegacyHandles.cs
//
// Unity removed the old gizmo cap helpers (Handles.SphereCap, Handles.ArrowCap,
// Handles.DotCap, Handles.CircleCap) in favour of the *HandleCap family, which
// additionally takes the event type it should draw for. Several inherited editor
// gizmo scripts still call the old 4-argument form, so this forwarder maps it
// onto the modern API.
//
// Note: only the *direct call* form is redirected here. Calls that pass the cap
// helper as a delegate
//     Handles.FreeMoveHandle(pos, rot, size, Vector3.zero, Handles.SphereCap)
// were rewritten to the modern Handles.SphereHandleCap instead, because that
// method already matches Handles.CapFunction.
// ---------------------------------------------------------------------------
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class LegacyHandles
{
    public static void SphereCap(int controlID, Vector3 position, Quaternion rotation, float size)
    {
        Handles.SphereHandleCap(controlID, position, rotation, size, EventType.Repaint);
    }

    public static void ArrowCap(int controlID, Vector3 position, Quaternion rotation, float size)
    {
        Handles.ArrowHandleCap(controlID, position, rotation, size, EventType.Repaint);
    }

    public static void DotCap(int controlID, Vector3 position, Quaternion rotation, float size)
    {
        Handles.DotHandleCap(controlID, position, rotation, size, EventType.Repaint);
    }

    public static void CircleCap(int controlID, Vector3 position, Quaternion rotation, float size)
    {
        Handles.CircleHandleCap(controlID, position, rotation, size, EventType.Repaint);
    }
}
#endif
