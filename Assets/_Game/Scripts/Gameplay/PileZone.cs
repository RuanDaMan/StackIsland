using System.Collections.Generic;
using UnityEngine;

namespace StackIsland
{
    /// <summary>
    /// A spot that holds items in a neat grid of little towers (a tray, a table top, the cash pile).
    /// Walk into its radius and items move between it and your stack: drop zones take what you carry,
    /// pick-up zones give you what they hold. Workers use the same zones.
    /// </summary>
    public class PileZone : MonoBehaviour
    {
        public static readonly List<PileZone> All = new List<PileZone>();

        public string Id;
        public ItemType Type;
        public int Capacity = 30;
        public bool Drop;                 // carriers put items here
        public bool Take;                 // carriers take items from here
        public float Radius = 1.4f;
        public Transform Display;         // where the items are drawn (defaults to this transform)
        public int Columns = 3, Rows = 2;
        public Vector2 Spacing = new Vector2(0.55f, 0.45f);
        public bool PlayerAllowed = true;
        public int MaxShown = int.MaxValue;  // items past this are counted but not drawn (huge piles)

        /// <summary>Total value of the cash in this pile (cash piles only); each bundle is worth an equal share.</summary>
        public double CashValue;

        readonly List<Transform> _views = new List<Transform>();
        int _count;
        TMPro.TextMeshPro _countLabel;

        public int Count => _count;
        public int Free => Mathf.Max(0, Capacity - _count);
        public bool IsFull => _count >= Capacity;
        Transform Root => Display != null ? Display : transform;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        Vector3 SlotLocal(int i)
        {
            int perLayer = Mathf.Max(1, Columns * Rows);
            int layer = i / perLayer;
            int k = i % perLayer;
            int cx = k % Columns, cz = k / Columns;
            float x = (cx - (Columns - 1) * 0.5f) * Spacing.x;
            float z = (cz - (Rows - 1) * 0.5f) * Spacing.y;
            return new Vector3(x, layer * Items.Height(Type), z);
        }

        public Vector3 NextSlotWorld => Root.TransformPoint(SlotLocal(_count));

        /// <summary>A floating count over piles too big to draw in full.</summary>
        void RefreshCount()
        {
            bool show = _count > MaxShown;
            if (!show) { if (_countLabel != null) _countLabel.gameObject.SetActive(false); return; }
            if (_countLabel == null)
            {
                int perLayer = Mathf.Max(1, Columns * Rows);
                float top = Mathf.CeilToInt(MaxShown / (float)perLayer) * Items.Height(Type) + 0.7f;
                _countLabel = Billboard.Label(Root, new Vector3(0, top, 0), 5f, Color.white, "", true);
            }
            _countLabel.gameObject.SetActive(true);
            _countLabel.text = NumberFormatter.Format(_count);
        }

        public bool InReach(Vector3 pos, float extra = 0f)
        {
            var d = pos - transform.position;
            d.y = 0;
            return d.magnitude <= Radius + extra;
        }

        /// <summary>Put one item in. If <paramref name="from"/> is given it hops over from there.</summary>
        public bool Add(Vector3? from = null, double cashValue = 0)
        {
            if (IsFull) return false;
            int i = _count++;
            CashValue += cashValue;
            RefreshCount();
            if (_views.Count >= MaxShown)
            {
                // pile is drawn at its cap: the item just hops onto the top and vanishes into it
                if (!from.HasValue || Tweens.I == null) return true;
                var hop = Items.Get(Type, null).transform;
                var top = Root;
                var topLocal = SlotLocal(MaxShown - 1);
                Tweens.I.Fly(hop, from.Value, () => top.TransformPoint(topLocal), 0.2f, 0.7f, () =>
                {
                    if (hop != null) Items.Release(Type, hop.gameObject);
                }, () => top.rotation);
                return true;
            }
            var go = Items.Get(Type, Root);
            var tr = go.transform;
            _views.Add(tr);
            tr.localPosition = SlotLocal(i);
            if (from.HasValue && Tweens.I != null)
            {
                tr.SetParent(null, true);
                var root = Root;
                var local = SlotLocal(i);
                Tweens.I.Fly(tr, from.Value, () => root.TransformPoint(local), 0.2f, 0.7f, () =>
                {
                    if (tr != null && _views.Contains(tr))
                    {
                        tr.SetParent(root, false);
                        tr.localPosition = local;
                        tr.localRotation = Quaternion.identity;
                    }
                }, () => root.rotation);
            }
            return true;
        }

        /// <summary>Take the top item. Returns its world position (for the flight) and its cash share.</summary>
        public bool Remove(out Vector3 at, out double cash)
        {
            at = transform.position;
            cash = 0;
            if (_count <= 0) return false;
            _count--;
            RefreshCount();
            if (Type == ItemType.Cash)
            {
                cash = _count == 0 ? CashValue : CashValue / (_count + 1);
                CashValue -= cash;
                if (_count == 0) CashValue = 0;
            }
            if (_views.Count == 0) return true;
            var tr = _views[_views.Count - 1];
            if (tr != null) at = tr.position;
            if (_views.Count <= _count) return true;   // hidden items left: the drawn pile stays as it is
            _views.RemoveAt(_views.Count - 1);
            if (tr != null) Items.Release(Type, tr.gameObject);
            return true;
        }

        /// <summary>Set the contents directly (loading a save).</summary>
        public void SetCount(int n, double cashValue = 0)
        {
            while (_count > 0) Remove(out _, out _);
            n = Mathf.Clamp(n, 0, Capacity);
            for (int i = 0; i < n; i++) Add();
            CashValue = cashValue;
        }
    }
}
