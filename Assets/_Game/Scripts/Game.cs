using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace StackIsland
{
    /// <summary>
    /// Composition root: builds the island and the yards that are open, the player and camera, every buy tile
    /// (all progression lives here and in <see cref="YardDefs"/>, as data), the UI, and handles cash and saving.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public static bool TestMode;        // tests / autoshot: no menu, throwaway save
        public static bool FreshStart;      // wipe the save on the next load
        public static bool SkipMenuOnce;

        public SaveData Save { get; private set; }
        public SettingsData Settings { get; private set; }
        public Player Player { get; private set; }
        public FollowCamera Cam { get; private set; }
        public Hud Hud { get; private set; }
        public readonly Dictionary<int, Yard> Yards = new Dictionary<int, Yard>();
        readonly Dictionary<string, BuyPad> _pads = new Dictionary<string, BuyPad>();
        Transform _world;
        float _autosave;
        bool _loading;

        public double Cash => Save.cash;
        public bool InputBlocked => Hud != null && Hud.IsMenuOpen;
        public bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        public Yard Yard1 => Yards.TryGetValue(1, out var y) ? y : null;

        void Awake()
        {
            I = this;
            PileZone.All.Clear();
            BuyPad.All.Clear();
            LooseItem.All.Clear();
            Animal.All.Clear();
            Items.ResetPool();

            var art = Resources.Load<ArtLibrary>("ArtLibrary");
            Mats.Init(art);
            Visuals.Init(art);
            GameInput.Init();
            Settings = SaveSystem.LoadSettings();
            ApplyWindowMode();
            if (FreshStart) { SaveSystem.Delete(); FreshStart = false; }
            Save = (TestMode ? null : SaveSystem.Load()) ?? new SaveData();

            new GameObject("Tweens").AddComponent<Tweens>();
            new GameObject("Fx").AddComponent<Fx>();
            var sfx = new GameObject("Sfx").AddComponent<Sfx>();
            sfx.Volume = Settings.volume;
            SetupLighting();

            _world = new GameObject("World").transform;
            IslandBuilder.Build(_world, YardDefs.All.Count);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 220f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.62f, 0.9f);
            camGo.AddComponent<AudioListener>();
            Cam = camGo.AddComponent<FollowCamera>();

            var pg = new GameObject("Player");
            Player = pg.AddComponent<Player>();
            Player.Init();
            Cam.Target = Player.transform;

            _loading = true;
            OpenYard(1);
            LoadState();
            _loading = false;
            Player.Teleport(Save.px != 0 || Save.pz != 0 ? new Vector3(Save.px, 0, Save.pz) : new Vector3(0, 0, -3));

            Hud = new GameObject("UI").AddComponent<Hud>();
            Hud.Init(this);
            if (!TestMode) PayOffline();
        }

        /// <summary>Coins per second the helpers earn on their own right now.</summary>
        public double AutomatedIncome() => BalanceModel.AutomatedIncome(Level, Yards.Count);

        /// <summary>Helpers kept working while the game was closed: pay out what they earned (capped).</summary>
        void PayOffline()
        {
            if (Save.lastSaveUnix <= 0) return;
            double away = SaveSystem.NowUnix() - Save.lastSaveUnix;
            if (away < 60) return;
            int night = Level("g.night");
            double counted = System.Math.Min(away, Economy.OfflineCapHours(night) * 3600);
            double earned = AutomatedIncome() * counted * Economy.OfflineEfficiency(night);
            if (earned <= 0) return;
            Save.cash += earned;
            Save.totalEarned += earned;
            Hud.QueueWelcome(earned, away, counted, Economy.OfflineEfficiency(night));
        }

        void Start()
        {
            Cam.Snap();
            if (!TestMode && !SkipMenuOnce) Hud.ShowMainMenu();
            SkipMenuOnce = false;
        }

        // ------------------------------------------------------------------ yards

        /// <summary>Build a yard (first time it opens, or on load) with all its tiles.</summary>
        Yard OpenYard(int index)
        {
            if (Yards.TryGetValue(index, out var existing)) return existing;
            var def = YardDefs.Get(index);
            if (def == null) return null;
            var y = new GameObject("Yard " + index + " " + def.Name).AddComponent<Yard>();
            y.transform.SetParent(_world, false);
            y.Build(def);
            Yards[index] = y;
            IslandBuilder.YardLinks(_world, index);
            BuildYardPads(y);
            if (index == 1) BuildGlobalPads(y);
            // apply already-owned levels (load) and saved piles
            // (snapshot: a bought gate opens the next yard from inside this loop, which adds more tiles)
            foreach (var p in new List<BuyPad>(BuyPad.All))
                if (p.Id.StartsWith($"y{index}.") || (index == 1 && p.Id.StartsWith("g.")))
                {
                    var s = Save.Pad(p.Id);
                    p.Load(s.level, s.paid);
                    if (s.level > 0) p.OnBought?.Invoke(s.level);
                }
            foreach (var z in y.GetComponentsInChildren<PileZone>())
            {
                if (string.IsNullOrEmpty(z.Id)) continue;
                var s = Save.Pile(z.Id);
                if (s.count > 0) z.SetCount(s.count, s.cash);
            }
            if (Yards.TryGetValue(index - 1, out var prev)) prev.OpenEastGate();
            if (!_loading)
            {
                Fx.I?.Confetti(y.W(0, 0), 200);
                Hud?.Toast($"Welcome to {def.Name}!");
            }
            foreach (var p in BuyPad.All) p.Refresh();
            return y;
        }

        /// <summary>Where the player may walk: the grass around the open yards, not out into the sea or past the last yard.</summary>
        public Rect WalkArea
        {
            get
            {
                int last = 1;
                foreach (int i in Yards.Keys) last = Mathf.Max(last, i);
                float x0 = Yard.OriginOf(1).x - 22f, x1 = Yard.OriginOf(last).x + 22f;
                return Rect.MinMaxRect(x0, -25f, x1, 25f);
            }
        }

        /// <summary>Quick travel: jump to an open yard (keys 1-9, 0). Lands on a clear spot between the tiles.</summary>
        public bool TravelTo(int index)
        {
            if (!Yards.TryGetValue(index, out var y)) return false;
            Fx.I?.Puff(Player.transform.position + Vector3.up, 16);
            Player.Teleport(y.W(5.5f, -5f));
            Cam.Snap();
            Fx.I?.Puff(Player.transform.position + Vector3.up, 24);
            Sfx.I?.Play(Sfx.I.Pop, 0.5f, 0.8f);
            return true;
        }

        public Yard YardAt(Vector3 p)
        {
            foreach (var y in Yards.Values) if (y.Contains(p)) return y;
            return null;
        }

        /// <summary>
        /// Waypoints across the island: inside a yard its own routing (detouring through its pen gate), between yards
        /// out through the side gates and along the walkways.
        /// </summary>
        public void IslandRoute(Vector3 from, Vector3 to, List<Vector3> pts)
        {
            pts.Clear();
            int a = Mathf.RoundToInt(from.x / Yard.Spacing) + 1, b = Mathf.RoundToInt(to.x / Yard.Spacing) + 1;
            var tmp = new List<Vector3>();
            var cur = from;
            float gz = (Yard.SideGateZ0 + Yard.SideGateZ1) * 0.5f;
            while (a != b && Yards.TryGetValue(a, out var ya))
            {
                int dir = b > a ? 1 : -1;
                var exit = ya.W(dir > 0 ? Yard.MaxX - 0.6f : Yard.MinX + 0.6f, gz);
                ya.Route(cur, exit, tmp);
                pts.AddRange(tmp);
                a += dir;
                if (!Yards.TryGetValue(a, out var yb)) break;
                cur = yb.W(dir > 0 ? Yard.MinX + 0.6f : Yard.MaxX - 0.6f, gz);
                pts.Add(cur);
            }
            if (Yards.TryGetValue(b, out var yt)) { yt.Route(cur, to, tmp); pts.AddRange(tmp); }
            else pts.Add(to);
        }

        public double PriceBonus(int yard) => Economy.PriceMult(Level($"y{yard}.recipe"));

        // ------------------------------------------------------------------ progression (all tiles; numbers in Economy)

        void BuildYardPads(Yard y)
        {
            var d = y.Def;
            string k = $"y{d.Index}.";
            int L(string key) => Level(k + key);
            foreach (var spec in Economy.YardTiles)
            {
                var sp = spec;
                if (!Economy.TileApplies(d, sp.Key)) continue;
                string title = null;
                System.Action<int> apply = null;
                System.Func<int, string> describe = null;
                switch (sp.Key)
                {
                    case "src":
                        title = d.SourceTitle;
                        apply = l =>
                        {
                            if (y.Pen != null) { y.Pen.SetCount(Economy.PenAnimals(l)); y.Pen.RespawnSeconds = Economy.PenRespawn(l); }
                            if (y.Field != null) y.Field.SetLevel(l);
                        };
                        describe = l => y.Pen != null
                            ? $"+2 {d.AnimalName}s, and they come back faster ({Economy.PenAnimals(l)} > {Economy.PenAnimals(l + 1)})"
                            : $"More of the field fills up and it regrows faster ({Economy.FieldRegrow(l):0.#} > {Economy.FieldRegrow(l + 1):0.#} per second)";
                        break;
                    case "gather":
                        title = d.GathererName;
                        apply = l => SyncWorkers(y);
                        describe = l => d.Source == SourceKind.None
                            ? $"A helper who fetches dishes from the other yards for the {d.StationName} ({l} > {l + 1} hired)"
                            : d.Source == SourceKind.Pen
                                ? $"A helper who catches {d.AnimalName}s and brings the {Items.Name(d.Raw)} to the {d.StationName} ({l} > {l + 1} hired). " +
                                  $"Every hire makes you and them stronger: {(l == 0 ? HitsText(d, 1) : $"{HitsText(d, l)} > {HitsText(d, l + 1)}")} per {d.AnimalName}"
                                : $"A helper who gathers {Items.Name(d.Raw)} and brings it to the {d.StationName} ({l} > {l + 1} hired)";
                        break;
                    case "speed":
                        title = d.SpeedTitle;
                        apply = l => { foreach (var st in y.Stations) st.CookSeconds = Economy.CookSeconds(d, l); };
                        describe = l => $"{d.StationName} works 10% faster ({Economy.CookSeconds(d, l):0.00}s > {Economy.CookSeconds(d, l + 1):0.00}s each)";
                        break;
                    case "waiter":
                        title = "Waiter";
                        apply = l => SyncWorkers(y);
                        describe = l => $"A helper who carries {Items.Name(d.Product)} from the {d.StationName} to the counter ({l} > {l + 1} hired)";
                        break;
                    case "parrot":
                        title = "Cash Parrot";
                        apply = l => { if (l >= 1) y.Counter.EnableAutoCash(); };
                        describe = l => "Collects every customer's cash for you, straight into your wallet";
                        break;
                    case "station2":
                        title = "Second " + d.StationName;
                        apply = l =>
                        {
                            if (l < 1 || y.Stations.Count >= 2) return;
                            var st = y.AddStation();
                            st.CookSeconds = Economy.CookSeconds(d, L("speed"));
                            LoadPiles(st.gameObject);
                        };
                        describe = l => $"Builds a second {d.StationName} (twice the output)";
                        break;
                    case "recipe":
                        title = d.RecipeTitle;
                        apply = l => { };
                        describe = l => $"{Items.Name(d.Product)} sells for more (+15% of base) ({NumberFormatter.Format(Items.Price(d.Product) * Economy.PriceMult(l))} > {NumberFormatter.Format(Items.Price(d.Product) * Economy.PriceMult(l + 1))})";
                        break;
                    case "busy":
                        title = "More Customers";
                        apply = l => { y.Counter.SpawnEvery = Economy.SpawnEvery(l); y.Counter.MaxWant = Economy.MaxWant(l); };
                        describe = l => $"Customers arrive 10% more often and buy more (up to {Economy.MaxWant(l + 1)} each)";
                        break;
                    case "manager":
                        title = "Island Manager";
                        apply = l => { if (l >= 1) HireManager(y); };
                        describe = l => $"Buys this yard's upgrades for you, cheapest first, whenever you can afford them";
                        break;
                    case "crew":
                        title = "Helper Shoes";
                        apply = l => SyncWorkers(y);
                        describe = l => $"Helpers walk 8% faster and carry 2 more ({Economy.GathererCarry(l)} > {Economy.GathererCarry(l + 1)})";
                        break;
                }
                Pad(y, k + sp.Key, sp, title, l => sp.Cost(l, d.Scale), () => Economy.Available(sp.Key, L, Save.totalEarned, d.Scale), apply, describe);
            }

            var next = YardDefs.Get(d.Index + 1);
            if (next != null)
                Pad(y, k + "gate", Economy.Gate, "To " + next.Name, l => next.GateCost, () => Economy.Available("gate", L, Save.totalEarned, d.Scale),
                    l => { if (l >= 1) OpenYard(next.Index); },
                    l => $"Opens the gate to the next yard: {next.Name}. {next.Tagline}");
        }

        void BuildGlobalPads(Yard y)
        {
            foreach (var spec in Economy.GlobalTiles)
            {
                var sp = spec;
                if (sp.Key == "backpack")
                    Pad(y, "g.backpack", sp, "Bigger Stack", l => sp.Cost(l, 1), () => Economy.GlobalAvailable(sp.Key, Save.totalEarned),
                        l => Player.Stack.Capacity = Economy.Backpack(l),
                        l => $"Carry 4 more items ({Economy.Backpack(l)} > {Economy.Backpack(l + 1)})");
                else if (sp.Key == "shoes")
                    Pad(y, "g.shoes", sp, "Running Shoes", l => sp.Cost(l, 1), () => Economy.GlobalAvailable(sp.Key, Save.totalEarned),
                        l => Player.Speed = Economy.WalkSpeed(l),
                        l => "Walk 8% faster");
                else if (sp.Key == "night")
                    Pad(y, "g.night", sp, "Night Shift", l => sp.Cost(l, 1), () => Economy.GlobalAvailable(sp.Key, Save.totalEarned),
                        l => { },
                        l => $"Helpers earn more while you're away ({Economy.OfflineEfficiency(l) * 100:0}% > {Economy.OfflineEfficiency(l + 1) * 100:0}%, up to {Economy.OfflineCapHours(l + 1):0.#}h)");
            }
        }

        readonly HashSet<int> _managed = new HashSet<int>();
        float _managerT;

        public bool HasManagers => _managed.Count > 0;

        public void SetManagersPaused(bool paused)
        {
            if (Save.managersPaused == paused) return;
            Save.managersPaused = paused;
            Hud?.Toast(paused ? "Managers paused" : "Managers auto-buying");
            SaveNow();
        }

        void HireManager(Yard y)
        {
            if (!_managed.Add(y.Def.Index)) return;
            var m = new GameObject("Manager").transform;
            m.SetParent(y.transform, false);
            m.position = y.W(2.4f, -3.2f);
            m.rotation = Quaternion.Euler(0, 200, 0);
            var body = Visuals.Spawn("mini-characters/character-female-c", m, 1.6f, Color.white, true);
            Visuals.SetShadows(body, true);
            CharAnim.Attach(body);
            var cap = Visuals.Primitive(PrimitiveType.Sphere, m, new Vector3(0, 1.62f, 0), new Vector3(0.62f, 0.3f, 0.62f), Mats.Lit(new Color(0.98f, 0.8f, 0.2f)), "cap");
            cap.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var board = Visuals.Spawn("food-kit/cutting-board", m, 0.5f, Color.white);
            board.transform.localPosition = new Vector3(0.1f, 0.85f, 0.35f);
            board.transform.localRotation = Quaternion.Euler(-60, 0, 0);
            if (!_loading) Fx.I?.Puff(m.position + Vector3.up, 20);
        }

        /// <summary>
        /// Managers buy the cheapest affordable upgrade among their yards and your own upgrades (bigger stack, shoes...).
        /// Never gates: moving on is the player's call.
        /// </summary>
        void RunManagers()
        {
            _managerT -= Time.deltaTime;
            if (_managerT > 0 || _managed.Count == 0 || Save.managersPaused) return;
            _managerT = 0.8f;
            BuyPad best = null;
            foreach (var p in BuyPad.All)
            {
                if (p.Id.EndsWith(".gate") || !p.Shown || p.Maxed || !ManagerBuys(p.Id)) continue;
                if (best == null || p.Remaining < best.Remaining) best = p;
            }
            if (best != null && best.Remaining <= Save.cash) best.TryBuyNow();   // one purchase per tick
        }

        static string HitsText(YardDef d, int gatherLevel)
        {
            int hits = Economy.HitsToCatch(d.AnimalHp, Economy.GathererDamage(d.AnimalHp, gatherLevel));
            return hits == 1 ? "1 hit" : $"{hits} hits";
        }

        /// <summary>How hard you and your catchers hit in this yard: every catcher hired makes the whole team stronger.</summary>
        public int HitDamage(Yard y) => Economy.GathererDamage(y.Def.AnimalHp, Level($"y{y.Def.Index}.gather"));

        bool ManagerBuys(string padId)
        {
            if (padId.StartsWith("g.")) return true;   // the player's own upgrades
            foreach (int yard in _managed) if (padId.StartsWith($"y{yard}.")) return true;
            return false;
        }

        void SyncWorkers(Yard y)
        {
            string k = $"y{y.Def.Index}.";
            int crew = Level(k + "crew");
            float speed = Economy.HelperSpeed(crew);
            while (y.Def.Source == SourceKind.None && y.Couriers.Count < Level(k + "gather"))
            {
                var w = new GameObject("Courier").AddComponent<CourierWorker>();
                w.transform.SetParent(y.transform, false);
                w.Init(y, "mini-characters/character-male-e", new Color(0.98f, 0.6f, 0.15f), Economy.GathererCarry(crew), y.W(-2.6f, -1.4f));
                y.Couriers.Add(w);
                if (!_loading) Fx.I?.Puff(w.transform.position + Vector3.up, 20);
            }
            while (y.Def.Source != SourceKind.None && y.Gatherers.Count < Level(k + "gather"))
            {
                var w = new GameObject("Gatherer").AddComponent<CatcherWorker>();
                w.transform.SetParent(y.transform, false);
                w.Init(y, y.Gatherers.Count % 2 == 0 ? "mini-characters/character-male-f" : "mini-characters/character-male-d", new Color(0.95f, 0.3f, 0.3f),
                    Economy.GathererCarry(crew), y.W(-2.6f, -1.4f));
                y.Gatherers.Add(w);
                if (!_loading) Fx.I?.Puff(w.transform.position + Vector3.up, 20);
            }
            while (y.Waiters.Count < Level(k + "waiter"))
            {
                var w = new GameObject("Waiter").AddComponent<WaiterWorker>();
                w.transform.SetParent(y.transform, false);
                w.Init(y, "mini-characters/character-female-f", new Color(0.25f, 0.6f, 1f), Economy.WaiterCarry(crew), y.W(10.9f, -3.6f));
                y.Waiters.Add(w);
                if (!_loading) Fx.I?.Puff(w.transform.position + Vector3.up, 20);
            }
            int damage = HitDamage(y);
            foreach (var w in y.Gatherers) { w.Speed = speed; w.SetCapacity(Economy.GathererCarry(crew)); w.Damage = damage; }
            foreach (var w in y.Couriers) { w.Speed = speed; w.SetCapacity(Economy.GathererCarry(crew)); }
            foreach (var w in y.Waiters) { w.Speed = speed; w.SetCapacity(Economy.WaiterCarry(crew)); }
        }

        void Pad(Yard y, string id, Economy.TileSpec spec, string title, System.Func<int, double> cost, System.Func<bool> available,
                 System.Action<int> apply, System.Func<int, string> describe)
        {
            var go = new GameObject("Pad " + id);
            go.transform.SetParent(y.transform, false);
            go.transform.position = y.W(spec.Local);
            var p = go.AddComponent<BuyPad>();
            p.Id = id;
            p.Kind = spec.Kind;
            p.Title = title;
            p.MaxLevel = spec.MaxLevel;
            p.CostAt = cost;
            p.Available = available;
            p.OnBought = apply;
            p.Describe = describe;
            p.Width = spec.Width;
            p.Depth = spec.Depth;
            p.Build();
            _pads[id] = p;
        }

        public int Level(string id) => _pads.TryGetValue(id, out var p) ? p.Level : 0;
        public BuyPad PadById(string id) => _pads.TryGetValue(id, out var p) ? p : null;

        public void OnPurchase(BuyPad pad)
        {
            Hud?.Toast(pad.Maxed && pad.MaxLevel > 1 ? $"{pad.Title} maxed!" : $"{pad.Title} {(pad.MaxLevel > 1 ? $"{pad.Level}/{pad.MaxLevel}" : "")}");
            SaveNow();
        }

        /// <summary>Hover info for whatever is at world point <paramref name="p"/> (null = nothing interesting).</summary>
        public string InfoAt(Vector3 p)
        {
            foreach (var pad in BuyPad.All)
                if (pad.Shown && pad.PlayerOn(p)) return pad.Info();
            var y = YardAt(p);
            if (y == null) return null;
            foreach (var st in y.Stations)
            {
                var d = st.transform.position - p;
                d.y = 0;
                if (d.magnitude < 2.4f || st.Output.InReach(p, -0.3f)) return st.Info();
                foreach (var z in st.Inputs) if (z.InReach(p, -0.3f)) return st.Info();
            }
            if (y.InSource(p)) return y.SourceInfo(HitDamage(y));
            var c = y.Counter.transform.position - p;
            c.y = 0;
            if ((Mathf.Abs(c.x) < 3.4f && Mathf.Abs(c.z) < 1.2f) || y.Counter.Shelf.InReach(p) || y.Counter.Cash.InReach(p)) return y.Counter.Info();
            return null;
        }

        // ------------------------------------------------------------------ cash

        public void AddCash(double v, Vector3 from)
        {
            if (v <= 0) return;
            Save.cash += v;
            Save.totalEarned += v;
            Player.FlyCashFrom(from);
            Sfx.I?.Play(Sfx.I.Coin, 0.4f, Random.Range(0.95f, 1.15f));
            foreach (var p in BuyPad.All) p.Refresh();
        }

        public bool Spend(double v)
        {
            if (v <= 0 || Save.cash + 1e-9 < v) return false;
            Save.cash = System.Math.Max(0, Save.cash - v);
            return true;
        }

        /// <summary>Debug / autoshot.</summary>
        public void Grant(double v) { Save.cash += v; foreach (var p in BuyPad.All) p.Refresh(); }

        // ------------------------------------------------------------------ save / load

        void LoadState()
        {
            // yards beyond the first open as their gates were bought (in order)
            for (int i = 1; i <= YardDefs.All.Count; i++)
                if (Save.Pad($"y{i}.gate").level > 0) OpenYard(i + 1);
                else break;
            foreach (var t in Save.stack) Player.Stack.Add((ItemType)t);
        }

        void LoadPiles(GameObject root)
        {
            foreach (var z in root.GetComponentsInChildren<PileZone>())
            {
                if (string.IsNullOrEmpty(z.Id)) continue;
                var s = Save.Pile(z.Id);
                if (s.count > 0 && z.Count == 0) z.SetCount(s.count, s.cash);
            }
        }

        public void SaveNow()
        {
            _autosave = 0;
            if (TestMode) return;
            Save.px = Player.transform.position.x;
            Save.pz = Player.transform.position.z;
            Save.stack.Clear();
            foreach (var t in Player.Stack.ItemsHeld) Save.stack.Add((int)t);
            foreach (var p in _pads.Values)
            {
                var s = Save.Pad(p.Id);
                s.level = p.Level;
                s.paid = p.Paid;
            }
            foreach (var z in PileZone.All)
            {
                if (string.IsNullOrEmpty(z.Id)) continue;
                var s = Save.Pile(z.Id);
                s.count = z.Count;
                s.cash = z.CashValue;
            }
            SaveSystem.Save(Save);
        }

        void Update()
        {
            RunManagers();
            Save.playTime += Time.unscaledDeltaTime;
            _autosave += Time.unscaledDeltaTime;
            if (_autosave > 20f) SaveNow();
        }

        void OnApplicationQuit() { if (Player != null) SaveNow(); }

        public void NewGame()
        {
            FreshStart = true;
            SkipMenuOnce = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ------------------------------------------------------------------ presentation

        public void ApplyWindowMode()
        {
            if (Application.isEditor || TestMode) return;
            if (Settings.windowed)
            {
                if (Screen.fullScreenMode != FullScreenMode.Windowed) Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            }
            else
            {
                var r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
            }
        }

        void SetupLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.45f;
            sun.transform.rotation = Quaternion.Euler(55f, -25f, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.82f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.7f, 0.66f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.5f, 0.45f);
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 60f;
        }

        void OnDestroy() { if (I == this) I = null; }
    }
}
