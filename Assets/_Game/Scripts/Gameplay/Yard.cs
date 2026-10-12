using System.Collections.Generic;
using UnityEngine;

namespace StackIsland
{
    /// <summary>
    /// One walled yard in the bright tycoon style, built from a <see cref="YardDef"/> at its own spot along the
    /// island (yards sit side by side, joined by short gated walkways). Owns the yard's source (pen or field),
    /// stations, counter, helpers, and answers routing questions inside the yard.
    /// All layout numbers below are in yard-local metres (origin = yard centre).
    /// </summary>
    public class Yard : MonoBehaviour
    {
        public const float Spacing = 32f;                 // centre-to-centre distance between yards
        public const float MinX = -13f, MaxX = 13f, MinZ = -17f, MaxZ = 17f;
        public const float SrcMinX = -12.6f, SrcMaxX = -1.2f, SrcMinZ = 4.2f, SrcMaxZ = 16.6f;
        public static readonly Vector3 SrcGateLocal = new Vector3(-4.5f, 0, 4.2f);
        public const float EntryGateX = 10.2f, ExitGateX = 0.6f;
        public const float SideGateZ0 = 4.6f, SideGateZ1 = 7.8f;     // east/west walkway gap

        public YardDef Def { get; private set; }
        public Vector3 Origin { get; private set; }
        public AnimalPen Pen { get; private set; }
        public FieldPatch Field { get; private set; }
        public Counter Counter { get; private set; }
        public readonly List<Station> Stations = new List<Station>();
        public readonly List<CatcherWorker> Gatherers = new List<CatcherWorker>();
        public readonly List<WaiterWorker> Waiters = new List<WaiterWorker>();
        public readonly List<CourierWorker> Couriers = new List<CourierWorker>();
        GameObject _eastGate;

        public static Vector3 OriginOf(int index) => new Vector3((index - 1) * Spacing, 0, 0);
        public Vector3 W(Vector3 local) => Origin + local;
        public Vector3 W(float x, float z) => Origin + new Vector3(x, 0, z);
        public Vector3 SrcGate => W(SrcGateLocal);

        public bool Contains(Vector3 p) => p.x > Origin.x + MinX && p.x < Origin.x + MaxX && p.z > Origin.z + MinZ && p.z < Origin.z + MaxZ;
        public bool InSource(Vector3 p) => p.x > Origin.x + SrcMinX && p.x < Origin.x + SrcMaxX && p.z > Origin.z + SrcMinZ && p.z < Origin.z + SrcMaxZ;

        /// <summary>Waypoints from a to b (b included). Only the source area needs a detour, through its gate.</summary>
        public void Route(Vector3 a, Vector3 b, List<Vector3> pts)
        {
            pts.Clear();
            bool ia = InSource(a), ib = InSource(b);
            var cur = a;
            if (ia != ib)
            {
                var inside = SrcGate + new Vector3(0, 0, 1.2f);
                var outside = SrcGate + new Vector3(0, 0, -1.2f);
                if (ia) { pts.Add(inside); pts.Add(outside); cur = outside; }
                else { Leg(cur, outside, pts); pts.Add(inside); cur = inside; }
            }
            Leg(cur, b, pts);
        }

        /// <summary>
        /// Straight on to <paramref name="to"/>, unless that cuts across the fenced source corner from outside
        /// (e.g. gate to the tray): then round its front-right corner first.
        /// </summary>
        void Leg(Vector3 from, Vector3 to, List<Vector3> pts)
        {
            if (Def.Source != SourceKind.None && !InSource(from) && !InSource(to) && CutsSource(from, to))
                pts.Add(W(SrcMaxX + 0.8f, SrcMinZ - 0.8f));
            pts.Add(to);
        }

        bool CutsSource(Vector3 from, Vector3 to)
        {
            const float margin = 0.5f;
            for (int i = 1; i < 24; i++)
            {
                var p = transform.InverseTransformPoint(Vector3.Lerp(from, to, i / 24f));
                if (p.x > SrcMinX - margin && p.x < SrcMaxX + margin && p.z > SrcMinZ - margin && p.z < SrcMaxZ + margin) return true;
            }
            return false;
        }

        public void Build(YardDef def)
        {
            Def = def;
            Origin = OriginOf(def.Index);
            transform.position = Origin;
            BuildGround();
            BuildWalls();
            BuildSource();
            Counter = new GameObject("Counter").AddComponent<Counter>();
            Counter.transform.SetParent(transform, false);
            Counter.transform.position = W(4.5f, -10f);
            Counter.Build(this, $"y{def.Index}.counter", def.Product);
            AddStation();
            // name painted on the floor near the entrance
            ProcArt.FloorText(transform, def.Name.ToUpperInvariant(), 9f, new Color(1f, 1f, 1f, 0.55f), new Vector3(-6.5f, 0.06f, -15.2f));
        }

        // ------------------------------------------------------------------ pieces

        public Station AddStation()
        {
            if (Stations.Count >= 2) return null;
            var slot = Stations.Count == 0 ? W(6, 11) : W(6, 3);
            var s = new GameObject("Station " + Stations.Count).AddComponent<Station>();
            s.transform.SetParent(transform, false);
            s.transform.position = slot;
            s.Build($"y{Def.Index}.station{Stations.Count + 1}", Def);
            Stations.Add(s);
            return s;
        }

        public PileZone BestStationInput(ItemType t)
        {
            PileZone best = null;
            foreach (var s in Stations)
                foreach (var z in s.Inputs)
                    if (z.Type == t && z.Free > 0 && (best == null || z.Free > best.Free)) best = z;
            return best;
        }

        public PileZone BestStationOutput()
        {
            PileZone best = null;
            foreach (var s in Stations)
                if (s.Output.Count > 0 && (best == null || s.Output.Count > best.Count)) best = s.Output;
            return best;
        }

        void BuildSource()
        {
            var root = new GameObject("Source").transform;
            root.SetParent(transform, false);
            if (Def.Source == SourceKind.Pen)
            {
                Pen = root.gameObject.AddComponent<AnimalPen>();
                Pen.Init(this);
            }
            else if (Def.Source == SourceKind.Field)
            {
                Field = root.gameObject.AddComponent<FieldPatch>();
                Field.Init(this);
            }
            else BuildVolcano(root);
        }

        /// <summary>No pen or field here: a smoking volcano fills the corner instead.</summary>
        void BuildVolcano(Transform root)
        {
            var c = W((SrcMinX + SrcMaxX) * 0.5f, (SrcMinZ + SrcMaxZ) * 0.5f + 1f);
            var rock = new Color(0.36f, 0.3f, 0.32f);
            for (int i = 0; i < 6; i++)
            {
                float r = 9f - i * 1.3f;
                var ring = Visuals.Primitive(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(r, 0.55f, r), Mats.Lit(Color.Lerp(rock, new Color(0.5f, 0.4f, 0.4f), i / 6f)), "slope");
                ring.transform.position = c + Vector3.up * (0.55f + i * 1.0f);
                ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            var lava = Visuals.Primitive(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(1.8f, 0.1f, 1.8f), Mats.Glow(new Color(1f, 0.45f, 0.1f), 2.2f), "lava");
            lava.transform.position = c + Vector3.up * 6.0f;
            for (int i = 0; i < 4; i++)
            {
                var flow = Visuals.Primitive(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.5f, 0.08f, 4.5f), Mats.Glow(new Color(1f, 0.5f, 0.12f), 1.6f), "lavaFlow");
                flow.transform.position = c + Vector3.up * 3.6f;
                flow.transform.rotation = Quaternion.Euler(-42f, i * 90f + 30f, 0);
            }
            var top = new GameObject("smokeTop").transform;
            top.SetParent(root, false);
            top.position = c + Vector3.up * 6.2f;
            var smoke = Fx.Smoke(top);
            var em = smoke.emission;
            em.rateOverTime = 4f;
            var main = smoke.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            Blocker(c, new Vector3(10f, 4f, 10f), Quaternion.identity);
        }

        void BuildGround()
        {
            ProcArt.MeshObject("floor", transform, ProcArt.RoundedSlab(MaxX - MinX, MaxZ - MinZ, 0.6f, 0.05f), Mats.Lit(Def.Floor), Vector3.zero).GetComponent<Renderer>().receiveShadows = true;
            var rng = new System.Random(3 + Def.Index * 17);
            var tile = Mats.Lit(Def.Tile);
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector3(Mathf.Round(Mathf.Lerp(MinX + 1, MaxX - 1, (float)rng.NextDouble()) / 0.7f) * 0.7f, 0.055f,
                                    Mathf.Round(Mathf.Lerp(MinZ + 1, MaxZ - 1, (float)rng.NextDouble()) / 0.7f) * 0.7f);
                if (InSource(Origin + p)) continue;
                ProcArt.MeshObject("tile", transform, ProcArt.RoundedRect(0.62f, 0.62f, 0.08f), tile, p).GetComponent<Renderer>().receiveShadows = true;
                if (rng.NextDouble() < 0.5)
                    ProcArt.MeshObject("tile", transform, ProcArt.RoundedRect(0.62f, 0.62f, 0.08f), tile, p + new Vector3(0.7f, 0, rng.NextDouble() < 0.5 ? 0.7f : 0)).GetComponent<Renderer>().receiveShadows = true;
            }
            if (Def.Source != SourceKind.None)
            {
                var src = ProcArt.MeshObject("sourceFloor", transform, ProcArt.RoundedSlab(SrcMaxX - SrcMinX, SrcMaxZ - SrcMinZ, 0.5f, 0.07f), Mats.Lit(Def.SourceFloor),
                    new Vector3((SrcMinX + SrcMaxX) * 0.5f, 0, (SrcMinZ + SrcMaxZ) * 0.5f));
                src.GetComponent<Renderer>().receiveShadows = true;
            }
        }

        void BuildWalls()
        {
            bool first = Def.Index == 1;
            bool hasNext = YardDefs.Get(Def.Index + 1) != null;
            // west wall (gap to the previous yard's walkway)
            if (first) Wall(new Vector3(MinX, 0, MinZ), new Vector3(MinX, 0, MaxZ));
            else
            {
                Wall(new Vector3(MinX, 0, MinZ), new Vector3(MinX, 0, SideGateZ0));
                Wall(new Vector3(MinX, 0, SideGateZ1), new Vector3(MinX, 0, MaxZ));
            }
            // east wall (gap to the next yard, closed by a cage gate until bought)
            if (!hasNext) Wall(new Vector3(MaxX, 0, MinZ), new Vector3(MaxX, 0, MaxZ));
            else
            {
                Wall(new Vector3(MaxX, 0, MinZ), new Vector3(MaxX, 0, SideGateZ0));
                Wall(new Vector3(MaxX, 0, SideGateZ1), new Vector3(MaxX, 0, MaxZ));
                _eastGate = new GameObject("eastGate");
                _eastGate.transform.SetParent(transform, false);
                ProcArt.CageFence(_eastGate.transform, W(MaxX, SideGateZ0), W(MaxX, SideGateZ1), 1.3f, Def.Cage);
                var col = new GameObject("gateCollider");
                col.transform.SetParent(_eastGate.transform, false);
                col.transform.position = W(MaxX, (SideGateZ0 + SideGateZ1) * 0.5f) + Vector3.up * 1.5f;
                col.AddComponent<BoxCollider>().size = new Vector3(0.8f, 3f, SideGateZ1 - SideGateZ0);
            }
            Wall(new Vector3(MinX, 0, MaxZ), new Vector3(MaxX, 0, MaxZ));
            Wall(new Vector3(MinX, 0, MinZ), new Vector3(ExitGateX - 1.4f, 0, MinZ));
            Wall(new Vector3(ExitGateX + 1.4f, 0, MinZ), new Vector3(EntryGateX - 1.4f, 0, MinZ));
            Wall(new Vector3(EntryGateX + 1.4f, 0, MinZ), new Vector3(MaxX, 0, MinZ));

            if (Def.Source != SourceKind.None)
            {
                Cage(new Vector3(SrcMaxX, 0, SrcMinZ), new Vector3(SrcMaxX, 0, MaxZ - 0.4f));
                Cage(new Vector3(MinX + 0.4f, 0, SrcMinZ), new Vector3(SrcGateLocal.x - 1.3f, 0, SrcMinZ));
                Cage(new Vector3(SrcGateLocal.x + 1.3f, 0, SrcMinZ), new Vector3(SrcMaxX, 0, SrcMinZ));
            }
        }

        /// <summary>Open the walkway to the next yard (after its gate is bought).</summary>
        public void OpenEastGate()
        {
            if (_eastGate == null) return;
            Fx.I?.Puff(W(MaxX, (SideGateZ0 + SideGateZ1) * 0.5f) + Vector3.up, 40);
            Destroy(_eastGate);
            _eastGate = null;
        }

        /// <summary>A straight run of thick low wall (yard-local ends) with one collider.</summary>
        void Wall(Vector3 a, Vector3 b)
        {
            if ((b - a).magnitude < 0.1f) return;
            var wa = Origin + a;
            var wb = Origin + b;
            ProcArt.LowWall(transform, wa, wb, 0.9f, 0.7f, Def.Wall);
            Blocker((wa + wb) * 0.5f, new Vector3(0.7f, 3f, (wb - wa).magnitude), Quaternion.LookRotation((wb - wa).normalized));
        }

        void Cage(Vector3 a, Vector3 b)
        {
            if ((b - a).magnitude < 0.1f) return;
            var wa = Origin + a;
            var wb = Origin + b;
            ProcArt.CageFence(transform, wa, wb, 1.6f, Def.Cage);
            Blocker((wa + wb) * 0.5f, new Vector3(0.3f, 3f, (wb - wa).magnitude), Quaternion.LookRotation((wb - wa).normalized));
        }

        void Blocker(Vector3 center, Vector3 size, Quaternion rot)
        {
            var go = new GameObject("wallCollider");
            go.transform.SetParent(transform, false);
            go.transform.position = center + Vector3.up * size.y * 0.5f;
            go.transform.rotation = rot;
            go.AddComponent<BoxCollider>().size = size;
        }

        public string SourceInfo(int damage)
        {
            if (Pen != null) return Pen.Info(damage);
            if (Field != null) return Field.Info();
            return null;
        }
    }

    /// <summary>
    /// The island around the yards: sea, sand, a grass band, round trees and palms, the walkways between yards
    /// and the paths out of each yard's customer gates.
    /// </summary>
    public static class IslandBuilder
    {
        public static void Build(Transform parent, int yards)
        {
            float x0 = Yard.OriginOf(1).x, x1 = Yard.OriginOf(yards).x;
            float cx = (x0 + x1) * 0.5f, width = x1 - x0;
            var sea = Visuals.Primitive(PrimitiveType.Plane, parent, new Vector3(cx, -0.35f, 0), new Vector3(width / 10f + 40, 1, 40), Mats.Lit(new Color(0.2f, 0.62f, 0.9f)), "sea");
            sea.GetComponent<Renderer>().receiveShadows = false;
            ProcArt.MeshObject("sand", parent, ProcArt.RoundedSlab(width + 84, 90, 24, 0.3f), Mats.Lit(new Color(0.98f, 0.88f, 0.62f)), new Vector3(cx, -0.32f, 2));
            ProcArt.MeshObject("grass", parent, ProcArt.RoundedSlab(width + 50, 58, 12, 0.3f), Mats.Lit(new Color(0.42f, 0.82f, 0.3f)), new Vector3(cx, -0.3f, 0))
                .GetComponent<Renderer>().receiveShadows = true;
            var col = new GameObject("floorCollider").AddComponent<BoxCollider>();
            col.transform.SetParent(parent, false);
            col.size = new Vector3(width + 200, 1, 200);
            col.center = new Vector3(cx, -0.5f, 0);

            // trees around everything (not on yards, walkways or paths)
            var rng = new System.Random(11);
            Color[] greens = { new Color(0.36f, 0.78f, 0.25f), new Color(0.3f, 0.7f, 0.22f), new Color(0.48f, 0.85f, 0.3f) };
            string[] palms = { "nature-kit/tree_palmTall", "nature-kit/tree_palmBend", "pirate-kit/palm-detailed-straight", "nature-kit/tree_palmDetailedTall" };
            int target = 60 + 40 * yards, placed = 0, guard = 0;
            while (placed < target && guard++ < target * 30)
            {
                float x = Mathf.Lerp(x0 - 26, x1 + 26, (float)rng.NextDouble());
                float z = Mathf.Lerp(-30, 30, (float)rng.NextDouble());
                if (Blocked(x, z, yards)) continue;
                bool inGrass = x > x0 - 23 && x < x1 + 23 && Mathf.Abs(z) < 27;
                if (inGrass)
                    ProcArt.BlobTree(parent, new Vector3(x, 0, z), 2.6f + (float)rng.NextDouble() * 1.8f, greens[rng.Next(greens.Length)]);
                else
                {
                    var tree = Visuals.Spawn(palms[rng.Next(palms.Length)], parent, 4f + (float)rng.NextDouble() * 2f, Color.white, true);
                    tree.transform.position = new Vector3(x, -0.05f, z);
                    tree.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    Visuals.SetShadows(tree, true);
                }
                placed++;
            }
        }

        /// <summary>
        /// The walkway in from the previous yard and the paths out of this yard's customer gates. Built when the yard
        /// opens, so no paths lead off into empty grass where later yards will stand.
        /// </summary>
        public static void YardLinks(Transform parent, int index)
        {
            if (index > 1)
            {
                var a = Yard.OriginOf(index - 1);
                float zc = (Yard.SideGateZ0 + Yard.SideGateZ1) * 0.5f;
                float xa = a.x + Yard.MaxX, xb = a.x + Yard.Spacing + Yard.MinX;
                ProcArt.MeshObject("walkway", parent, ProcArt.RoundedSlab(xb - xa + 1.4f, Yard.SideGateZ1 - Yard.SideGateZ0 + 0.6f, 0.4f, 0.05f),
                    Mats.Lit(new Color(0.96f, 0.8f, 0.62f)), new Vector3((xa + xb) * 0.5f, 0, zc)).GetComponent<Renderer>().receiveShadows = true;
                foreach (float z in new[] { Yard.SideGateZ0 - 0.2f, Yard.SideGateZ1 + 0.2f })
                {
                    ProcArt.LowWall(parent, new Vector3(xa, 0, z), new Vector3(xb, 0, z), 0.6f, 0.4f);
                    var b = new GameObject("walkwayCollider");
                    b.transform.SetParent(parent, false);
                    b.transform.position = new Vector3((xa + xb) * 0.5f, 1.5f, z);
                    b.AddComponent<BoxCollider>().size = new Vector3(xb - xa, 3f, 0.4f);
                }
            }
            var o = Yard.OriginOf(index);
            foreach (float gx in new[] { Yard.EntryGateX, Yard.ExitGateX })
                ProcArt.MeshObject("path", parent, ProcArt.RoundedSlab(2.6f, 12f, 1f, 0.05f), Mats.Lit(new Color(0.96f, 0.78f, 0.6f)), o + new Vector3(gx, -0.26f, Yard.MinZ - 6f))
                    .GetComponent<Renderer>().receiveShadows = true;
        }

        static bool Blocked(float x, float z, int yards)
        {
            for (int i = 1; i <= yards; i++)
            {
                var o = Yard.OriginOf(i);
                float lx = x - o.x;
                if (lx > Yard.MinX - 2.4f && lx < Yard.MaxX + 2.4f && z > Yard.MinZ - 2.4f && z < Yard.MaxZ + 2.4f) return true;
                if (z < Yard.MinZ && (Mathf.Abs(lx - Yard.EntryGateX) < 2.6f || Mathf.Abs(lx - Yard.ExitGateX) < 2.6f)) return true;
                if (i < yards && lx > Yard.MaxX && lx < Yard.Spacing + Yard.MinX && z > Yard.SideGateZ0 - 3f && z < Yard.SideGateZ1 + 3f) return true;
            }
            return false;
        }
    }
}
