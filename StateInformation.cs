using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace LockInitClient
{
    /*
     * The states for initialization a device.
     */
    public enum InitializationState
    {
        Assigning = 2,
        InitializeConfig = 3,
        TestingConfig = 4,
        Done = 5,
        Running = 6,
        MaxState = 7
    }
}
