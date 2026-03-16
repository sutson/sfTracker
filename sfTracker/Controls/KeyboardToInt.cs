using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace sfTracker.Controls
{
    /// <summary>
    /// Class which converts keyboard number inputs to their associated values.
    /// </summary>
    public static class KeyboardToInt
    {
        public static readonly Dictionary<Key, int> Map =
            new Dictionary<Key, int>
            {
                { Key.D0, 0 },
                { Key.D1, 1 },
                { Key.D2, 2 },
                { Key.D3, 3 },
                { Key.D4, 4 },
                { Key.D5, 5 },
                { Key.D6, 6 },
                { Key.D7, 7 },
                { Key.D8, 8 },
                { Key.D9, 9 },
            };
    }
}

