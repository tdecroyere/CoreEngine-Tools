using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace CoreEngine.Tools.Common
{
    public static class Logger
    {
        // TODO: This code is not thread-safe!
        private static Stack<string> messageStack = new Stack<string>();
        private static int currentLevel = 0;
        private static Stack<Stopwatch> stopwatchStack = new Stack<Stopwatch>();

        public static void WriteMessage(string message, LogMessageTypes messageType = LogMessageTypes.Normal)
        {
            if ((messageType & LogMessageTypes.Success) != 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }

            else if ((messageType & LogMessageTypes.Action) != 0)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
            }

            else if ((messageType & LogMessageTypes.Warning) != 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }

            else if ((messageType & LogMessageTypes.Error) != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }

            else if ((messageType & LogMessageTypes.Important) != 0)
            {
                Console.ForegroundColor = ConsoleColor.White;
            }

            for (var i = 0; i < currentLevel; i++)
            {
                Console.Write(" ");
            }

            if (messageType != LogMessageTypes.Normal && messageType != LogMessageTypes.Debug && messageType != LogMessageTypes.Important && messageType != LogMessageTypes.Action && messageType != LogMessageTypes.Success)
            {
                message = $"{messageType.ToString()}: " + message;
            }

            Console.WriteLine(message);
            Debug.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void WriteLine()
        {
            Console.WriteLine();
        }

        public static void BeginAction(string message)
        {
            messageStack.Push(message);
            WriteMessage($"{message}...", LogMessageTypes.Action);
            currentLevel++;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            stopwatchStack.Push(stopwatch);
        }

        public static void EndAction()
        {
            currentLevel--;
            var message = messageStack.Pop();
            var stopwatch = stopwatchStack.Pop();

            WriteMessage($"{message} done. (Elapsed: {stopwatch.ElapsedMilliseconds} ms)", LogMessageTypes.Success);
        }

        public static void EndActionError()
        {
            currentLevel--;
            var message = messageStack.Pop();
            stopwatchStack.Pop();

            WriteMessage($"{message} failed.", LogMessageTypes.Error);
        }

        public static void EndActionWarning(string message)
        {
            currentLevel--;
            messageStack.Pop();
            stopwatchStack.Pop();

            WriteMessage($"{message}.", LogMessageTypes.Warning);
        }
    }
}