using System;
using MeltySynth;
using sfTracker.Playback;

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
        public int[] CurrentVolumes { get; set; } // initialise volume arrays
        public int[] TargetVolumes { get; set; }
        public int BPM { get; private set; } = 120; // TODO: change default BPM
        public int TicksPerBeat { get; private set; } = 24; // ticks per beat is arbitrary, but 24 has a lot of factors
        public int Speed { get; private set; } = 6; // TODO: change default Speed
        public int CurrentRow { get; set; }
        public int CurrentPattern { get; set; }
        public int CurrentTick { get; set; }
        public Pattern[] Patterns { get; set; } 
        public Voice[] ActiveVoices { get; set; }

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
        /// <param name="bpm">the desired BPM (beats per minute)</param>
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
        /// Method to reset channel volumes.
        /// This is done to reduce note attack being affected by restarts.
        /// </summary>
        public void ResetChannelVolumes()
        {
            // for each channel, update its volume to max value
            for (int channel = 0; channel < ActiveVoices.Length; channel++)
            {
                CurrentVolumes[channel] = 100; //TODO: max value is actually 127, make a function to map 0-100 -> 0-127
                TargetVolumes[channel] = 100;
                SetVolume(channel, 100);
            }
        }

        /// <summary>
        /// Method to update channel volumes smoothly.
        /// This prevents volume changes failing to render properly in real time.
        /// </summary>
        public void UpdateAllChannelVolumes()
        {
            // for each channel, update its volume
            for (int channel = 0; channel < ActiveVoices.Length; channel++)
            {
                int current = CurrentVolumes[channel]; // get selected channel's current volume
                int target = TargetVolumes[channel]; // get selected channel's target volume

                if (current == target) // do nothing if the volumes haven't changed
                    continue;

                int diff = target - current; // find difference between volumes

                if (Math.Abs(diff) < 2)
                    current = target; // if the difference between current and target is 0 or 1, simply set it
                else 
                    current += diff / 2; // otherwise gradually update the volume value based on the difference

                // set the volume of the current channel
                CurrentVolumes[channel] = current;
                SetVolume(channel, current);
            }
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
        /// <param name="bank">the bank associated with the instrument being played</param>
        /// <param name="instrument">the instrument being played</param>
        /// <param name="velocity">the volume of the note</param>
        private void TriggerNote(int channel, int note, int bank, int instrument, int velocity)
        {
            Voice voice = ActiveVoices[channel]; // get active voice for current channel (column)

            if (voice == null) // if no voice active in this channel
                HandleNoteTrigger(channel, note, bank, instrument, velocity);
            else
                HandleNoteTrigger(channel, note, bank, instrument, velocity, voice);

            ActiveVoices[channel] = new Voice(note, bank, instrument, velocity); // create a voice and update ActiveVoices array
        }

        /// <summary>
        /// Method to handle note being triggered. If an active voice exists, it is stopped here.
        /// </summary>
        /// <param name="channel">the channel (or column) being considered</param>
        /// <param name="note">the MIDI pitch value of the note</param>
        /// <param name="bank">the bank associated with the instrument being played</param>
        /// <param name="instrument">the instrument being played</param>
        /// <param name="velocity">the volume of the note</param>
        /// <param name="voice">the active voice (optional)</param>
        private void HandleNoteTrigger(int channel, int note, int bank, int instrument, int velocity, Voice voice = null)
        {
            // if voice exists, turn it off
            if (voice != null)
                NoteOff(channel, voice.Note);

            // play new note
            SetBank(channel, bank);
            SetInstrument(channel, instrument);
            NoteOn(channel, note, velocity);
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
                if (cell.Velocity >= 0)
                    TargetVolumes[cell.Channel] = cell.Velocity;
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
        /// Method to start a MIDI note.
        /// </summary>
        /// <param name="channel">the channel (or column) being considered</param>
        /// <param name="note">the MIDI pitch value of the note</param>
        /// <param name="velocity">the volume of the note</param>
        public void NoteOn(int channel, int note, int velocity)
        {
            //synthesizer.ProcessMidiMessage(channel, 0xB0, 10, 0); // TODO: implement panning effects (0-127)
            synthesizer.NoteOn(channel, note, velocity);
        }

        /// <summary>
        /// Method to stop a MIDI note.
        /// </summary>
        /// <param name="channel">the channel (or column) being considered</param>
        /// <param name="note">the MIDI pitch value of the note</param>
        public void NoteOff(int channel, int note)
        {
            synthesizer.NoteOff(channel, note);
        }

        /// <summary>
        /// Method to select the instrument which should be assigned to the MIDI channel.
        /// </summary>
        /// <param name="channel">the channel being considered</param>
        /// <param name="programNumber">the program number (instrument) in the SoundFont</param>
        public void SetInstrument(int channel, int programNumber)
        {
            synthesizer.ProcessMidiMessage(channel, 0xC0, programNumber, 0);
        }

        /// <summary>
        /// Method to select the bank which should be assigned to the MIDI channel.
        /// </summary>
        /// <param name="channel">the channel being considered</param>
        /// <param name="bankNumber">the bank number in the SoundFont</param>
        public void SetBank(int channel, int bankNumber)
        {
            synthesizer.ProcessMidiMessage(channel, 0xB0, 0, bankNumber);
        }

        /// <summary>
        /// Method to set the velocity (volume) of the MIDI channel.
        /// </summary>
        /// <param name="channel">the channel being considered</param>
        /// <param name="volume">the desired velocity (volume)</param>
        public void SetVolume(int channel, int volume)
        {
            synthesizer.ProcessMidiMessage(channel, 0xB0, 11, volume);
        }

        private static void ProcessTickEffects()
        {
            // volume, vibrato, portamento, etc. here
        }


        /// <summary>
        /// Method to trigger note temporarily. TODO: this doesn't work as expected, decide if should keep
        /// </summary>
        /// <param name="channel">the channel (or column) being considered</param>
        /// <param name="note">the MIDI pitch value of the note</param>
        /// <param name="bank">the bank associated with the instrument being played</param>
        /// <param name="instrument">the instrument being played</param>
        /// <param name="velocity">the volume of the note</param>
        /// <param name="trigger">optional bool determining whether to trigger the note or stop it</param>
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
