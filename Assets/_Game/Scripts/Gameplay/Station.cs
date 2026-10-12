using System.Collections.Generic;
using UnityEngine;

namespace StackIsland
{
    /// <summary>
    /// A cooking station (grill, juice press, sushi bar, blender, fryer, ice cream machine...): drop the raw items
    /// on the left tray(s), the product piles up on the right tray. Works by itself whenever it has what it needs
    /// and room for the result - nothing is ever lost.
    /// </summary>
    public class Station : MonoBehaviour
    {
        public string Id;
        public YardDef Def;
        public readonly List<PileZone> Inputs = new List<PileZone>();
        public PileZone Output;
        public float CookSeconds = 1.1f;
        float _t;
        Transform _mouth, _chimney;
        ParticleSystem _smoke;
        GameObject _active;

        public bool Working { get; private set; }

        public void Build(string id, YardDef def)
        {
            Id = id;
            Def = def;
            CookSeconds = def.CookSeconds;
            StationArt.Build(def.StationLook, transform, out _mouth, out _chimney, out _active);
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.1f, 0);
            col.size = new Vector3(3.2f, 2.2f, 2.6f);

            var inputs = def.StationInputs;
            for (int i = 0; i < inputs.Length; i++)
                Inputs.Add(MakeTray("in" + i, new Vector3(-2.9f, 0, -2.6f + i * 2.4f), inputs[i], true, false, new Color(0.96f, 0.55f, 0.5f)));
            Output = MakeTray("out", new Vector3(2.9f, 0, -2.6f), def.Product, false, true, new Color(1f, 0.82f, 0.45f));
            if (_chimney != null) _smoke = Fx.Smoke(_chimney);
        }

        PileZone MakeTray(string name, Vector3 local, ItemType type, bool drop, bool take, Color rim)
        {
            var go = new GameObject("tray_" + name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            ProcArt.Tray(go.transform, 1.9f, 1.6f, rim);
            var disp = new GameObject("display").transform;
            disp.SetParent(go.transform, false);
            disp.localPosition = new Vector3(0, 0.14f, 0);
            var z = go.AddComponent<PileZone>();
            z.Id = Id + ":" + name;
            z.Type = type;
            // drop-off trays take everything you bring (only 40 are drawn); output trays fill up and pause the station
            z.Capacity = drop && !take ? 99999 : 40;
            z.MaxShown = 40;
            z.Drop = drop;
            z.Take = take;
            z.Radius = 1.5f;
            z.Display = disp;
            z.Columns = 3;
            z.Rows = 2;
            z.Spacing = new Vector2(0.55f, 0.5f);
            return z;
        }

        bool HasInputs()
        {
            foreach (var z in Inputs) if (z.Count <= 0) return false;
            return true;
        }

        public string Info()
        {
            string need = "";
            foreach (var z in Inputs) if (z.Count == 0) need += (need.Length > 0 ? ", " : "") + Items.Name(z.Type);
            string status = Working ? "<color=#9CFF9A>Working</color>" : Output.IsFull ? "<color=#FF9A8A>Full - take the output</color>" : $"<color=#FFD27A>Needs {need}</color>";
            var sb = new System.Text.StringBuilder();
            sb.Append($"<b>{Def.StationName}</b>  {status}\n");
            sb.Append($"Makes 1 {Items.Name(Output.Type)} every {CookSeconds:0.00}s ({60f / CookSeconds:0} per minute)\n");
            foreach (var z in Inputs) sb.Append($"{Items.Name(z.Type)} waiting: {z.Count}\n");
            sb.Append($"Ready to take: {Output.Count}/{Output.Capacity}");
            return sb.ToString();
        }

        void Update()
        {
            Working = HasInputs() && !Output.IsFull;
            if (_smoke != null)
            {
                var em = _smoke.emission;
                em.rateOverTime = Working ? 7f : 0.6f;
            }
            if (_active != null) _active.SetActive(Working);
            if (!Working) { _t = 0; return; }
            _t += Time.deltaTime;
            if (_t < CookSeconds) return;
            _t = 0;
            foreach (var z in Inputs) z.Remove(out _, out _);
            Output.Add(_mouth != null ? _mouth.position : transform.position + Vector3.up);
        }
    }

    /// <summary>Station models drawn in code (plus a few Kenney props), one per look.</summary>
    public static class StationArt
    {
        static GameObject P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, bool shadows = true)
        {
            var go = Visuals.Primitive(t, parent, pos, scale, Mats.Lit(c), "part");
            if (shadows) go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        public static void Build(string look, Transform parent, out Transform mouth, out Transform chimney, out GameObject active)
        {
            chimney = null;
            active = null;
            switch (look)
            {
                case "press": Press(parent, out mouth, out active); return;
                case "sushi": Sushi(parent, out mouth, out active); return;
                case "blender": Blender(parent, out mouth, out active); return;
                case "fryer": Fryer(parent, out mouth, out chimney, out active); return;
                case "freezer": Freezer(parent, out mouth, out active); return;
                case "feast": Feast(parent, out mouth, out active); return;
            }
            var oven = ProcArt.DomeOven(parent, out mouth, out chimney);
            var fire = oven.transform.Find("fire");
            active = fire != null ? fire.gameObject : null;
        }

        static Transform Mouth(Transform parent, Vector3 pos)
        {
            var m = new GameObject("mouth").transform;
            m.SetParent(parent, false);
            m.localPosition = pos;
            return m;
        }

        /// <summary>Big wooden barrel with a screw press on top.</summary>
        static void Press(Transform parent, out Transform mouth, out GameObject active)
        {
            var wood = new Color(0.62f, 0.4f, 0.24f);
            P(PrimitiveType.Cube, parent, new Vector3(0, 0.3f, 0), new Vector3(3f, 0.6f, 2.4f), new Color(0.5f, 0.32f, 0.2f));
            P(PrimitiveType.Cylinder, parent, new Vector3(0, 1.25f, 0), new Vector3(1.9f, 0.7f, 1.9f), wood);
            foreach (float y in new[] { 0.75f, 1.75f }) P(PrimitiveType.Cylinder, parent, new Vector3(0, y, 0), new Vector3(1.96f, 0.06f, 1.96f), new Color(0.35f, 0.35f, 0.38f), false);
            P(PrimitiveType.Cylinder, parent, new Vector3(0, 2.4f, 0), new Vector3(0.22f, 0.6f, 0.22f), new Color(0.45f, 0.45f, 0.5f));
            var handle = P(PrimitiveType.Cube, parent, new Vector3(0, 2.95f, 0), new Vector3(1.6f, 0.14f, 0.14f), new Color(0.75f, 0.2f, 0.2f));
            active = new GameObject("spin");
            active.transform.SetParent(parent, false);
            active.AddComponent<Spinner>().Target = handle.transform;
            P(PrimitiveType.Cube, parent, new Vector3(0, 0.7f, -1.15f), new Vector3(0.5f, 0.15f, 0.5f), new Color(0.98f, 0.95f, 0.85f), false);
            mouth = Mouth(parent, new Vector3(0, 0.9f, -1.2f));
        }

        /// <summary>Sushi counter with a cutting board and a knife.</summary>
        static void Sushi(Transform parent, out Transform mouth, out GameObject active)
        {
            P(PrimitiveType.Cube, parent, new Vector3(0, 0.5f, 0), new Vector3(3.2f, 1f, 2.2f), new Color(0.78f, 0.55f, 0.35f));
            P(PrimitiveType.Cube, parent, new Vector3(0, 1.04f, 0), new Vector3(3.4f, 0.08f, 2.4f), new Color(0.95f, 0.92f, 0.85f));
            var board = Visuals.Spawn("food-kit/cutting-board-japanese", parent, 1.6f, Color.white);
            board.transform.localPosition = new Vector3(0, 1.08f, 0);
            var knife = Visuals.Spawn("food-kit/cooking-knife-chopping", parent, 0.9f, Color.white);
            knife.transform.localPosition = new Vector3(0.6f, 1.12f, 0.2f);
            // lantern-red noren curtain on a frame
            P(PrimitiveType.Cube, parent, new Vector3(0, 2.4f, 1.0f), new Vector3(3.4f, 0.12f, 0.12f), new Color(0.35f, 0.22f, 0.15f));
            for (int i = -1; i <= 1; i++) P(PrimitiveType.Cube, parent, new Vector3(i * 1.05f, 2.0f, 1.0f), new Vector3(0.95f, 0.7f, 0.04f), new Color(0.85f, 0.2f, 0.2f), false);
            foreach (float x in new[] { -1.6f, 1.6f }) P(PrimitiveType.Cube, parent, new Vector3(x, 1.7f, 1.0f), new Vector3(0.12f, 1.4f, 0.12f), new Color(0.35f, 0.22f, 0.15f));
            active = new GameObject("chop");
            active.transform.SetParent(parent, false);
            active.AddComponent<Spinner>().Bob = knife.transform;
            mouth = Mouth(parent, new Vector3(0, 1.3f, -0.6f));
        }

        /// <summary>Giant blender: base, glass jug with a smoothie inside.</summary>
        static void Blender(Transform parent, out Transform mouth, out GameObject active)
        {
            P(PrimitiveType.Cube, parent, new Vector3(0, 0.45f, 0), new Vector3(2.6f, 0.9f, 2.2f), new Color(0.95f, 0.4f, 0.45f));
            P(PrimitiveType.Cube, parent, new Vector3(0, 0.95f, 0), new Vector3(2.0f, 0.15f, 1.7f), new Color(0.3f, 0.3f, 0.35f));
            var glass = Visuals.Primitive(PrimitiveType.Cylinder, parent, new Vector3(0, 1.95f, 0), new Vector3(1.4f, 0.9f, 1.4f), Mats.LitTransparent(new Color(0.85f, 0.95f, 1f, 0.35f)), "jug");
            var fill = P(PrimitiveType.Cylinder, parent, new Vector3(0, 1.6f, 0), new Vector3(1.25f, 0.55f, 1.25f), new Color(1f, 0.92f, 0.55f), false);
            P(PrimitiveType.Cylinder, parent, new Vector3(0, 2.9f, 0), new Vector3(1.5f, 0.08f, 1.5f), new Color(0.3f, 0.3f, 0.35f));
            _ = glass;
            active = new GameObject("whirl");
            active.transform.SetParent(parent, false);
            active.AddComponent<Spinner>().Target = fill.transform;
            mouth = Mouth(parent, new Vector3(0, 1.4f, -1.0f));
        }

        /// <summary>Stove with a big pan (fryer / griddle / burger grill).</summary>
        static void Fryer(Transform parent, out Transform mouth, out Transform chimney, out GameObject active)
        {
            P(PrimitiveType.Cube, parent, new Vector3(0, 0.5f, 0), new Vector3(3.2f, 1f, 2.4f), new Color(0.75f, 0.76f, 0.8f));
            P(PrimitiveType.Cube, parent, new Vector3(0, 1.03f, 0), new Vector3(3.3f, 0.06f, 2.5f), new Color(0.25f, 0.25f, 0.28f));
            P(PrimitiveType.Cube, parent, new Vector3(0, 0.55f, -1.21f), new Vector3(2.4f, 0.5f, 0.04f), new Color(0.2f, 0.2f, 0.22f), false);
            var pan = Visuals.Spawn("food-kit/frying-pan", parent, 1.9f, Color.white);
            pan.transform.localPosition = new Vector3(-0.2f, 1.06f, 0.1f);
            // hood and chimney
            P(PrimitiveType.Cube, parent, new Vector3(0, 2.6f, 0.6f), new Vector3(2.6f, 0.5f, 1.2f), new Color(0.8f, 0.82f, 0.86f));
            P(PrimitiveType.Cylinder, parent, new Vector3(0, 3.2f, 0.6f), new Vector3(0.45f, 0.4f, 0.45f), new Color(0.6f, 0.62f, 0.66f));
            foreach (float x in new[] { -1.2f, 1.2f }) P(PrimitiveType.Cube, parent, new Vector3(x, 1.8f, 1.1f), new Vector3(0.1f, 1.5f, 0.1f), new Color(0.6f, 0.62f, 0.66f));
            chimney = new GameObject("chimneyTop").transform;
            chimney.SetParent(parent, false);
            chimney.localPosition = new Vector3(0, 3.65f, 0.6f);
            active = Visuals.Primitive(PrimitiveType.Cylinder, parent, new Vector3(-0.2f, 1.07f, 0.1f), new Vector3(1.3f, 0.02f, 1.3f), Mats.Glow(new Color(1f, 0.6f, 0.2f), 1.2f), "heat");
            mouth = Mouth(parent, new Vector3(-0.2f, 1.3f, -0.4f));
        }

        /// <summary>Long banquet table with platters, candles and a roast in the middle.</summary>
        static void Feast(Transform parent, out Transform mouth, out GameObject active)
        {
            P(PrimitiveType.Cube, parent, new Vector3(0, 0.85f, 0), new Vector3(3.4f, 0.14f, 2.2f), new Color(0.55f, 0.3f, 0.18f));
            P(PrimitiveType.Cube, parent, new Vector3(0, 0.93f, 0), new Vector3(3.5f, 0.03f, 2.3f), new Color(0.85f, 0.2f, 0.22f), false);
            foreach (float x in new[] { -1.5f, 1.5f })
                foreach (float z in new[] { -0.9f, 0.9f })
                    P(PrimitiveType.Cube, parent, new Vector3(x, 0.4f, z), new Vector3(0.18f, 0.8f, 0.18f), new Color(0.45f, 0.25f, 0.15f));
            var roast = Visuals.Spawn("food-kit/turkey", parent, 1.0f, Color.white);
            roast.transform.localPosition = new Vector3(0, 0.95f, 0);
            string[] dishes = { "food-kit/maki-salmon", "food-kit/frappe", "food-kit/burger-cheese", "food-kit/cake" };
            for (int i = 0; i < dishes.Length; i++)
            {
                var d = Visuals.Spawn(dishes[i], parent, 0.45f, Color.white);
                d.transform.localPosition = new Vector3(-1.2f + i * 0.8f, 0.95f, i % 2 == 0 ? 0.6f : -0.6f);
            }
            foreach (float x in new[] { -1.4f, 1.4f })
            {
                P(PrimitiveType.Cylinder, parent, new Vector3(x, 1.15f, 0), new Vector3(0.12f, 0.22f, 0.12f), new Color(1f, 0.97f, 0.88f), false);
                Visuals.Primitive(PrimitiveType.Sphere, parent, new Vector3(x, 1.43f, 0), Vector3.one * 0.1f, Mats.Glow(new Color(1f, 0.75f, 0.3f), 2f), "flame");
            }
            // golden arch over the table
            foreach (float x in new[] { -1.8f, 1.8f }) P(PrimitiveType.Cube, parent, new Vector3(x, 1.6f, 1.15f), new Vector3(0.18f, 3.2f, 0.18f), new Color(0.98f, 0.78f, 0.25f));
            P(PrimitiveType.Cube, parent, new Vector3(0, 3.2f, 1.15f), new Vector3(3.8f, 0.3f, 0.3f), new Color(0.98f, 0.78f, 0.25f));
            active = new GameObject("spin");
            active.transform.SetParent(parent, false);
            active.AddComponent<Spinner>().Target = roast.transform;
            mouth = Mouth(parent, new Vector3(0, 1.2f, -0.8f));
        }

        /// <summary>Ice cream machine: a mini-market freezer with a striped awning.</summary>
        static void Freezer(Transform parent, out Transform mouth, out GameObject active)
        {
            var f = Visuals.Spawn("mini-market/freezer", parent, 3.0f, Color.white);
            Visuals.SetShadows(f, true);
            P(PrimitiveType.Cube, parent, new Vector3(0, 2.6f, 0), new Vector3(3.2f, 0.12f, 2.3f), new Color(1f, 0.55f, 0.65f));
            for (int i = -2; i <= 2; i += 2) P(PrimitiveType.Cube, parent, new Vector3(i * 0.55f, 2.66f, 0), new Vector3(0.5f, 0.02f, 2.3f), Color.white, false);
            foreach (float x in new[] { -1.5f, 1.5f }) P(PrimitiveType.Cube, parent, new Vector3(x, 1.4f, 1.0f), new Vector3(0.1f, 2.4f, 0.1f), new Color(0.95f, 0.95f, 0.95f));
            var cone = Visuals.Spawn("food-kit/ice-cream", parent, 1.0f, Color.white, true);
            cone.transform.localPosition = new Vector3(0, 2.7f, 0);
            active = new GameObject("spin");
            active.transform.SetParent(parent, false);
            active.AddComponent<Spinner>().Target = cone.transform;
            mouth = Mouth(parent, new Vector3(0, 1.2f, -1.0f));
        }
    }

    /// <summary>Tiny "the machine is running" animation: spins a part, or bobs it up and down.</summary>
    public class Spinner : MonoBehaviour
    {
        public Transform Target, Bob;
        float _t;
        Vector3 _bobStart;
        bool _init;

        void Update()
        {
            _t += Time.deltaTime;
            if (Target != null) Target.Rotate(0, 220f * Time.deltaTime, 0, Space.Self);
            if (Bob != null)
            {
                if (!_init) { _bobStart = Bob.localPosition; _init = true; }
                Bob.localPosition = _bobStart + Vector3.up * Mathf.Abs(Mathf.Sin(_t * 9f)) * 0.25f;
            }
        }
    }
}
