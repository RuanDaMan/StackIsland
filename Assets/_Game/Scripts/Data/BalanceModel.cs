using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StackIsland
{
    /// <summary>
    /// Plays the whole game in a few milliseconds with a "thoughtful player": a flow model of each yard (supply,
    /// station, helpers, customers) built from the same <see cref="Economy"/> numbers the game uses, and a greedy
    /// buyer that always picks the tile with the best payback. Prints when each yard opens and when everything is
    /// maxed, how long each yard takes and the slowest waits, so pacing can be tuned to even yards with no stalls.
    /// Pure C# (no Unity types) so it also runs outside the engine; the editor menu / batch entry is BalanceSim.
    /// </summary>
    public static class BalanceModel
    {
        const double PurchaseOverhead = 4;     // seconds walking to a tile and standing on it
        const double ManualEfficiency = 0.65;  // the player isn't a robot

        class State
        {
            public readonly Dictionary<string, int> Lv = new Dictionary<string, int>();
            public int Open = 1;
            public double Cash, Earned, Time;
            public int L(string id) => Lv.TryGetValue(id, out var v) ? v : 0;
            public State Clone()
            {
                var s = new State { Open = Open, Cash = Cash, Earned = Earned, Time = Time };
                foreach (var kv in Lv) s.Lv[kv.Key] = kv.Value;
                return s;
            }
        }

        // ------------------------------------------------------------------ flow model

        struct Flow
        {
            public int Gatherers, Waiters;
            public double Supply, Station, Waiting, Customers;
        }

        /// <summary>What each link of a yard's chain can move per second (items/s).</summary>
        static Flow Flows(State s, YardDef d)
        {
            string k = $"y{d.Index}.";
            int crew = s.L(k + "crew");
            double hv = Economy.HelperSpeed(crew);
            var f = new Flow { Gatherers = s.L(k + "gather"), Waiters = s.L(k + "waiter") };
            f.Station = (1 + s.L(k + "station2")) / Economy.CookSeconds(d, s.L(k + "speed"));
            int busy = s.L(k + "busy");
            f.Customers = Math.Min(Economy.AvgWant(busy) / Economy.SpawnEvery(busy), 1 / Economy.ServeSeconds);

            int gcap = Economy.GathererCarry(crew);
            int src = s.L(k + "src");
            double gatherTime, sourceCap;
            if (d.Source == SourceKind.Pen)
            {
                int hits = Economy.HitsToCatch(d.AnimalHp, Economy.GathererDamage(d.AnimalHp, f.Gatherers));
                gatherTime = gcap / (double)d.RawPerAnimal * (hits * 0.5 + 1.5) + gcap * 0.15;
                sourceCap = Economy.PenAnimals(src) * d.RawPerAnimal / (Economy.PenRespawn(src) + hits * 0.5);
            }
            else
            {
                gatherTime = gcap / 2.0 * 0.12 + 2.5;
                sourceCap = Economy.FieldRegrow(src);
            }
            f.Supply = Math.Min(f.Gatherers * gcap / (gatherTime + 27 / hv + gcap * 0.07), sourceCap);
            if (d.Source == SourceKind.None) f.Supply = CourierSupply(s, d, f.Gatherers, gcap, hv);
            int wcap = Economy.WaiterCarry(crew);
            f.Waiting = f.Waiters * wcap / (33 / hv + wcap * 0.14);
            return f;
        }

        /// <summary>Complete sets of inputs per second couriers bring to a yard with no source of its own.</summary>
        static double CourierSupply(State s, YardDef d, int couriers, int cap, double speed)
        {
            if (couriers == 0) return 0;
            var inputs = d.StationInputs;
            double share = couriers / (double)inputs.Length, sets = double.MaxValue;
            foreach (var t in inputs)
            {
                var from = YardDefs.All.Find(y => y.Product == t);
                if (from == null || from.Index > s.Open) return 0;
                sets = Math.Min(sets, share * cap / (Economy.CourierTrip(d.Index, from.Index) / speed + cap * 0.14));
            }
            return sets;
        }

        /// <summary>Seconds per item for the player fetching one input type from its yard (average over inputs).</summary>
        static double FetchTime(State s, YardDef d, double ws)
        {
            double total = 0;
            foreach (var t in d.StationInputs)
            {
                var from = YardDefs.All.Find(y => y.Product == t);
                total += from == null ? 999 : Economy.CourierTrip(d.Index, from.Index) / ws;
            }
            return total / d.StationInputs.Length;
        }

        /// <summary>Items per second a yard sells. The player works the newest yard and fills whichever links have no helper.</summary>
        static double SellRate(State s, YardDef d, bool playerHere)
        {
            var f = Flows(s, d);
            double auto = f.Gatherers > 0 && f.Waiters > 0 ? Math.Min(Math.Min(f.Supply, f.Station), Math.Min(f.Waiting, f.Customers)) : 0;
            if (!playerHere) return auto;

            int cap = Economy.Backpack(s.L("g.backpack"));
            double ws = Economy.WalkSpeed(s.L("g.shoes"));
            double gather = d.Source == SourceKind.Pen ? cap / (double)d.RawPerAnimal * (d.AnimalHp * 0.36 + 1.2)
                          : d.Source == SourceKind.Field ? cap / 2.0 * 0.05 + 3
                          : FetchTime(s, d, ws) * d.StationInputs.Length;   // carrying dishes over from other yards
            double hand = cap * 0.055;
            double rate;
            if (f.Gatherers == 0 && f.Waiters == 0)
                rate = Math.Min(cap / (gather + 60 / ws + hand * 3), f.Station);                 // everything by hand
            else if (f.Waiters == 0)
                rate = Math.Min(Math.Min(f.Supply, f.Station), cap / (35 / ws + hand * 2));        // helpers gather, you serve
            else if (f.Gatherers == 0)
                rate = Math.Min(Math.Min(cap / (gather + 30 / ws + hand * 2), f.Station), f.Waiting); // you gather, helpers serve
            else
                rate = Math.Min(auto + 0.5 * cap / (gather + 60 / ws + hand * 3), f.Station);    // fully automated, you help out
            return Math.Min(rate * ManualEfficiency + (f.Gatherers > 0 && f.Waiters > 0 ? auto * (1 - ManualEfficiency) : 0), f.Customers);
        }

        /// <summary>
        /// Coins per second the yards earn with helpers alone (no player): what offline earnings pay out.
        /// <paramref name="level"/> looks up a tile level by id ("y2.gather", "g.backpack"...).
        /// </summary>
        public static double AutomatedIncome(Func<string, int> level, int openYards)
        {
            var s = new State { Open = openYards };
            foreach (var d in YardDefs.All)
                foreach (var spec in Economy.YardTiles) s.Lv[$"y{d.Index}.{spec.Key}"] = level($"y{d.Index}.{spec.Key}");
            double total = 0;
            for (int i = 1; i <= openYards; i++)
            {
                var d = YardDefs.Get(i);
                total += SellRate(s, d, false) * ItemInfo.Price(d.Product) * Economy.PriceMult(s.L($"y{i}.recipe"));
            }
            return total;
        }

        static double Income(State s)
        {
            double total = 0;
            for (int i = 1; i <= s.Open; i++)
            {
                var d = YardDefs.Get(i);
                total += SellRate(s, d, i == s.Open) * ItemInfo.Price(d.Product) * Economy.PriceMult(s.L($"y{i}.recipe"));
            }
            return total;
        }

        // ------------------------------------------------------------------ buying

        class Option
        {
            public string Id;
            public double Cost;
            public bool IsGate;
        }

        static IEnumerable<Option> Options(State s)
        {
            foreach (var spec in Economy.GlobalTiles)
            {
                string id = "g." + spec.Key;
                int l = s.L(id);
                if (l < spec.MaxLevel && Economy.GlobalAvailable(spec.Key, s.Earned)) yield return new Option { Id = id, Cost = spec.Cost(l, 1) };
            }
            for (int i = 1; i <= s.Open; i++)
            {
                var d = YardDefs.Get(i);
                string k = $"y{i}.";
                int L(string key) => s.L(k + key);
                foreach (var spec in Economy.YardTiles)
                {
                    int l = L(spec.Key);
                    if (l < spec.MaxLevel && Economy.TileApplies(d, spec.Key) && Economy.Available(spec.Key, L, s.Earned, d.Scale)) yield return new Option { Id = k + spec.Key, Cost = spec.Cost(l, d.Scale) };
                }
                var next = YardDefs.Get(i + 1);
                if (next != null && L("gate") == 0 && Economy.Available("gate", L, s.Earned, d.Scale))
                    yield return new Option { Id = k + "gate", Cost = next.GateCost, IsGate = true };
            }
        }

        static void Apply(State s, Option o)
        {
            s.Lv[o.Id] = s.L(o.Id) + 1;
            if (o.IsGate) s.Open++;
        }

        /// <summary>Hours until everything is maxed in the last <see cref="Run"/>.</summary>
        public static double LastTotalHours { get; private set; }
        /// <summary>Longest single wait for one purchase in the last run, in minutes (the "game grinds to a halt" check).</summary>
        public static double LastLongestWaitMinutes { get; private set; }
        /// <summary>Hours each yard was the newest one (index 0 = yard 1), the last entry being the time after the final yard opens.</summary>
        public static List<double> LastYardHours { get; } = new List<double>();

        public static string Run()
        {
            var s = new State();
            var log = new StringBuilder();
            var gateTimes = new List<(int yard, double h)>();
            int purchases = 0;
            int totalLevels = Economy.GlobalTiles.Sum(t => t.MaxLevel) + YardDefs.All.Count * Economy.YardTiles.Sum(t => t.MaxLevel) + (YardDefs.All.Count - 1);
            double lastReport = 0, segStart = 0, segLongest = 0;
            string segLongestId = "";
            var waits = new List<(double sec, double at, string id)>();
            LastYardHours.Clear();
            while (purchases < 5000)
            {
                var opts = Options(s).ToList();
                if (opts.Count == 0) break;
                double inc = Math.Max(Income(s), 1e-6);
                Option best = null;
                double bestScore = double.NegativeInfinity;
                foreach (var o in opts)
                {
                    var t = s.Clone();
                    Apply(t, o);
                    double gain = Income(t) - inc;
                    // payback: time to afford it plus time to earn it back
                    double wait = Math.Max(0, o.Cost - s.Cash) / inc;
                    double payback = gain > 0 ? o.Cost / gain : double.PositiveInfinity;
                    double score = -(wait + payback);
                    if (gain <= 0)
                    {
                        // a stepping stone (e.g. a hire that only pays off with the next one): judge it together with
                        // the best purchase it enables
                        double bestPair = double.NegativeInfinity;
                        foreach (var o2 in Options(t))
                        {
                            var t2 = t.Clone();
                            Apply(t2, o2);
                            double g2 = Income(t2) - inc;
                            if (g2 <= 0) continue;
                            double c2 = o.Cost + o2.Cost;
                            double pair = -(Math.Max(0, c2 - s.Cash) / inc + c2 / g2);
                            if (pair > bestPair) bestPair = pair;
                        }
                        score = bestPair > double.NegativeInfinity ? bestPair : -1e12 - o.Cost;   // nothing better: just the cheapest one
                    }
                    if (score > bestScore) { bestScore = score; best = o; }
                }
                double need = Math.Max(0, best.Cost - s.Cash);
                double dt = need / inc + PurchaseOverhead;
                waits.Add((dt, s.Time, best.Id));
                if (dt > segLongest) { segLongest = dt; segLongestId = best.Id; }
                s.Time += dt;
                s.Cash += inc * dt - best.Cost;
                s.Earned += inc * dt;
                Apply(s, best);
                purchases++;
                if (best.IsGate)
                {
                    gateTimes.Add((s.Open, s.Time / 3600));
                    log.AppendLine($"[BalanceSim] yard {s.Open} ({YardDefs.Get(s.Open).Name}) opens at {s.Time / 3600:0.0}h  (+{(s.Time - segStart) / 3600:0.0}h, gate saved {dt / 60:0}m, longest wait {segLongest / 60:0}m for {segLongestId})  income {NumberFormatter.Format(Income(s))}/s");
                    LastYardHours.Add((s.Time - segStart) / 3600);
                    segStart = s.Time;
                    segLongest = 0;
                }
                if (s.Time - lastReport > 3600 * 5)
                {
                    lastReport = s.Time;
                    log.AppendLine($"[BalanceSim]   {s.Time / 3600:0.0}h: {purchases}/{totalLevels} levels bought, income {NumberFormatter.Format(Income(s))}/s");
                }
            }
            LastTotalHours = s.Time / 3600;
            LastYardHours.Add((s.Time - segStart) / 3600);
            LastLongestWaitMinutes = waits.Count > 0 ? waits.Max(w => w.sec) / 60 : 0;
            log.AppendLine($"[BalanceSim] after the last yard: +{(s.Time - segStart) / 3600:0.0}h, longest wait {segLongest / 60:0}m for {segLongestId}");
            if (Environment.GetEnvironmentVariable("SIM_TAIL") != null)
                foreach (var w in waits.Skip(waits.Count - 60)) log.AppendLine($"[BalanceSim]   tail: {w.sec / 60:0.0}m at {w.at / 3600:0.0}h {w.id}");
            foreach (var w in waits.OrderByDescending(w => w.sec).Take(8))
                log.AppendLine($"[BalanceSim]   slow: {w.sec / 60:0}m at {w.at / 3600:0.0}h for {w.id}");
            log.AppendLine($"[BalanceSim] everything maxed after {s.Time / 3600:0.0}h ({purchases} purchases, final income {NumberFormatter.Format(Income(s))}/s)");
            return log.ToString();
        }
    }
}
