using System;
using System.Threading.Tasks;

namespace UnityAutorun.Mcp
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "mcp")
                {
                    await new McpServer().RunAsync();
                    return 0;
                }

                if (args.Length > 0 && args[0] == "mock-bridge")
                {
                    await MockBridge.RunAsync();
                    return 0;
                }

                return await CliApp.RunAsync(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}

