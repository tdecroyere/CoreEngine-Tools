using System;
using System.Threading.Tasks;
using CoreEngine.Tools.Common;

namespace CoreEngine.Compiler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var logger = new Logger();

            logger.WriteMessage("CoreEngine Compiler Tool version 0.1");
            logger.WriteLine();
            
            if (args.Length > 0)
            {
                var input = args[0];

                logger.WriteMessage($"Compiling '{input}'...", LogMessageType.Important);

                try
                {
                    var projectCompiler = new ProjectCompiler(logger);
                    await projectCompiler.CompileProject(input, false);
                }

                catch(Exception e)
                {
                    logger.WriteMessage($"Error: {e.Message}", LogMessageType.Error);
                }
            }
        }
    }
}
