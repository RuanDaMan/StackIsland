using System.Collections.Generic;
using UnityEngine;

namespace StackIsland
{
    /// <summary>
    /// A fenced field packed with a raw item (coconuts, bananas, eggs...), like the item "seas" in the genre.
    /// Walk through it and everything near you hops onto your stack; the field slowly refills by itself.
    /// "Bigger" upgrades unlock more rows and make it refill faster.
    /// </summary>
    public class FieldPatch : MonoBehaviour
    {
        const float Cell = 0.62f;
        public Yard Yard { get; private set; }
        public int Level { get; private set; }
        public float RegrowPerSecond => Economy.FieldRegrow(Level);

        readonly List<Vector3> _cells = new List<Vector3>();
        readonly List<GameObject> _items = new List<GameObject>();   // null = empty cell
        int _activeCells;
        float _regrow;
        int _present;

        public int Present => _present;
        public int Capacity => _activeCells;

        public void Init(Yard yard)
        {
            Yard = yard;
            var rng = new System.Random(yard.Def.Index * 7);
            // fill from the back of the field forwards, so "bigger" grows toward the gate
            for (float z = Yard.SrcMaxZ - 0.8f; z > Yard.SrcMinZ + 1.4f; z -= Cell)
                for (float x = Yard.SrcMinX + 0.8f; x < Yard.SrcMaxX - 0.6f; x += Cell)
                {
                    var jitter = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.2f, 0.07f, ((float)rng.NextDouble() - 0.5f) * 0.2f);
                    _cells.Add(yard.W(x, z) + jitter);
                    _items.Add(null);
                }
            SetLevel(0);
            // start full
            for (int i = 0; i < _activeCells; i++) Fill(i);
        }

        public void SetLevel(int level)
        {
            Level = level;
            // 40% of the field at first, the whole field at level 8
            _activeCells = Mathf.Clamp(Mathf.RoundToInt(_cells.Count * Economy.FieldShare(level)), 1, _cells.Count);
        }

        void Fill(int i)
        {
            if (_items[i] != null) return;
            var go = Items.Get(Yard.Def.Raw, transform);
            go.transform.position = _cells[i];
            go.transform.rotation = Quaternion.Euler(0, (i * 73) % 360, 0);
            _items[i] = go;
            _present++;
        }

        /// <summary>
        /// Take up to <paramref name="max"/> items within <paramref name="radius"/> of <paramref name="pos"/>,
        /// leaving alone anything within <paramref name="keepRadius"/> of <paramref name="keepFor"/> (the player's share).
        /// </summary>
        public int Harvest(Vector3 pos, float radius, int max, List<Vector3> at, Vector3? keepFor = null, float keepRadius = 0f)
        {
            at.Clear();
            if (max <= 0) return 0;
            float r2 = radius * radius;
            for (int i = 0; i < _activeCells && at.Count < max; i++)
            {
                var go = _items[i];
                if (go == null) continue;
                var d = _cells[i] - pos;
                d.y = 0;
                if (d.sqrMagnitude > r2 || Kept(i, keepFor, keepRadius)) continue;
                at.Add(go.transform.position);
                Items.Release(Yard.Def.Raw, go);
                _items[i] = null;
                _present--;
            }
            return at.Count;
        }

        /// <summary>A spot with items, nearest to <paramref name="from"/> (for pickers), outside the player's share.</summary>
        public bool NearestItem(Vector3 from, out Vector3 spot, Vector3? keepFor = null, float keepRadius = 0f)
        {
            spot = from;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < _activeCells; i++)
            {
                if (_items[i] == null || Kept(i, keepFor, keepRadius)) continue;
                float d = (_cells[i] - from).sqrMagnitude;
                if (d < best) { best = d; spot = _cells[i]; found = true; }
            }
            return found;
        }

        bool Kept(int i, Vector3? keepFor, float keepRadius)
        {
            if (!keepFor.HasValue) return false;
            var d = _cells[i] - keepFor.Value;
            d.y = 0;
            return d.sqrMagnitude < keepRadius * keepRadius;
        }

        void Update()
        {
            if (_present >= _activeCells) return;
            _regrow += RegrowPerSecond * Time.deltaTime;
            int guard = 0;
            while (_regrow >= 1f && _present < _activeCells && guard++ < 50)
            {
                // refill a random empty cell (scan from a random start)
                int start = Random.Range(0, _activeCells);
                for (int k = 0; k < _activeCells; k++)
                {
                    int i = (start + k) % _activeCells;
                    if (_items[i] == null) { Fill(i); _items[i].transform.localScale = Vector3.one * 0.3f; break; }
                }
                _regrow -= 1f;
            }
            // pop newly grown items up to full size
            for (int i = 0; i < _activeCells; i++)
            {
                var go = _items[i];
                if (go == null) continue;
                var s = go.transform.localScale.x;
                if (s < 1f) go.transform.localScale = Vector3.one * Mathf.MoveTowards(s, 1f, Time.deltaTime * 3f);
            }
        }

        public string Info()
        {
            return $"<b>{Yard.Def.Name} field</b>\n{_present}/{_activeCells} {Items.Name(Yard.Def.Raw)} ready\n" +
                   $"Regrows {RegrowPerSecond:0.#} per second\nWalk through it to gather";
        }
    }
}
