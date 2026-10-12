using System;
using System.Collections.Generic;
using UnityEngine;

namespace StackIsland
{
    /// <summary>
    /// Every buy tile's levels, prices and effects in one place. The game reads these to build and apply tiles,
    /// and the balance simulator reads the very same numbers, so measured pacing always matches the game.
    /// </summary>
    public static class Economy
    {
        public class TileSpec
        {
            public string Key;            // "src", "gather"... (id = y{n}.{Key}, or g.{Key} for global tiles)
            public string Kind;           // HIRE / UNLOCK / UPGRADE
            public int MaxLevel;
            public double Base, Growth;   // cost = Base x Growth^level (x yard Scale for yard tiles)
            public bool Global;
            public Vector3 Local;         // where it sits in its yard
            public float Width = 3.2f, Depth = 2.5f;

            public double Cost(int level, double scale) => Base * Math.Pow(Growth, level) * (Global ? 1 : scale);
        }

        public static readonly List<TileSpec> YardTiles = new List<TileSpec>
        {
            new TileSpec { Key = "src", Kind = "UPGRADE", MaxLevel = 10, Base = 30, Growth = 1.6, Local = new Vector3(-8.2f, 0, 1.4f) },
            new TileSpec { Key = "gather", Kind = "HIRE", MaxLevel = 4, Base = 50, Growth = 2.6, Local = new Vector3(-2.6f, 0, -1.4f) },
            new TileSpec { Key = "speed", Kind = "UPGRADE", MaxLevel = 20, Base = 100, Growth = 1.4, Local = new Vector3(10.9f, 0, 13f) },
            new TileSpec { Key = "waiter", Kind = "HIRE", MaxLevel = 3, Base = 120, Growth = 2.6, Local = new Vector3(10.9f, 0, -3.6f) },
            new TileSpec { Key = "parrot", Kind = "HIRE", MaxLevel = 1, Base = 180, Growth = 1, Local = new Vector3(5.6f, 0, -14.8f) },
            new TileSpec { Key = "station2", Kind = "UNLOCK", MaxLevel = 1, Base = 400, Growth = 1, Local = new Vector3(6f, 0, 3.4f), Width = 4.2f, Depth = 3.2f },
            new TileSpec { Key = "recipe", Kind = "UPGRADE", MaxLevel = 25, Base = 300, Growth = 1.38, Local = new Vector3(-8.2f, 0, -10.6f) },
            new TileSpec { Key = "busy", Kind = "UPGRADE", MaxLevel = 12, Base = 150, Growth = 1.5, Local = new Vector3(-2.6f, 0, -7.0f) },
            new TileSpec { Key = "crew", Kind = "UPGRADE", MaxLevel = 10, Base = 200, Growth = 1.6, Local = new Vector3(-8.2f, 0, -7.6f) },
            new TileSpec { Key = "manager", Kind = "HIRE", MaxLevel = 1, Base = 2500, Growth = 1, Local = new Vector3(0.4f, 0, -4.0f) },
        };

        public static readonly TileSpec Gate = new TileSpec { Key = "gate", Kind = "UNLOCK", MaxLevel = 1, Base = 1, Growth = 1, Local = new Vector3(10.9f, 0, 6.2f) };

        public static readonly List<TileSpec> GlobalTiles = new List<TileSpec>
        {
            new TileSpec { Key = "backpack", Kind = "UPGRADE", MaxLevel = 15, Base = 25, Growth = 1.75, Global = true, Local = new Vector3(-8.2f, 0, -4.6f) },
            new TileSpec { Key = "shoes", Kind = "UPGRADE", MaxLevel = 8, Base = 60, Growth = 2.2, Global = true, Local = new Vector3(-2.6f, 0, -12.4f) },
            new TileSpec { Key = "night", Kind = "UPGRADE", MaxLevel = 6, Base = 5000, Growth = 9, Global = true, Local = new Vector3(-8.2f, 0, -14.0f) },
        };

        // ------------------------------------------------------------------ effects (level = tile level)

        public static int PenAnimals(int src) => 4 + 2 * src;
        public static float PenRespawn(int src) => 2.5f * Mathf.Pow(0.9f, src);
        public static float FieldShare(int src) => Mathf.Min(1f, 0.4f + 0.06f * src);
        public static float FieldRegrow(int src) => 3f + 1.3f * src;
        public static float CookSeconds(YardDef d, int speed) => d.CookSeconds * Mathf.Pow(0.9f, speed);
        public static double PriceMult(int recipe) => 1.0 + 0.15 * recipe;
        public static float SpawnEvery(int busy) => 2.2f * Mathf.Pow(0.9f, busy);
        public static int MaxWant(int busy) => 2 + (busy + 1) / 2;
        /// <summary>Smallest order a customer places: orders lean to the top half of the range.</summary>
        public static int MinWant(int maxWant) => Mathf.Max(2, (maxWant + 2) / 2);
        public static double AvgWant(int busy) => (MinWant(MaxWant(busy)) + MaxWant(busy)) * 0.5;
        public const float ServeSeconds = 0.16f;   // a customer takes one item from the counter every this
        public static float HelperSpeed(int crew) => 3.6f * (1 + 0.08f * crew);
        public static int GathererCarry(int crew) => 8 + 2 * crew;
        public static int WaiterCarry(int crew) => 10 + 2 * crew;
        public static int Backpack(int level) => 12 + 4 * level;
        public static float WalkSpeed(int shoes) => 5.6f * (1 + 0.08f * shoes);

        /// <summary>Does this tile exist in this yard at all? (A yard without a pen or field has no "bigger source".)</summary>
        public static bool TileApplies(YardDef d, string key) => key != "src" || d.Source != SourceKind.None;

        /// <summary>Walking distance (m) a courier covers for one round trip between two yards (along the walkways).</summary>
        public static double CourierTrip(int fromYard, int toYard) => 2 * (Math.Abs(fromYard - toYard) * 32.0 + 24.0);

        /// <summary>When a yard tile appears (needs its prerequisites), given a level lookup for that yard.</summary>
        public static bool Available(string key, Func<string, int> lvl, double totalEarned, double scale)
        {
            switch (key)
            {
                case "src": return true;
                case "gather": return totalEarned >= 15 * scale || lvl("gather") > 0;
                case "speed":
                case "waiter":
                case "parrot": return lvl("gather") >= 1;
                case "station2": return lvl("speed") >= 1 || lvl("waiter") >= 1;
                case "recipe":
                case "manager": return lvl("station2") >= 1;
                case "gate":
                case "busy":
                case "crew": return lvl("waiter") >= 1;
            }
            return true;
        }

        public static bool GlobalAvailable(string key, double totalEarned) =>
            key == "shoes" ? totalEarned >= 100 : key == "night" ? totalEarned >= 20000 : true;

        // offline earnings: helpers keep working while you're away
        public static double OfflineEfficiency(int night) => Math.Min(0.95, 0.5 + 0.08 * night);
        public static double OfflineCapHours(int night) => 3 + 1.5 * night;
    }
}
