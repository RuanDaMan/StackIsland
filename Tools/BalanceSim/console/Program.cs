public static class Program
{
    public static void Main()
    {
        // SIM_GATES="2=12000,3=3.6e6" overrides gate costs, SIM_SCALES the same for tile scales (for tune.py)
        Override("SIM_GATES", (d, v) => d.GateCost = v);
        Override("SIM_SCALES", (d, v) => d.Scale = v);
        System.Console.Write(StackIsland.BalanceModel.Run());
    }

    static void Override(string env, System.Action<StackIsland.YardDef, double> set)
    {
        var s = System.Environment.GetEnvironmentVariable(env);
        if (string.IsNullOrEmpty(s)) return;
        foreach (var part in s.Split(','))
        {
            var kv = part.Split('=');
            set(StackIsland.YardDefs.Get(int.Parse(kv[0])), double.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
