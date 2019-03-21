using System;
using System.Threading.Tasks;

namespace CoreEngine.Compiler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("CoreEngine Compiler Tool 0.1");
            Console.WriteLine();
            
            if (args.Length > 0)
            {
                var input = args[0];

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Compiling '{input}'...");
                Console.ForegroundColor = ConsoleColor.Gray;

                try
                {
                    var projectCompiler = new ProjectCompiler();
                    await projectCompiler.CompileProject(input);
                }

                catch(Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERROR: {e.Message}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }
        }
    }
}
