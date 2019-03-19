using System;
using System.Threading.Tasks;
using CoreEngine.ResourceCompilers;

namespace CoreEngine.Compiler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("CoreEngine Compiler Tool");

            var resourceCompiler = new ResourceCompiler();

            if (args.Length > 1)
            {
                var input = args[0];
                var output = args[1];

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Compiling '{input}' to '{output}'...");
                Console.ForegroundColor = ConsoleColor.Gray;

                try
                {
                    await resourceCompiler.CompileFileAsync(input, output);
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
