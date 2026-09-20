using System;
using System.Collections.Generic;
using UnityEngine;

public class VirtualScrollView
{
    private const int Overscan = 1;
    private const float Padding = 10f;
    private readonly UIPanel panel;
    private readonly UIScrollBar scrollBar;
    private readonly Func<string[], GameObject> itemOnListProducer;
    private readonly Action<GameObject, string[]> itemBinder;
    private readonly float heightOfEach;
    private readonly UIScrollView scrollView;
    private readonly UIWidget contentBounds;
    private readonly List<Row> rows = new List<Row>();
    private readonly Stack<Row> available = new Stack<Row>();
    private readonly Dictionary<int, Row> visible = new Dictionary<int, Row>();
    private int selectedIndex = -1;
    private bool syncingScrollBar;
    private bool refreshing;
    private bool waitingForRelease;
    private Vector4 lastClipRegion;

    public Action<GameObject> itemOnListHider;
    public Action<GameObject> itemOnSelect;
    public Action<GameObject> itemOnListShower;
    public Action<GameObject, bool> selectHandler;

    public class Item
    {
        public string[] Args;
        // Only rows currently bound to this item have an object.
        public GameObject gameObject;
    }

    private class Row
    {
        public GameObject gameObject;
        public Item item;
        public int index = -1;
    }

    public List<Item> Items = new List<Item>();

    public VirtualScrollView(UIPanel panel, UIScrollBar scrollBar,
        Func<string[], GameObject> itemOnListProducer, float heightOfEach,
        Action<GameObject, string[]> itemBinder)
    {
        if (heightOfEach <= 0f) throw new ArgumentOutOfRangeException(nameof(heightOfEach));
        this.panel = panel;
        this.scrollBar = scrollBar;
        this.itemOnListProducer = itemOnListProducer;
        this.itemBinder = itemBinder ?? throw new ArgumentNullException(nameof(itemBinder));
        this.heightOfEach = heightOfEach;

        scrollView = panel.GetComponent<UIScrollView>() ?? panel.gameObject.AddComponent<UIScrollView>();
        scrollView.can_be_draged = false;
        scrollView.movement = UIScrollView.Movement.Vertical;
        scrollView.contentPivot = UIWidget.Pivot.TopLeft;
        scrollView.dragEffect = UIScrollView.DragEffect.Momentum;
        scrollView.disableDragIfFits = true;

        // NGUI derives drag and momentum bounds from active widgets. Keep the
        // full data extent even though only a few rows are instantiated.
        var boundsObject = new GameObject("ScrollContentBounds");
        boundsObject.layer = panel.gameObject.layer;
        boundsObject.transform.SetParent(panel.transform, false);
        contentBounds = boundsObject.AddComponent<UIWidget>();
        contentBounds.alpha = 0f;
        contentBounds.width = 2;
        contentBounds.pivot = UIWidget.Pivot.Center;

        UIHelper.registEvent(scrollBar, onScrollBarChange);
        scrollView.onScrolled += RefreshVisible;
        panel.onClipMove += OnClipMove;
        panel.gameObject.AddComponent<VirtualScrollViewLifecycle>().refreshAfterRelease = RefreshAfterRelease;
        lastClipRegion = panel.baseClipRegion;
        UpdateContentBounds();
        toTop();
    }

    public GameObject getSelected()
    {
        Row row;
        return visible.TryGetValue(selectedIndex, out row) ? row.gameObject : null;
    }

    public string[] getSelectedArgs()
    {
        return selectedIndex >= 0 && selectedIndex < Items.Count ? Items[selectedIndex].Args : null;
    }

    public void setSelected(GameObject obj)
    {
        if (obj == null)
        {
            ChangeSelection(-1);
            return;
        }
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].gameObject == obj && rows[i].index >= 0)
            {
                ChangeSelection(rows[i].index);
                return;
            }
    }

    public void selectIndex(int i = 0)
    {
        if (i >= 0 && i < Items.Count) ChangeSelection(i);
    }

    public void selectArg(string[] task)
    {
        for (int i = 0; i < Items.Count; i++)
            if (SameArgs(Items[i].Args, task))
            {
                ChangeSelection(i);
                return;
            }
    }

    private void ChangeSelection(int index)
    {
        if (selectedIndex == index) return;
        var previous = getSelected();
        if (previous != null) selectHandler?.Invoke(previous, false);
        selectedIndex = index;
        var current = getSelected();
        if (current != null) selectHandler?.Invoke(current, true);
        itemOnSelect?.Invoke(current);
    }

    public void print(List<string[]> tasks)
    {
        var selectedArgs = getSelectedArgs();
        var previous = getSelected();
        if (previous != null) selectHandler?.Invoke(previous, false);
        selectedIndex = -1;
        // Preserve the pool across searches. A pressed row keeps its old data
        // until NGUI has delivered release/click notifications to that object.
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.item == null) continue;
            if (IsInTouch(row.gameObject))
            {
                row.index = -1;
                waitingForRelease = true;
            }
            else Recycle(row);
        }
        visible.Clear();
        Items.Clear();
        if (tasks != null)
            for (int i = 0; i < tasks.Count; i++)
            {
                Items.Add(new Item { Args = tasks[i] });
                if (selectedArgs != null && SameArgs(selectedArgs, tasks[i])) selectedIndex = i;
            }
        UpdateContentBounds();
        StopMovement();
        MoveTo(scrollBar.value);
        RefreshVisible();
    }

    public void clear()
    {
        print(null);
    }

    private static bool SameArgs(string[] left, string[] right)
    {
        if (left == null || right == null || left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++)
            if (left[i] != right[i]) return false;
        return true;
    }

    private float ScrollRange => Mathf.Max(0f, Items.Count * heightOfEach + Padding * 2f - panel.GetViewSize().y);
    private float TopOffset => -panel.baseClipRegion.y - (panel.GetViewSize().y - heightOfEach) * 0.5f + Padding;

    private void UpdateContentBounds()
    {
        contentBounds.height = Mathf.Max(2, Mathf.CeilToInt(Items.Count * heightOfEach + Padding * 2f));
        contentBounds.transform.localPosition = new Vector3(0f, -(Items.Count - 1) * heightOfEach * 0.5f, 0f);
        scrollView.InvalidateBounds();
    }

    private void OnClipMove(UIPanel movedPanel)
    {
        RefreshVisible();
    }

    private void onScrollBarChange()
    {
        if (syncingScrollBar) return;
        MoveTo(scrollBar.value);
        RefreshVisible();
    }

    private void MoveTo(float value)
    {
        syncingScrollBar = true;
        try
        {
            float offset = TopOffset - Mathf.Clamp01(value) * ScrollRange;
            panel.clipOffset = new Vector2(panel.clipOffset.x, offset);
            var position = scrollView.transform.localPosition;
            position.y = -panel.clipOffset.y;
            scrollView.transform.localPosition = position;
        }
        finally { syncingScrollBar = false; }
    }

    public void toTop()
    {
        StopMovement();
        MoveTo(0f);
        RefreshVisible();
    }

    private void StopMovement()
    {
        scrollView.currentMomentum = Vector3.zero;
        scrollView.mScroll = 0f;
        scrollView.DisableSpring();
    }

    public void RefreshVisible()
    {
        if (syncingScrollBar || refreshing) return;
        refreshing = true;
        try
        {
            if (lastClipRegion != panel.baseClipRegion)
            {
                lastClipRegion = panel.baseClipRegion;
                MoveTo(scrollBar.value);
            }
            syncingScrollBar = true;
            try
            {
                float range = ScrollRange;
                scrollBar.value = range > 0f ? (TopOffset - panel.clipOffset.y) / range : 0f;
                scrollBar.barSize = Mathf.Clamp(panel.GetViewSize().y /
                    Mathf.Max(1f, Items.Count * heightOfEach + Padding * 2f), 0.1f, 1f);
            }
            finally { syncingScrollBar = false; }

            Vector4 clip = panel.finalClipRegion;
            float top = clip.y + clip.w * 0.5f;
            float bottom = clip.y - clip.w * 0.5f;
            int first = Mathf.Max(0, Mathf.CeilToInt((-top - heightOfEach * 0.5f) / heightOfEach) - Overscan);
            int last = Mathf.Min(Items.Count - 1, Mathf.FloorToInt((-bottom + heightOfEach * 0.5f) / heightOfEach) + Overscan);

            waitingForRelease = false;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.item == null || (row.index >= first && row.index <= last && row.index >= 0)) continue;
                if (IsInTouch(row.gameObject)) waitingForRelease = true;
                else Recycle(row);
            }
            for (int i = first; i <= last; i++)
            {
                if (visible.ContainsKey(i)) continue;
                Bind(i);
            }
        }
        finally { refreshing = false; }
    }

    private void Bind(int index)
    {
        Row row;
        if (available.Count > 0) row = available.Pop();
        else
        {
            row = new Row { gameObject = itemOnListProducer(Items[index].Args) };
            row.gameObject.SetActive(false);
            row.gameObject.transform.SetParent(panel.transform, false);
            var collider = row.gameObject.GetComponentInChildren<BoxCollider>(true);
            if (collider != null)
            {
                var drag = collider.GetComponent<UIDragScrollView>() ?? collider.gameObject.AddComponent<UIDragScrollView>();
                drag.scrollView = scrollView;
            }
            rows.Add(row);
        }
        row.index = index;
        row.item = Items[index];
        row.item.gameObject = row.gameObject;
        row.gameObject.transform.localPosition = new Vector3(0f, -index * heightOfEach, 0f);
        itemBinder(row.gameObject, row.item.Args);
        visible.Add(index, row);
        row.gameObject.SetActive(true);
        selectHandler?.Invoke(row.gameObject, index == selectedIndex);
        itemOnListShower?.Invoke(row.gameObject);
    }

    private void Recycle(Row row)
    {
        if (row.index >= 0)
        {
            visible.Remove(row.index);
            if (row.index == selectedIndex) selectHandler?.Invoke(row.gameObject, false);
        }
        itemOnListHider?.Invoke(row.gameObject);
        row.gameObject.SetActive(false);
        row.item.gameObject = null;
        row.item = null;
        row.index = -1;
        available.Push(row);
        scrollView.InvalidateBounds();
    }

    private static bool IsInTouch(GameObject row)
    {
        for (int i = 0; i < 3; i++)
            if (ContainsTouch(row.transform, UICamera.GetMouse(i))) return true;
        for (int i = 0; i < UICamera.activeTouches.Count; i++)
            if (ContainsTouch(row.transform, UICamera.activeTouches[i])) return true;
        return ContainsTouch(row.transform, UICamera.controller);
    }

    private static bool ContainsTouch(Transform row, UICamera.MouseOrTouch touch)
    {
        return touch != null &&
            ((touch.pressed != null && touch.pressed.transform.IsChildOf(row)) ||
             (touch.dragged != null && touch.dragged.transform.IsChildOf(row)));
    }

    private void RefreshAfterRelease()
    {
        if (waitingForRelease) RefreshVisible();
    }
}

// NGUI may stop moving before releasing a touch. Recycle those protected rows
// after its Update has finished dispatching the final input events.
public sealed class VirtualScrollViewLifecycle : MonoBehaviour
{
    public Action refreshAfterRelease;

    private void LateUpdate()
    {
        refreshAfterRelease?.Invoke();
    }
}
