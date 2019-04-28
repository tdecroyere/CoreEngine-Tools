using System;
using System.Threading;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;
using CoreEngine.Tools.ResourceCompilers;

namespace CoreEngine.Compiler
{
    class Program
    {
        private static async Task RunCompilePass(Logger logger, ResourceCompiler resourceCompiler, string input, bool isWatchMode)
        {
            if (!isWatchMode)
            {
                logger.WriteMessage($"Compiling '{input}'...", LogMessageType.Important);
            }

            try
            {
                var projectCompiler = new ProjectCompiler(resourceCompiler, logger);
                await projectCompiler.CompileProject(input, isWatchMode, true);
            }

            catch(Exception e)
            {
                logger.WriteMessage($"Error: {e.Message}", LogMessageType.Error);
            }
        }

        static async Task Main(string[] args)
        {
            // TODO: Add verbose parameter
            // TODO: Add help parameter
            // TODO: Add rebuild parameter
            // TODO: Add watch parameter

            var logger = new Logger();
            var resourceCompiler = new ResourceCompiler(logger);

            logger.WriteMessage("CoreEngine Compiler Tool version 0.1");
            logger.WriteLine();
            
            if (args.Length > 0)
            {
                var input = args[0];
                var isWatchMode = (args.Length > 1 && args[1] == "--watch");

                if (!isWatchMode)
                {
                    await RunCompilePass(logger, resourceCompiler, input, isWatchMode);
                }

                else
                {
                    logger.WriteMessage("Entering watch mode...", LogMessageType.Action);

                    while (true)
                    {
                        await RunCompilePass(logger, resourceCompiler, input, isWatchMode);
                        Thread.Sleep(1000);
                    }
                }
            }
        }
    }
}
