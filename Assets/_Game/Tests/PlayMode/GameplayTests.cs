using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StackIsland.Tests
{
    /// <summary>Runs the real game scene on a throwaway save and checks the core loops end to end.</summary>
    public class GameplayTests
    {
        Game G => Game.I;
        Yard Y1 => G.Yard1;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Game.TestMode = true;
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.NotNull(Game.I, "game booted");
            Assert.NotNull(Y1, "yard 1 built");
        }

        [UnityTest]
        public IEnumerator AnimalDropsMeatThatCanBeCarried()
        {
            yield return new WaitForSeconds(0.5f);   // spawn pop-in
            var crab = Animal.All.First(c => c.Alive);
            for (int i = 0; i < 6 && crab.Hp > 0; i++) crab.Hit(1, crab.transform.position + Vector3.right);
            Assert.AreEqual(0, crab.Hp);
            yield return new WaitForSeconds(0.6f);
            Assert.GreaterOrEqual(LooseItem.All.Count(l => l.Type == ItemType.Meat), 1, "meat dropped");
            G.Player.Teleport(LooseItem.All[0].transform.position);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(G.Player.Stack.CountOf(ItemType.Meat), 0, "player scooped meat");
        }

        [UnityTest]
        public IEnumerator StationCooksAndCustomersPay()
        {
            var st = Y1.Stations[0];
            for (int i = 0; i < 4; i++) st.Inputs[0].Add();
            yield return new WaitForSeconds(st.CookSeconds * 4 + 0.6f);
            Assert.AreEqual(4, st.Output.Count, "4 cakes cooked");
            for (int i = 0; i < 12; i++) Y1.Counter.Shelf.Add();
            float t = 0;
            while (Y1.Counter.Cash.Count == 0 && t < 30f) { t += Time.deltaTime; yield return null; }
            Assert.Greater(Y1.Counter.Cash.Count, 0, "cash on the tray");
            double before = G.Cash;
            G.Player.Teleport(Y1.Counter.Cash.transform.position);
            yield return new WaitForSeconds(1f);
            Assert.Greater(G.Cash, before, "collected cash");
        }

        [UnityTest]
        public IEnumerator StandingStillOnPadBuysIt()
        {
            var pad = G.PadById("y1.src");
            G.Grant(pad.Cost + 5);
            G.Player.Teleport(pad.transform.position);
            float t = 0;
            while (pad.Level == 0 && t < 6f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(1, pad.Level, "bought");
            Assert.AreEqual(6, Y1.Pen.Count, "two more crabs");
        }

        [UnityTest]
        public IEnumerator GateOpensFieldYardAndFieldIsHarvestable()
        {
            G.PadById("y1.gate").ForceBuy();
            yield return null;
            Assert.IsTrue(G.Yards.ContainsKey(2), "yard 2 open");
            var y2 = G.Yards[2];
            Assert.NotNull(y2.Field, "coconut field");
            Assert.Greater(y2.Field.Present, 0);
            var cc = G.Player.GetComponent<CharacterController>();
            G.Player.Teleport(y2.W(-6f, 14.5f));   // the field fills from the back first
            yield return new WaitForSeconds(1f);
            Assert.Greater(G.Player.Stack.CountOf(ItemType.Coconut), 0, "walked through the field and picked coconuts");
            _ = cc;
        }

        [UnityTest]
        public IEnumerator FieldGatherersOnlyCrossTheFenceAtTheGate()
        {
            G.PadById("y1.gate").ForceBuy();
            yield return null;
            var y2 = G.Yards[2];
            var gather = G.PadById("y2.gather");
            for (int i = 0; i < 3; i++) gather.ForceBuy();
            yield return null;
            Assert.AreEqual(3, y2.Gatherers.Count);
            var was = y2.Gatherers.Select(w => y2.InSource(w.transform.position)).ToArray();
            var last = y2.Gatherers.Select(w => w.transform.position).ToArray();
            Time.timeScale = 4f;
            string bad = null;
            float t = 0;
            while (t < 40f && bad == null)
            {
                t += Time.deltaTime;
                yield return null;
                for (int i = 0; i < y2.Gatherers.Count; i++)
                {
                    var p = y2.Gatherers[i].transform.position;
                    bool inside = y2.InSource(p);
                    if (inside != was[i])
                    {
                        // a legal crossing goes through the gate gap in the front fence
                        var mid = (p + last[i]) * 0.5f;
                        if (Mathf.Abs(mid.x - y2.SrcGate.x) > 1.3f) bad = $"gatherer {i} crossed at {y2.transform.InverseTransformPoint(mid)} ({(inside ? "in" : "out")}) t={t:0.0} from {y2.transform.InverseTransformPoint(last[i])} {y2.Gatherers[i].DebugRoute}";
                        was[i] = inside;
                    }
                    last[i] = p;
                }
            }
            Time.timeScale = 1f;
            Assert.IsNull(bad, bad);
        }
    }
}
