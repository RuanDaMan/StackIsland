using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace StackIsland
{
    /// <summary>
    /// The sales counter. Stand on the painted SELL sign and your crab cakes hop onto the table; customers queue on
    /// the other side, each asking (in a speech bubble) for a few, then pay in cash bundles that pile up at the end
    /// of the counter for you to scoop up.
    /// </summary>
    public class Counter : MonoBehaviour
    {
        public string Id;
        public ItemType Sells = ItemType.CrabCake;
        public PileZone Shelf, Cash;
        public int MaxQueue = 8;
        public float SpawnEvery = 2.2f;
        public int MaxWant = 3;
        public float PriceMultiplier = 1f;
        public bool AutoCash;
        float _parrotT;
        GameObject _parrot;

        readonly List<Customer> _queue = new List<Customer>();
        float _spawn;
        Transform _sign;
        Material _signMat, _signIdle, _signActive;

        // queue path (world) from the service spot out through the entry gate
        readonly List<Vector3> _path = new List<Vector3>();
        readonly List<Vector3> _exit = new List<Vector3>();
        public IReadOnlyList<Vector3> ExitPath => _exit;

        public Yard Yard { get; private set; }

        public void Build(Yard yard, string id, ItemType sells)
        {
            Yard = yard;
            Id = id;
            Sells = sells;
            var table = ProcArt.Table(transform, 6f, 1.3f);
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.5f, 0);
            col.size = new Vector3(6f, 1f, 1.3f);
            _ = table;

            // painted SELL sign on the player's side
            _sign = new GameObject("sellSign").transform;
            _sign.SetParent(transform, false);
            _sign.localPosition = new Vector3(0, 0, 2.3f);
            _signIdle = Mats.Lit(new Color(0.86f, 0.86f, 0.9f));
            _signActive = Mats.Lit(new Color(0.35f, 0.82f, 0.4f));
            var slab = ProcArt.MeshObject("slab", _sign, ProcArt.RoundedSlab(4.4f, 2.2f, 0.5f, 0.06f), _signIdle, Vector3.zero);
            slab.GetComponent<Renderer>().receiveShadows = true;
            _signMat = null;
            ProcArt.FloorText(_sign, "SELL", 13f, new Color(0.38f, 0.38f, 0.44f), new Vector3(0, 0.05f, 0));

            var shelfDisplay = new GameObject("shelf").transform;
            shelfDisplay.SetParent(transform, false);
            shelfDisplay.localPosition = new Vector3(-0.6f, 0.96f, 0);
            Shelf = _sign.gameObject.AddComponent<PileZone>();
            Shelf.Id = id + ":shelf";
            Shelf.Type = Sells;
            Shelf.Capacity = 99999;   // effectively bottomless; only the first 36 are drawn
            Shelf.MaxShown = 36;
            Shelf.Drop = true;
            Shelf.Radius = 1.7f;
            Shelf.Display = shelfDisplay;
            Shelf.Columns = 6;
            Shelf.Rows = 2;
            Shelf.Spacing = new Vector2(0.62f, 0.55f);

            // cash tray at the end of the counter
            var cashGo = new GameObject("cash");
            cashGo.transform.SetParent(transform, false);
            cashGo.transform.localPosition = new Vector3(4.3f, 0, 1.9f);
            ProcArt.Tray(cashGo.transform, 1.9f, 1.4f, new Color(0.5f, 0.85f, 0.5f));
            var cashDisplay = new GameObject("display").transform;
            cashDisplay.SetParent(cashGo.transform, false);
            cashDisplay.localPosition = new Vector3(0, 0.14f, 0);
            Cash = cashGo.AddComponent<PileZone>();
            Cash.Id = id + ":cash";
            Cash.Type = ItemType.Cash;
            Cash.Capacity = 120;
            Cash.Take = true;
            Cash.Radius = 1.5f;
            Cash.Display = cashDisplay;
            Cash.Columns = 2;
            Cash.Rows = 3;
            Cash.Spacing = new Vector2(0.62f, 0.32f);

            var head = transform.TransformPoint(new Vector3(0, 0, -1.7f));
            _path.Add(head);
            var o = yard.Origin;
            _path.Add(new Vector3(o.x + Yard.EntryGateX, 0, head.z));
            _path.Add(new Vector3(o.x + Yard.EntryGateX, 0, o.z + Yard.MinZ - 2f));
            _path.Add(new Vector3(o.x + Yard.EntryGateX, 0, o.z + Yard.MinZ - 14f));
            _exit.Add(head + new Vector3(-1.5f, 0, -1.2f));
            _exit.Add(new Vector3(o.x + Yard.ExitGateX, 0, head.z - 1.6f));
            _exit.Add(new Vector3(o.x + Yard.ExitGateX, 0, o.z + Yard.MinZ - 2f));
            _exit.Add(new Vector3(o.x + Yard.ExitGateX, 0, o.z + Yard.MinZ - 12f));
        }

        /// <summary>Point <paramref name="d"/> metres back along the queue path.</summary>
        public Vector3 QueuePoint(float d)
        {
            for (int i = 1; i < _path.Count; i++)
            {
                float seg = Vector3.Distance(_path[i - 1], _path[i]);
                if (d <= seg) return Vector3.Lerp(_path[i - 1], _path[i], d / seg);
                d -= seg;
            }
            return _path[_path.Count - 1];
        }

        public Vector3 SpawnPoint => _path[_path.Count - 1];

        void Update()
        {
            // the sign lights up green while the player stands on it
            var p = Game.I != null && Game.I.Player != null ? Game.I.Player.transform.position : Vector3.one * 999;
            bool on = Shelf.InReach(p);
            var r = _sign.GetChild(0).GetComponent<Renderer>();
            var want = on ? _signActive : _signIdle;
            if (r.sharedMaterial != want) r.sharedMaterial = want;

            // the parrot also empties whatever was already on the tray
            if (AutoCash && Cash.Count > 0 && Game.I != null)
            {
                _parrotT -= Time.deltaTime;
                if (_parrotT <= 0)
                {
                    _parrotT = 0.08f;
                    Cash.Remove(out var at, out var v);
                    Game.I.AddCash(v, at);
                }
            }

            _spawn -= Time.deltaTime;
            if (_spawn <= 0 && _queue.Count < MaxQueue)
            {
                _spawn = SpawnEvery * Random.Range(0.8f, 1.2f);
                var go = new GameObject("Customer");
                go.transform.SetParent(transform.parent, false);
                var c = go.AddComponent<Customer>();
                c.Init(this, Random.Range(Economy.MinWant(MaxWant), MaxWant + 1));
                _queue.Add(c);
            }
            for (int i = 0; i < _queue.Count; i++) _queue[i].SetSlot(i);
        }

        /// <summary>Hire the Cash Parrot: it perches on the cash tray and sends all money to the wallet.</summary>
        public void EnableAutoCash()
        {
            AutoCash = true;
            if (_parrot != null) return;
            _parrot = Visuals.Spawn("cube-pets/animal-parrot", Cash.transform, 0.9f, Color.white);
            _parrot.transform.localPosition = new Vector3(0.7f, 0.12f, 0.45f);
            _parrot.transform.localRotation = Quaternion.Euler(0, 200, 0);
            Visuals.SetShadows(_parrot, true);
        }

        public double PriceEach => Items.Price(Sells) * PriceMultiplier * (Game.I != null ? Game.I.PriceBonus(Yard.Def.Index) : 1.0);

        public string Info()
        {
            return $"<b>Counter</b>\n{Items.Name(Sells)} sells for <color=#9CFF9A>{NumberFormatter.Format(PriceEach)}</color> each\n" +
                   $"{_queue.Count} customers in line, each wants {Economy.MinWant(MaxWant)}-{MaxWant}\n" +
                   $"On the counter: {Shelf.Count}\n" +
                   (AutoCash ? "Cash Parrot sends all money to your wallet" : $"Cash waiting: {NumberFormatter.Format(Cash.CashValue)} (walk over the tray)");
        }

        public bool IsHead(Customer c) => _queue.Count > 0 && _queue[0] == c;

        public void Done(Customer c) => _queue.Remove(c);

        /// <summary>A customer got what they wanted: pay in bundles at the cash tray.</summary>
        public void Pay(Customer c, int items)
        {
            double each = PriceEach;
            if (AutoCash && Game.I != null)
            {
                // the Cash Parrot takes the money straight to your wallet
                Game.I.AddCash(each * items, c.transform.position + Vector3.up * 1.2f);
                return;
            }
            for (int i = 0; i < items; i++)
                if (!Cash.Add(c.transform.position + Vector3.up * 1.2f, each))
                    Cash.CashValue += each;   // tray full: the money is still there, just not drawn
            Sfx.I?.Play(Sfx.I.Coin, 0.6f, 1.1f);
        }
    }

    /// <summary>A patient (mostly) customer: walks up the queue, asks for N items, takes them, pays, leaves.</summary>
    public class Customer : MonoBehaviour
    {
        static readonly string[] Models =
        {
            "mini-characters/character-male-a", "mini-characters/character-male-b", "mini-characters/character-male-c",
            "mini-characters/character-male-d", "mini-characters/character-male-e", "mini-characters/character-female-a",
            "mini-characters/character-female-b", "mini-characters/character-female-c", "mini-characters/character-female-d",
            "mini-characters/character-female-e",
        };
        static int _seed;

        Counter _counter;
        int _want, _got;
        int _slot;
        float _take, _waited, _bob;
        bool _leaving;
        int _exitIndex;
        Transform _model, _bubble;
        TextMeshPro _bubbleText;
        LabelPlate _plate;
        ItemStack _hands;

        public void Init(Counter counter, int want)
        {
            _counter = counter;
            _want = want;
            transform.position = counter.SpawnPoint;
            _model = Visuals.Spawn(Models[(_seed++ * 7) % Models.Length], transform, 1.6f, Color.white, true).transform;
            Visuals.SetShadows(_model.gameObject, true);
            _anim = CharAnim.Attach(_model.gameObject);
            var hands = new GameObject("hands");
            hands.transform.SetParent(transform, false);
            hands.transform.localPosition = new Vector3(0, 0.75f, 0.42f);
            _hands = hands.AddComponent<ItemStack>();
            _hands.Capacity = Mathf.Max(10, want);

            _bubble = new GameObject("bubble").transform;
            _bubble.SetParent(transform, false);
            _bubble.localPosition = new Vector3(0, 2.25f, 0);
            _bubble.gameObject.AddComponent<Billboard>();
            // white speech bubble: item icon + how many are still wanted; a green check once served
            var plate = new GameObject("plate").AddComponent<SpriteRenderer>();
            plate.transform.SetParent(_bubble, false);
            plate.sprite = UIKit.Rounded;
            plate.drawMode = SpriteDrawMode.Sliced;
            plate.size = new Vector2(1.25f, 0.78f);
            plate.color = new Color(1f, 1f, 1f, 0.96f);
            plate.transform.localPosition = new Vector3(0, 0, 0.02f);
            _plateSprite = plate;
            _icon = Items.Get(counter.Sells, _bubble).transform;
            _icon.localPosition = new Vector3(-0.24f, -0.12f, -0.05f);
            _icon.localRotation = Quaternion.Euler(-68f, 0, 0);
            _icon.localScale = Vector3.one * 0.95f;
            _bubbleText = new GameObject("count").AddComponent<TextMeshPro>();
            _bubbleText.transform.SetParent(_bubble, false);
            _bubbleText.transform.localPosition = new Vector3(0.28f, 0, -0.06f);
            _bubbleText.fontSize = 5f;
            _bubbleText.fontStyle = FontStyles.Bold;
            _bubbleText.alignment = TextAlignmentOptions.Center;
            _bubbleText.color = new Color(0.2f, 0.2f, 0.26f);
            _bubbleText.rectTransform.sizeDelta = new Vector2(0.8f, 0.8f);
            _check = ProcArt.CheckIcon(_bubble).transform;
            _check.localPosition = new Vector3(0, 0, -0.06f);
            _check.localScale = Vector3.one * 1.3f;
            _check.gameObject.SetActive(false);
            RefreshBubble();
        }

        SpriteRenderer _plateSprite;
        CharAnim _anim;
        Transform _icon, _check;

        public void SetSlot(int i) => _slot = i;

        void RefreshBubble()
        {
            int left = _want - _got;
            bool done = _leaving;
            _check.gameObject.SetActive(done);
            _icon.gameObject.SetActive(!done);
            _bubbleText.gameObject.SetActive(!done);
            _bubbleText.text = left.ToString();
            bool grumpy = !done && _waited > 7f && _counter.Shelf.Count == 0;
            _plateSprite.color = done ? new Color(0.85f, 1f, 0.85f, 0.96f) : grumpy ? new Color(1f, 0.82f, 0.78f, 0.96f) : new Color(1f, 1f, 1f, 0.96f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 target;
            if (_leaving)
            {
                var exit = _counter.ExitPath;
                target = exit[Mathf.Min(_exitIndex, exit.Count - 1)];
                if (Flat(target - transform.position) < 0.3f)
                {
                    _exitIndex++;
                    if (_exitIndex >= exit.Count) { _hands.Clear(); Destroy(gameObject); return; }
                }
                _bubble.gameObject.SetActive(_exitIndex < 1);
            }
            else
            {
                target = _counter.QueuePoint(_slot * 1.05f);
                // only the first few in line show what they want, so a long queue stays readable
                bool show = _slot < 3;
                if (_bubble.gameObject.activeSelf != show) _bubble.gameObject.SetActive(show);
            }

            var d = target - transform.position;
            d.y = 0;
            float dist = d.magnitude;
            if (dist > 0.05f)
            {
                transform.position += d.normalized * Mathf.Min(dist, 3.2f * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), 10f * dt);
                _bob += dt * 12f;
                _anim.SetMoving(true, 0.8f);
            }
            else if (!_leaving)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.forward), 6f * dt);
                _anim.SetMoving(false);
                _bob = Mathf.MoveTowards(_bob, Mathf.Round(_bob / Mathf.PI) * Mathf.PI, dt * 4f);
            }
            if (!_anim.HasClips) _model.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_bob)) * 0.1f, 0);

            if (_leaving) return;
            if (_counter.IsHead(this) && dist < 0.4f)
            {
                _waited += dt;
                _take -= dt;
                if (_take <= 0 && _got < _want && _counter.Shelf.Count > 0)
                {
                    _take = Economy.ServeSeconds;
                    _counter.Shelf.Remove(out var from, out _);
                    _hands.Add(_counter.Sells, from);
                    _got++;
                    _waited = 0;
                    Sfx.I?.Play(Sfx.I.Pop, 0.35f, 1.2f);
                    RefreshBubble();
                }
                if (_got >= _want)
                {
                    _counter.Pay(this, _got);
                    _leaving = true;
                    _counter.Done(this);
                    RefreshBubble();
                }
                else if (_waited > 7f) RefreshBubble();
            }
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
    }
}
