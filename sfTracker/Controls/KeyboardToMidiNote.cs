using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace sfTracker.Controls
{
    /// <summary>
    /// Class which converts keyboard key inputs to MIDI note values.
    /// </summary>
    public static class KeyboardToMidiNote
    {
        public static readonly Dictionary<Key, MidiNoteValueMap> Map =
            new Dictionary<Key, MidiNoteValueMap>
            { 
                { Key.Z, MidiNoteValueMap.C_3 },
                { Key.S, MidiNoteValueMap.C_SHARP_3 },
                { Key.X, MidiNoteValueMap.D_3 },
                { Key.D, MidiNoteValueMap.D_SHARP_3 },
                { Key.C, MidiNoteValueMap.E_3 },
                { Key.V, MidiNoteValueMap.F_3 },
                { Key.G, MidiNoteValueMap.F_SHARP_3 },
                { Key.B, MidiNoteValueMap.G_3 },
                { Key.H, MidiNoteValueMap.G_SHARP_3 },
                { Key.N, MidiNoteValueMap.A_3 },
                { Key.J, MidiNoteValueMap.A_SHARP_3 },
                { Key.M, MidiNoteValueMap.B_3 },
                { Key.OemComma, MidiNoteValueMap.C_4 }, // comma key
                { Key.L, MidiNoteValueMap.C_SHARP_4 },
                { Key.OemPeriod, MidiNoteValueMap.D_4 }, // full stop key

                { Key.Q, MidiNoteValueMap.C_4 },
                { Key.D2, MidiNoteValueMap.C_SHARP_4 },
                { Key.W, MidiNoteValueMap.D_4 },
                { Key.D3, MidiNoteValueMap.D_SHARP_4 },
                { Key.E, MidiNoteValueMap.E_4 },
                { Key.R, MidiNoteValueMap.F_4 },
                { Key.D5, MidiNoteValueMap.F_SHARP_4 },
                { Key.T, MidiNoteValueMap.G_4 },
                { Key.D6, MidiNoteValueMap.G_SHARP_4 },
                { Key.Y, MidiNoteValueMap.A_4 },
                { Key.D7, MidiNoteValueMap.A_SHARP_4 },
                { Key.U, MidiNoteValueMap.B_4 },
                { Key.I, MidiNoteValueMap.C_5 },
                { Key.D9, MidiNoteValueMap.C_SHARP_5 },
                { Key.O, MidiNoteValueMap.D_5 },
            };
    }

}
