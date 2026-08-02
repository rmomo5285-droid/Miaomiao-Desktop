namespace MiaomiaoHelper;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || !args[0].Equals("rebootas", StringComparison.OrdinalIgnoreCase))
            {
                ShowHelp();
                Environment.ExitCode = 64;
                return;
            }

            Console.WriteLine("Restarting Miaomiao...");
            Thread.Sleep(1000);
            Utils.StartMiaomiao();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Display help information and usage guidelines
    /// </summary>
    private static void ShowHelp()
    {
        Console.WriteLine("Usage: MiaomiaoHelper rebootas");
    }
}
