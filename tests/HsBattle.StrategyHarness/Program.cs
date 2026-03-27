using System;

namespace HsBattle.StrategyHarness
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                PlannerTests.RunAll();
                Console.WriteLine("Planner harness passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
