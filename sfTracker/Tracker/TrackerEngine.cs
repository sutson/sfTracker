using MeltySynth;
using sfTracker.Playback;
using System;
using System.Collections.Generic;
using System.Linq;

namespace sfTracker.Tracker
{
    /// <summary>
    /// Class which models a the main logic for the tracker
    /// </summary>
    public class TrackerEngine
    {
        public Synthesizer synthesizer;
        private readonly int sampleRate;
        private double samplesPerTick;
        public double tickSampleCounter;

        public int BPM { get; private set; } = 125; // TODO: change default BPM
        public int TicksPerBeat { get; private set; } = 24; // ticks per beat is arbitrary, but 24 has a lot of factors
        public int Speed { get; private set; } = 6; // TODO: change default Speed
        
        public int CurrentRow { get; set; }
        public int CurrentPattern { get; set; }
        public int CurrentTick { get; set; }
        public Pattern[] Patterns { get; set; } = new Pattern[2];
        public Voice[] ActiveVoices { get; private set; } = new Voice[10]; // TODO: make this 10 be a value equal to the max number of channels

        public TrackerEngine(Synthesizer synthesizer, int sampleRate)
        {
            this.synthesizer = synthesizer;
            this.sampleRate = sampleRate;
            RecalculateTiming(); // recalculate timing based on sample rate and BPM
        }

        /// <summary>
        /// Method to recalculate timing based on sample rate and BPM.
        /// </summary>
        private void RecalculateTiming()
        {
            double secondsPerBeat = 60.0 / BPM;
            double secondsPerTick = secondsPerBeat / TicksPerBeat;
            samplesPerTick = sampleRate * secondsPerTick;
        }

        /// <summary>
        /// Method to set the BPM and recalculate the timing based on this new value.
        /// </summary>
        /// <param name="bpm">the desired BPM (Beats Per Minute)</param>
        public void SetBPM(int bpm)
        {
            BPM = bpm;
            RecalculateTiming();
        }

        /// <summary>
        /// Method to set the Speed of the tracker.
        /// </summary>
        public void SetSpeed(int speed)
        {
            Speed = speed;
        }

        /// <summary>
        /// Method for advancing forward one tick.
        /// </summary>
        private void AdvanceTick()
        {
            if (CurrentTick % TicksPerBeat == 0)
                TriggerRow(); // trigger row on each beat
            else
                ProcessTickEffects(); // TODO: implement effects functionality

            CurrentTick++; // advance to next tick
            
            // based on Speed, handle row change
            if (CurrentTick >= Speed)
            {
                CurrentTick = 0; // reset tick to 0
                CurrentRow++; // advance to next row
            }

            if (Patterns[CurrentPattern] != null && CurrentRow >= Patterns[CurrentPattern].RowCount)
            {
                CurrentRow = 0; // loop back to the start if no rows left

                // reset to first pattern if at end of Patterns array, otherwise advance to next
                if (CurrentPattern == Patterns.Length - 1)
                    CurrentPattern = 0;
                else
                    CurrentPattern++;
            }
        }

        /// <summary>
        /// Method to trigger a note.
        /// </summary>
        /// <param name="channel">the channel (or column) being considered</param>
        /// <param name="note">the MIDI pitch value of the note</param>
        /// <param name="instrument">the instrument being played</param>
        /// <param name="velocity">the volume of the note</param>
        private void TriggerNote(int channel, int note, int bank, int instrument, int velocity)
        {
            var voice = ActiveVoices[channel]; // get active voice for current channel (column)

            if (voice == null) // if no voice active in this channel
            {
                // TODO: move to own function
                SetBank(channel, bank);
                SetInstrument(channel, instrument);
                NoteOn(channel, note, velocity);
            }
            else
            {
                //Console.WriteLine($"Channel: {channel}, Note: {voice.Note}");
                // TODO: move to own function
                NoteOff(channel, voice.Note);
                SetBank(channel, bank);
                SetInstrument(channel, instrument);
                NoteOn(channel, note, velocity);
            }

            ActiveVoices[channel] = new Voice(note, bank, instrument, velocity); // create a voice and update ActiveVoices array
        }

        /// <summary>
        /// Method to trigger a row.
        /// </summary>
        private void TriggerRow()
        {
            if (Patterns[CurrentPattern] == null) // safety measure in case CurrentPattern is not defined
                return;

            var row = Patterns[CurrentPattern].Rows[CurrentRow]; // get current row

            // trigger each cell in the row
            foreach (var cell in row.Cells)
            {
                if (cell.Note >= 0)
                    TriggerNote(cell.Channel, cell.Note, cell.Bank, cell.Instrument, cell.Velocity);

                // TODO: effect parsing
            }
        }

        /// <summary>
        /// Method to calculate how many frames are left until the next tick.
        /// Frames refer to the number of audio samples
        /// </summary>
        public double GetFramesUntilNextTick()
        {
            double remaining = samplesPerTick - tickSampleCounter; // get the number of audio samples remaining
            int frames = (int)Math.Ceiling(remaining); // find the number of frames by rounding up the float value

            if (frames <= 0) // prevent infinite looping by returning 1 frame if the remaining frames is 0 or lower
                return 1;

            return frames;
        }

        /// <summary>
        /// Method to advance the tracker by a specific number of frames.
        /// Frames refer to the number of audio samples
        /// </summary>
        /// <param name="frames">the number of audio samples</param>
        public void Advance(int frames)
        {
            tickSampleCounter += frames; // update number of samples current tick has handled so far

            // keep advancing ticks until current tick's sample counter
            // exceeds the number of samples allotted to one tick
            while (tickSampleCounter >= samplesPerTick)
            {
                tickSampleCounter -= samplesPerTick; // reset tick sample counter
                AdvanceTick(); // advance to next tick
            }
        }

        /// <summary>
        /// Method to return the MIDI note being triggered.
        /// </summary>
        /// <param name="note">the MIDI pitch of the note</param>
        public static string CalculateMidiNote(int note)
        {
            string[] notes = ["C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-"];
            int octave = (int)Math.Floor(note / 12.0) - 1;
            return $"{notes[note % 12]}{octave}"; // return note in the form [note][octave], eg. C-4 or C#4
        }

        /// <summary>
        /// Method to return the instruments available in a given SoundFont.
        /// </summary>
        /// <param name="soundfont">the soundfont being considered</param>
        public static void GetInstrumentsInSoundFont(SoundFont soundfont)
        {
            IReadOnlyList<Preset> presets = soundfont.Presets;
            int maxLength = presets.Max(p => p.Name.Length); // get max length of instrument name for formatting

            // have to go backwards to show order than makes sense
            for (int i = presets.Count - 1; i >= 0; i--)
            {
                Console.WriteLine(
                    $"Name: {{0,-{maxLength}}} | Bank: {{1,-5}} | Patch: {{2,-5}}", // nice formatting
                    presets[i].Name,
                    presets[i].BankNumber,
                    presets[i].PatchNumber
                );
            }
        }

        /// <summary>
        /// Method to start a MIDI note.
        /// </summary>
        /// <param name="channel">the channel (or column) being considered</param>
        /// <param name="note">the MIDI pitch value of the note</param>
        /// <param name="velocity">the volume of the note</param>
        public void NoteOn(int channel, int note, int velocity)
        {
            //synthesizer.ProcessMidiMessage(channel, 0xB0, 10, 0); // TODO: implement panning effects (0-127)
            synthesizer.NoteOn(channel, note, velocity);
            //Console.WriteLine($"Played {CalculateMidiNote(note)} on channel {channel}");
        }

        /// <summary>
        /// Method to stop a MIDI note.
        /// </summary>
        /// <param name="channel">the channel (or column) being considered</param>
        /// <param name="note">the MIDI pitch value of the note</param>
        public void NoteOff(int channel, int note)
        {
            synthesizer.NoteOff(channel, note);
            //Console.WriteLine($"Stopped {CalculateMidiNote(note)} on channel {channel}");
        }

        /// <summary>
        /// Method to select the instrument which should be assigned to the MIDI channel.
        /// </summary>
        /// <param name="channel">the channel (or column) being considered</param>
        /// <param name="programNumber">the program number (instrument) in the SoundFont</param>
        public void SetInstrument(int channel, int programNumber)
        {
            synthesizer.ProcessMidiMessage(channel, 0xC0, programNumber, 0);
            //Console.WriteLine($"Loaded {soundfont.Presets[programNumber]} from {soundfont.Info.BankName} soundfont.");
        }

        public void SetBank(int channel, int bankNumber)
        {
            synthesizer.ProcessMidiMessage(channel, 0xB0, 0, bankNumber);  // Set the bank number
            //Console.WriteLine($"Loaded {soundfont.Presets[programNumber]} from {soundfont.Info.BankName} soundfont.");
        }

        private static void ProcessTickEffects()
        {
            // volume, vibrato, portamento, etc. here
        }

        public void TriggerNoteWithKeyboard(int channel, int note, int bank, int instrument, int velocity, bool trigger = false)
        {
            if (trigger) {
                Console.WriteLine($"ON: channel {channel}, note {note}, bank {bank}, instr {instrument}, vel {velocity}");
                SetBank(channel, bank);
                SetInstrument(channel, instrument);
                NoteOn(channel, note, velocity);
            }
            else
            {
                Console.WriteLine($"OFF: channel {channel}, note {note}, bank {bank}, instr {instrument}, vel {velocity}");
                NoteOff(channel, note);
            }
        }
    }
}
