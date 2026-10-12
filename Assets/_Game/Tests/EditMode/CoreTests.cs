using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StackIsland.Tests
{
    public class CoreTests
    {
        [Test]
        public void NumbersFormatShort()
        {
            Assert.AreEqual("0", NumberFormatter.Format(0));
            Assert.AreEqual("999", NumberFormatter.Format(999.9));
            Assert.AreEqual("1.5K", NumberFormatter.Format(1500));
            Assert.AreEqual("2.25M", NumberFormatter.Format(2_250_000));
        }

        [Test]
        public void SaveRoundTrips()
        {
            var s = new SaveData { cash = 123.5, totalEarned = 400 };
            s.Pad("crabs").level = 2;
            s.Pile("grill1:in").count = 7;
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s));
            Assert.AreEqual(123.5, back.cash);
            Assert.AreEqual(2, back.Pad("crabs").level);
            Assert.AreEqual(7, back.Pile("grill1:in").count);
        }

        [Test]
        public void YardsAreConsistent()
        {
            var all = YardDefs.All;
            Assert.GreaterOrEqual(all.Count, 3);
            for (int i = 0; i < all.Count; i++)
            {
                var y = all[i];
                Assert.AreEqual(i + 1, y.Index, "yards are numbered 1..n in order");
                Assert.Greater(Items.Price(y.Product), 0, y.Name + " product has a price");
                Assert.AreEqual(0, Items.Price(y.Raw), y.Name + " raw item is not sold directly");
                if (y.Source == SourceKind.Pen) Assert.IsFalse(string.IsNullOrEmpty(y.AnimalModel));
                if (i == 0) continue;
                var prev = all[i - 1];
                Assert.Greater(y.GateCost, prev.GateCost, "gates get more expensive");
                Assert.Greater(Items.Price(y.Product), Items.Price(prev.Product), "later products are worth more");
                // the combo finale is limited by couriers, not by its price, so it's priced on its own
                if (y.Source != SourceKind.None) Assert.Greater(y.Scale, prev.Scale, "later yards cost more");
            }
        }

        [Test]
        public void BalanceSimPacesYardsEvenlyWithoutStalls()
        {
            BalanceModel.Run();
            Assert.That(BalanceModel.LastTotalHours, Is.InRange(3.0, 40.0), "full game length (thoughtful player)");
            Assert.LessOrEqual(BalanceModel.LastLongestWaitMinutes, 25.0, "no single purchase takes ages to save up for");
            // every yard after the first takes roughly the same time (the first is a quick tutorial, the tail mops up)
            var middle = BalanceModel.LastYardHours.Skip(1).Take(BalanceModel.LastYardHours.Count - 2).ToList();
            Assert.LessOrEqual(middle.Max() / middle.Min(), 2.5, "yards take about as long as each other: " + string.Join(", ", middle.Select(h => h.ToString("0.0"))));
        }

        [Test]
        public void EveryAnimalGetsEasierWithEachCatcherAndTheLastOneShots()
        {
            foreach (var y in YardDefs.All)
            {
                if (y.Source != SourceKind.Pen) continue;
                int prevHits = int.MaxValue;
                for (int l = 0; l <= Economy.GatherMaxLevel; l++)
                {
                    int hits = Economy.HitsToCatch(y.AnimalHp, Economy.GathererDamage(y.AnimalHp, l));
                    Assert.LessOrEqual(hits, prevHits, $"{y.Name}: hiring catcher {l} never makes a {y.AnimalName} harder to catch");
                    prevHits = hits;
                }
                Assert.AreEqual(y.AnimalHp, Economy.HitsToCatch(y.AnimalHp, Economy.GathererDamage(y.AnimalHp, 0)), $"{y.Name}: with no catchers you start at full {y.AnimalHp} hits");
                Assert.AreEqual(1, prevHits, $"{y.Name}: every catcher hired = one hit");
            }
        }

        [Test]
        public void ItemsHavePricesAndHeights()
        {
            Assert.Greater(Items.Price(ItemType.CrabCake), 0);
            Assert.Greater(Items.Height(ItemType.Meat), 0f);
        }
    }
}
