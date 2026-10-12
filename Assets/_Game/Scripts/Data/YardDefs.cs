using System.Collections.Generic;
using UnityEngine;

namespace StackIsland
{
    public enum SourceKind { None, Pen, Field }

    /// <summary>
    /// One yard: where its raw item comes from (a pen of animals you hit, or a field you walk through), what its
    /// station makes, what the counter sells, its colours, and how expensive it is. Everything else (layout, buy
    /// tiles, helpers) is shared, so a new yard is just a new entry here.
    /// </summary>
    public class YardDef
    {
        public int Index;
        public string Name;
        public string Tagline;

        // where the raw item comes from
        public SourceKind Source = SourceKind.Pen;
        public string SourceTitle = "More Crabs";       // the "bigger source" upgrade
        public string AnimalModel = "cube-pets/animal-crab";
        public string AnimalName = "crab";
        public float AnimalSize = 1.9f;
        public int AnimalHp = 3;
        public int RawPerAnimal = 3;
        public ItemType Raw = ItemType.Meat;

        // what's made and sold
        public string StationName = "Grill";
        public string StationLook = "dome";
        public float CookSeconds = 1.1f;
        public ItemType Product = ItemType.CrabCake;
        public ItemType[] Inputs;                        // station inputs (default: the raw item)
        public string GathererName = "Crab Catcher";
        public string SpeedTitle = "Hotter Grill";
        public string RecipeTitle = "Secret Recipe";

        // economy: pad prices are base x Scale; GateCost buys this yard from the previous one
        public double Scale = 1;
        public double GateCost;

        // looks
        public Color Floor = new Color(0.97f, 0.74f, 0.6f);
        public Color Tile = new Color(0.98f, 0.66f, 0.42f);
        public Color SourceFloor = new Color(0.98f, 0.86f, 0.62f);
        public Color Wall = new Color(0.93f, 0.42f, 0.36f);
        public Color Cage = new Color(0.22f, 0.56f, 0.92f);

        public ItemType[] StationInputs => Inputs ?? new[] { Raw };
    }

    public static class YardDefs
    {
        public static readonly List<YardDef> All = new List<YardDef>
        {
            new YardDef
            {
                Index = 1, Name = "Crab Cove", Tagline = "Catch crabs, grill crab cakes.",
                Scale = 1, GateCost = 0,
            },
            new YardDef
            {
                Index = 2, Name = "Coconut Grove", Tagline = "Wade through coconuts, press fresh juice.",
                Source = SourceKind.Field, SourceTitle = "Bigger Grove", Raw = ItemType.Coconut,
                StationName = "Juice Press", StationLook = "press", CookSeconds = 1.3f, Product = ItemType.Juice,
                GathererName = "Coconut Picker", SpeedTitle = "Faster Press", RecipeTitle = "Sweeter Juice",
                Scale = 45.2, GateCost = 28700,
                Floor = new Color(0.98f, 0.82f, 0.62f), Tile = new Color(0.95f, 0.7f, 0.46f), SourceFloor = new Color(0.62f, 0.86f, 0.42f),
                Wall = new Color(0.85f, 0.55f, 0.32f), Cage = new Color(0.36f, 0.72f, 0.3f),
            },
            new YardDef
            {
                Index = 3, Name = "Fish Lagoon", Tagline = "Net fish in the lagoon, roll sushi.",
                SourceTitle = "More Fish", AnimalModel = "cube-pets/animal-fish", AnimalName = "fish", AnimalSize = 1.5f, AnimalHp = 3, RawPerAnimal = 2,
                Raw = ItemType.Fish, StationName = "Sushi Bar", StationLook = "sushi", CookSeconds = 1.5f, Product = ItemType.Sushi,
                GathererName = "Fisher", SpeedTitle = "Sharper Knives", RecipeTitle = "Chef's Special",
                Scale = 281, GateCost = 613000,
                Floor = new Color(0.8f, 0.9f, 0.98f), Tile = new Color(0.66f, 0.8f, 0.95f), SourceFloor = new Color(0.3f, 0.66f, 0.92f),
                Wall = new Color(0.95f, 0.55f, 0.4f), Cage = new Color(0.98f, 0.74f, 0.2f),
            },
            new YardDef
            {
                Index = 4, Name = "Banana Jungle", Tagline = "Harvest bananas, blend smoothies.",
                Source = SourceKind.Field, SourceTitle = "Bigger Plantation", Raw = ItemType.Banana,
                StationName = "Blender", StationLook = "blender", CookSeconds = 1.6f, Product = ItemType.Smoothie,
                GathererName = "Banana Picker", SpeedTitle = "Turbo Blades", RecipeTitle = "Extra Creamy",
                Scale = 1910, GateCost = 2.53e6,
                Floor = new Color(0.86f, 0.95f, 0.7f), Tile = new Color(0.74f, 0.88f, 0.52f), SourceFloor = new Color(0.98f, 0.9f, 0.52f),
                Wall = new Color(0.45f, 0.7f, 0.35f), Cage = new Color(0.9f, 0.45f, 0.3f),
            },
            new YardDef
            {
                Index = 5, Name = "Hog Farm", Tagline = "Round up hogs, fry crispy bacon.",
                SourceTitle = "More Hogs", AnimalModel = "cube-pets/animal-hog", AnimalName = "hog", AnimalSize = 1.9f, AnimalHp = 4, RawPerAnimal = 3,
                Raw = ItemType.Pork, StationName = "Fryer", StationLook = "fryer", CookSeconds = 1.7f, Product = ItemType.Bacon,
                GathererName = "Farmhand", SpeedTitle = "Hotter Oil", RecipeTitle = "Maple Glaze",
                Scale = 10200, GateCost = 2.6e7,
                Floor = new Color(0.98f, 0.78f, 0.78f), Tile = new Color(0.95f, 0.64f, 0.66f), SourceFloor = new Color(0.72f, 0.58f, 0.42f),
                Wall = new Color(0.62f, 0.42f, 0.3f), Cage = new Color(0.95f, 0.9f, 0.85f),
            },
            new YardDef
            {
                Index = 6, Name = "Chicken Coop", Tagline = "Gather eggs, flip pancakes.",
                Source = SourceKind.Field, SourceTitle = "More Nests", Raw = ItemType.Egg,
                StationName = "Griddle", StationLook = "fryer", CookSeconds = 1.8f, Product = ItemType.Pancakes,
                GathererName = "Egg Collector", SpeedTitle = "Bigger Griddle", RecipeTitle = "Honey Drizzle",
                Scale = 75900, GateCost = 1.36e8,
                Floor = new Color(1f, 0.92f, 0.66f), Tile = new Color(0.98f, 0.82f, 0.45f), SourceFloor = new Color(0.86f, 0.74f, 0.5f),
                Wall = new Color(0.9f, 0.6f, 0.25f), Cage = new Color(0.55f, 0.36f, 0.25f),
            },
            new YardDef
            {
                Index = 7, Name = "Pineapple Plantation", Tagline = "Pick pineapples, bake island pizza.",
                Source = SourceKind.Field, SourceTitle = "Bigger Plantation", Raw = ItemType.Pineapple,
                StationName = "Pizza Oven", StationLook = "dome", CookSeconds = 2f, Product = ItemType.Pizza,
                GathererName = "Pineapple Picker", SpeedTitle = "Hotter Oven", RecipeTitle = "Stuffed Crust",
                Scale = 524000, GateCost = 1.11e9,
                Floor = new Color(0.95f, 0.88f, 0.98f), Tile = new Color(0.86f, 0.74f, 0.95f), SourceFloor = new Color(0.66f, 0.82f, 0.4f),
                Wall = new Color(0.6f, 0.4f, 0.75f), Cage = new Color(0.98f, 0.8f, 0.25f),
            },
            new YardDef
            {
                Index = 8, Name = "Strawberry Patch", Tagline = "Pick strawberries, scoop ice cream.",
                Source = SourceKind.Field, SourceTitle = "Bigger Patch", Raw = ItemType.Strawberry,
                StationName = "Ice Cream Machine", StationLook = "freezer", CookSeconds = 2.1f, Product = ItemType.IceCream,
                GathererName = "Berry Picker", SpeedTitle = "Colder Churn", RecipeTitle = "Extra Sprinkles",
                Scale = 3.14e6, GateCost = 6.85e9,
                Floor = new Color(1f, 0.86f, 0.9f), Tile = new Color(0.98f, 0.7f, 0.8f), SourceFloor = new Color(0.5f, 0.78f, 0.38f),
                Wall = new Color(0.95f, 0.4f, 0.55f), Cage = new Color(0.98f, 0.98f, 0.98f),
            },
            new YardDef
            {
                Index = 9, Name = "Burger Beach", Tagline = "Herd cattle, grill the island's best burgers.",
                SourceTitle = "More Cattle", AnimalModel = "cube-pets/animal-cow", AnimalName = "cow", AnimalSize = 2.1f, AnimalHp = 5, RawPerAnimal = 4,
                Raw = ItemType.Beef, StationName = "Burger Grill", StationLook = "fryer", CookSeconds = 2.2f, Product = ItemType.Burger,
                GathererName = "Rancher", SpeedTitle = "Bigger Flames", RecipeTitle = "Secret Sauce",
                Scale = 2.04e7, GateCost = 4.54e10,
                Floor = new Color(0.95f, 0.85f, 0.7f), Tile = new Color(0.88f, 0.72f, 0.52f), SourceFloor = new Color(0.55f, 0.8f, 0.4f),
                Wall = new Color(0.78f, 0.3f, 0.25f), Cage = new Color(0.3f, 0.3f, 0.35f),
            },
        };

        static YardDefs()
        {
            // the finale: no pen or field - couriers bring the best dishes from three other yards
            All.Add(new YardDef
            {
                Index = 10, Name = "Volcano Feast", Tagline = "Bring sushi, smoothies and burgers together for a legendary feast.",
                Source = SourceKind.None, Raw = ItemType.None, Inputs = new[] { ItemType.Sushi, ItemType.Smoothie, ItemType.Burger },
                StationName = "Feast Table", StationLook = "feast", CookSeconds = 2.6f, Product = ItemType.Feast,
                GathererName = "Courier", SpeedTitle = "Bigger Platters", RecipeTitle = "Royal Garnish",
                Scale = 3.42e7, GateCost = 2.28e11,
                Floor = new Color(0.55f, 0.5f, 0.52f), Tile = new Color(0.45f, 0.4f, 0.42f), SourceFloor = new Color(0.32f, 0.28f, 0.3f),
                Wall = new Color(0.3f, 0.26f, 0.28f), Cage = new Color(0.98f, 0.45f, 0.15f),
            });
        }

        public static YardDef Get(int index) => All.Find(y => y.Index == index);
    }
}
