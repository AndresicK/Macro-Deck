using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SuchByte.MacroDeck.Startup;

internal class MutexHelper
{
    public static bool IsRunning()
    {
        var _ = new Mutex(true, "MacroDeckServer2", out var createdNew);
        return !createdNew;
    }
}