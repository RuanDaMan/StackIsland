using System.Collections.Generic;
using UnityEngine;

namespace StackIsland
{
    /// <summary>Shared bits for hired helpers: a body with walk/idle animations, a stack on their back, walking along waypoints.</summary>
    public abstract class Worker : MonoBehaviour
    {
        public float Speed = 3.6f;
        public Yard Yard;
        protected ItemStack Stack;
        protected Transform Model;
        protected CharAnim Anim;
        readonly List<Vector3> _route = new List<Vector3>();
        Vector3 _routeTarget = new Vector3(9999, 0, 9999);
        float _bob;

        public int Carrying => Stack != null ? Stack.Count : 0;
        public string DebugRoute => $"target {Yard.transform.InverseTransformPoint(_routeTarget)} route [{string.Join(", ", _route.ConvertAll(p => Yard.transform.InverseTransformPoint(p).ToString()))}]";

        public void Init(Yard yard, string model, Color cap, int capacity, Vector3 start)
        {
            Yard = yard;
            transform.position = start;
            Model = new GameObject("model").transform;
            Model.SetParent(transform, false);
            var body = Visuals.Spawn(model, Model, 1.6f, Color.white, true);
            Visuals.SetShadows(body, true);
            Anim = CharAnim.Attach(body);
            // a coloured cap so helpers stand out from customers
            var c = Visuals.Primitive(PrimitiveType.Sphere, Model, new Vector3(0, 1.62f, 0), new Vector3(0.62f, 0.3f, 0.62f), Mats.Lit(cap), "cap");
            c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var back = new GameObject("back");
            back.transform.SetParent(Model, false);
            back.transform.localPosition = new Vector3(0, 0.74f, -0.48f);
            Stack = back.AddComponent<ItemStack>();
            Stack.Capacity = capacity;
            Stack.Columns = 2;
            Stack.ColumnSpacing = 0.48f;
        }

        public void SetCapacity(int c) { if (Stack != null) Stack.Capacity = c; }

        /// <summary>Walk toward <paramref name="target"/> (detouring through the source gate). True once there.</summary>
        protected bool WalkTo(Vector3 target, float stopAt = 0.3f)
        {
            target.y = 0;
            if ((target - _routeTarget).sqrMagnitude > 0.25f)
            {
                _routeTarget = target;
                if (Game.I != null) Game.I.IslandRoute(transform.position, target, _route);   // may cross into other yards
                else Yard.Route(transform.position, target, _route);
            }
            if (_route.Count == 0) { Anim.SetMoving(false); return true; }
            var next = _route[0];
            var d = next - transform.position;
            d.y = 0;
            float dist = d.magnitude;
            bool last = _route.Count == 1;
            if (dist <= (last ? stopAt : 0.25f))
            {
                _route.RemoveAt(0);
                if (last) { _bob = 0; Model.localPosition = Vector3.zero; Anim.SetMoving(false); return true; }
                return false;
            }
            float dt = Time.deltaTime;
            transform.position += d / dist * Mathf.Min(dist, Speed * dt);
            Model.rotation = Quaternion.Slerp(Model.rotation, Quaternion.LookRotation(d / dist), 12f * dt);
            if (Anim.HasClips) Anim.SetMoving(true, Speed / 4.2f);
            else
            {
                _bob += dt * Speed * 2.6f;
                Model.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_bob)) * 0.1f, 0);
            }
            return false;
        }

        protected void Face(Vector3 p)
        {
            var d = p - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.01f) Model.rotation = Quaternion.Slerp(Model.rotation, Quaternion.LookRotation(d.normalized), 12f * Time.deltaTime);
        }
    }

    /// <summary>
    /// Gatherer (Crab Catcher, Coconut Picker, Fisher...): catches animals in the pen or sweeps the field,
    /// then carries the raw items to a station of its yard.
    /// </summary>
    public class CatcherWorker : Worker
    {
        enum Mode { Gather, Deliver }
        Mode _mode;
        float _attack, _transfer;
        LooseItem _claim;
        PileZone _drop;
        Vector3 _fieldSpot;
        bool _hasSpot;
        readonly List<Vector3> _picked = new List<Vector3>();

        void Start()
        {
            if (Yard.Def.Source != SourceKind.Pen) return;
            var tool = Visuals.Spawn("food-kit/cooking-knife", Model, 0.6f, Color.white).transform;
            tool.localPosition = new Vector3(0.42f, 0.72f, 0.12f);
            tool.localRotation = Quaternion.Euler(0, 0, -70);
        }

        void Update()
        {
            if (Game.I == null) return;
            if (_mode == Mode.Gather) { if (Yard.Pen != null) Hunt(); else if (Yard.Field != null) Pick(); }
            else Deliver();
        }

        /// <summary>
        /// While the player is gathering in this yard's source (and has room), helpers leave what's around them alone:
        /// the player gets first pick.
        /// </summary>
        Vector3? PlayerShare()
        {
            var p = Game.I.Player;
            if (p == null || p.Stack.IsFull || !Yard.InSource(p.transform.position)) return null;
            return p.transform.position;
        }

        const float PlayerShareRadius = 3.2f;

        void Pick()
        {
            if (Stack.IsFull) { _mode = Mode.Deliver; return; }
            var share = PlayerShare();
            // our spot is now next to the player: find another
            if (_hasSpot && share.HasValue && (_fieldSpot - share.Value).sqrMagnitude < PlayerShareRadius * PlayerShareRadius) _hasSpot = false;
            if (!_hasSpot && !(_hasSpot = Yard.Field.NearestItem(transform.position, out _fieldSpot, share, PlayerShareRadius)))
            {
                if (Stack.Count > 0) _mode = Mode.Deliver;
                return;
            }
            WalkTo(_fieldSpot, 0.4f);
            _transfer -= Time.deltaTime;
            if (_transfer > 0) return;
            _transfer = 0.12f;
            if (Yard.Field.Harvest(transform.position, 1.0f, Mathf.Min(2, Stack.Space), _picked, share, PlayerShareRadius) > 0)
                foreach (var p in _picked) Stack.Add(Yard.Def.Raw, p);
            if ((transform.position - _fieldSpot).sqrMagnitude < 0.5f) _hasSpot = false;
        }

        void Hunt()
        {
            if (Stack.IsFull) { Release(); _mode = Mode.Deliver; return; }
            // grab what's on the ground first
            var share = PlayerShare();
            if (_claim != null && share.HasValue && (_claim.transform.position - share.Value).sqrMagnitude < PlayerShareRadius * PlayerShareRadius) Release();
            if (_claim == null || !_claim.enabled || (_claim.ClaimedBy != null && _claim.ClaimedBy != (object)this))
            {
                Release();
                _claim = NearestLoose(share);
                if (_claim != null) _claim.ClaimedBy = this;
            }
            if (_claim != null)
            {
                if (WalkTo(_claim.transform.position, 0.6f) && _claim.Ready)
                {
                    var at = _claim.transform.position;
                    var t = _claim.Type;
                    _claim.Consume();
                    Stack.Add(t, at);
                    _claim = null;
                }
                return;
            }
            var target = NearestAnimal();
            if (target == null)
            {
                if (Stack.Count > 0) _mode = Mode.Deliver;
                return;
            }
            if (WalkTo(target.transform.position, 1.4f) || (target.transform.position - transform.position).magnitude < 1.6f)
            {
                Face(target.transform.position);
                _attack -= Time.deltaTime;
                if (_attack <= 0)
                {
                    _attack = 0.5f;
                    var dir = target.transform.position - transform.position;
                    dir.y = 0;
                    Anim.Attack(2f);
                    Fx.I?.Slash(transform.position + Vector3.up * 0.9f + dir.normalized * 0.7f, dir.normalized);
                    target.Hit(1, transform.position);
                }
            }
        }

        void Deliver()
        {
            // stick with the tray we picked: re-picking every frame ping-pongs between stations as their fill levels shift
            if (_drop == null || _drop.IsFull) _drop = Yard.BestStationInput(Yard.Def.Raw);
            var target = _drop;
            if (target == null) return;
            if (!WalkTo(target.transform.position, 1.0f)) return;
            _transfer -= Time.deltaTime;
            if (_transfer > 0) return;
            _transfer = 0.07f;
            if (Stack.Count > 0 && !target.IsFull)
            {
                Stack.Remove(Yard.Def.Raw, out var at);
                target.Add(at);
            }
            if (Stack.Count == 0 || target.IsFull) { _mode = Mode.Gather; _hasSpot = false; _drop = null; }
        }

        void Release()
        {
            if (_claim != null && _claim.ClaimedBy == (object)this) _claim.ClaimedBy = null;
            _claim = null;
        }

        LooseItem NearestLoose(Vector3? share)
        {
            LooseItem best = null;
            float bd = float.MaxValue;
            foreach (var li in LooseItem.All)
            {
                if (li.ClaimedBy != null || li.Yard != Yard) continue;
                if (share.HasValue && (li.transform.position - share.Value).sqrMagnitude < PlayerShareRadius * PlayerShareRadius) continue;
                float d = (li.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = li; }
            }
            return best;
        }

        Animal NearestAnimal()
        {
            Animal best = null;
            float bd = float.MaxValue;
            foreach (var a in Animal.All)
            {
                if (!a.Alive || a.Pen != Yard.Pen) continue;
                float d = (a.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = a; }
            }
            return best;
        }

        void OnDestroy() => Release();
    }

    /// <summary>Waiter: carries finished products from the yard's stations to its counter.</summary>
    public class WaiterWorker : Worker
    {
        bool _delivering;
        PileZone _src;
        float _transfer;

        void Update()
        {
            if (Game.I == null) return;
            _transfer -= Time.deltaTime;
            if (!_delivering)
            {
                // stick with the tray we picked: re-picking every frame ping-pongs between stations as their counts shift
                if (_src == null || _src.Count == 0) _src = Yard.BestStationOutput();
                var src = _src;
                if (src == null)
                {
                    if (Stack.Count > 0) _delivering = true;
                    return;
                }
                if (!WalkTo(src.transform.position, 1.0f)) return;
                if (_transfer <= 0 && src.Count > 0 && !Stack.IsFull)
                {
                    _transfer = 0.07f;
                    src.Remove(out var at, out _);
                    Stack.Add(src.Type, at);
                }
                if (Stack.IsFull || (src.Count == 0 && Stack.Count > 0)) { _delivering = true; _src = null; }
            }
            else
            {
                var shelf = Yard.Counter.Shelf;
                // stand on the SELL sign like the player does
                if (!WalkTo(shelf.transform.position, 0.8f)) return;
                if (_transfer <= 0 && Stack.Count > 0 && !shelf.IsFull)
                {
                    _transfer = 0.07f;
                    Stack.Remove(shelf.Type, out var at);
                    shelf.Add(at);
                }
                if (Stack.Count == 0) _delivering = false;
            }
        }
    }

    /// <summary>
    /// Courier (yards with no source of their own, like the Volcano Feast): walks the walkways to the yards that make
    /// the dishes the station needs, takes them off those yards' output trays, and brings them back.
    /// </summary>
    public class CourierWorker : Worker
    {
        bool _delivering;
        PileZone _src, _drop;
        float _transfer;

        void Update()
        {
            if (Game.I == null) return;
            _transfer -= Time.deltaTime;
            if (!_delivering)
            {
                if (_src == null || _src.Count == 0)
                {
                    _src = PickSource();
                    if (_src == null)
                    {
                        if (Stack.Count > 0) _delivering = true;
                        return;
                    }
                }
                if (!WalkTo(_src.transform.position, 1.0f)) return;
                if (_transfer <= 0 && _src.Count > 0 && !Stack.IsFull)
                {
                    _transfer = 0.07f;
                    _src.Remove(out var at, out _);
                    Stack.Add(_src.Type, at);
                }
                if (Stack.IsFull || _src.Count == 0) { _delivering = Stack.Count > 0; _src = null; }
            }
            else
            {
                if (Stack.Count == 0) { _delivering = false; return; }
                var t = Stack.ItemsHeld[Stack.Count - 1];
                if (_drop == null || _drop.IsFull || _drop.Type != t) _drop = Yard.BestStationInput(t);
                var target = _drop;
                if (target == null) return;   // trays full: wait until the table uses some
                if (!WalkTo(target.transform.position, 1.0f)) return;
                if (_transfer <= 0 && !target.IsFull)
                {
                    _transfer = 0.07f;
                    Stack.Remove(t, out var at);
                    target.Add(at);
                }
            }
        }

        /// <summary>The output tray to fetch from: whichever dish the station is shortest of, from the yard that makes it.</summary>
        PileZone PickSource()
        {
            PileZone best = null;
            int lowest = int.MaxValue;
            foreach (var st in Yard.Stations)
                foreach (var input in st.Inputs)
                {
                    if (input.Count >= lowest || input.IsFull) continue;
                    foreach (var y in Game.I.Yards.Values)
                    {
                        if (y.Def.Product != input.Type) continue;
                        var output = y.BestStationOutput();
                        if (output == null || output.Count == 0) continue;
                        lowest = input.Count;
                        best = output;
                    }
                }
            return best;
        }
    }
}
