using System;
using System.Threading;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using CoreEngine.Tools.ResourceCompilers;

namespace CoreEngine.Compiler
{
    class Program
    {
        private static async Task RunCompilePass(ResourceCompiler resourceCompiler, string input, string? searchPattern, bool isWatchMode, bool rebuildAll)
        {
            if (!isWatchMode)
            {
                Logger.WriteMessage($"Compiling '{input}'...", LogMessageTypes.Important);
            }

            try
            {
                var projectCompiler = new ProjectCompiler(resourceCompiler);
                await projectCompiler.CompileProject(input, searchPattern, isWatchMode, rebuildAll);
            }

            catch (Exception e)
            {
                Logger.WriteMessage($"Error: {e.Message}", LogMessageTypes.Error);
            }
        }

        static async Task Main(string[] args)
        {
            // TODO: Add verbose parameter
            // TODO: Add help parameter
            // TODO: Add version number

            var resourceCompiler = new ResourceCompiler();

            Logger.WriteMessage("CoreEngine Compiler Tool version 1.0");
            Logger.WriteLine();
            
            if (args.Length > 0)
            {
                var input = args[0];
                var isWatchMode = (args.Length > 1 && args[1] == "--watch");
                var rebuildAll = (args.Length > 1 && args[1] == "--rebuild");
                string? searchPattern = null;
                
                if (args.Length > 1 && !args[1].StartsWith("--"))
                {
                    searchPattern = args[1];
                }

                if (!isWatchMode)
                {
                    await RunCompilePass(resourceCompiler, input, searchPattern, isWatchMode, rebuildAll);
                }

                else
                {
                    Logger.WriteMessage("Entering watch mode...", LogMessageTypes.Action);

                    while (true)
                    {
                        await RunCompilePass(resourceCompiler, input, null, isWatchMode, rebuildAll);
                        Thread.Sleep(1000);
                    }
                }
            }
        }
    }
}
