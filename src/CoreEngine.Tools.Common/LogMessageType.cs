using System;

namespace CoreEngine.Tools.Common
{
    [Flags]
    public enum LogMessageTypes
    {
        None = 0,
        Normal = 1,
        Error = 2,
        Success = 4,
        Warning = 8,
        Debug = 16,
        Important = 32,
        Action = 64,

        Minimal = Normal | Success | Warning | Error | Important | Action,
        All = Minimal | Debug
    }
}