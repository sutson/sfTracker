using MeltySynth;
using sfTracker.Common;
using sfTracker.Controls;
using sfTracker.Playback;
using System;
using System.Collections.Generic;

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
        public PanEffect[] CurrentPannings { get; set; } // initialise panning arrays
        public PanEffect[] TargetPannings { get; set; }
        public int BPM { get; private set; } = 120; // TODO: change default BPM
        public int TicksPerBeat { get; private set; } = 24; // ticks per beat is arbitrary, but 24 has a lot of factors
        public int Speed { get; private set; } = 6; // TODO: change default Speed
        public int CurrentRow { get; set; }
        public int CurrentPattern { get; set; }
        public int CurrentTick { get; set; }
        public int EarlyStoppingIndex { get; set; } // variable is set when the row count is changed in the GUI, moves to next pattern when reached

        public List<Pattern> Patterns { get; set; } 
        public Voice[] ActiveVoices { get; set; }
        public bool[] ChannelMuteStatuses { get; set; }

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
                CurrentVolumes[channel] = ProgramConstants.MaxVolume;
                TargetVolumes[channel] = ProgramConstants.MaxVolume;
            }
        }

        /// <summary>
        /// Method to reset channel pannings.
        /// This is done to reduce note attack being affected by restarts.
        /// </summary>
        public void ResetChannelPannings()
        {
            // for each channel, update its panning to default value
            for (int channel = 0; channel < ActiveVoices.Length; channel++)
            {
                CurrentPannings[channel] = new PanEffect(direction: null, value: -1);
                TargetPannings[channel] = new PanEffect(direction: null, value: -1);
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
                    current += diff; // otherwise gradually update the volume value based on the difference

                // set the volume of the current channel
                CurrentVolumes[channel] = current;
                SetVolume(channel, current);
            }
        }

        /// <summary>
        /// Method to update channel pannings smoothly.
        /// This prevents panning changes failing to render properly in real time.
        /// </summary>
        public void UpdateAllChannelPannings()
        {
            // for each channel, update its panning
            for (int channel = 0; channel < ActiveVoices.Length; channel++)
            {
                PanEffect current = CurrentPannings[channel]; // get selected channel's current panning data
                PanEffect target = TargetPannings[channel]; // get selected channel's target panning data
                
                // do nothing if the panning has no values set
                if (current.Direction == null && target.Direction == null) { continue; }

                // do nothing if the current and target are the same
                if (current.Direction == target.Direction && current.Value == target.Value) { continue; }

                current.Direction = target.Direction; // update panning direction to target value

                int diff = current.Value - target.Value;
                if (Math.Abs(diff) < 2)
                    current.Value = target.Value; // if the difference between current and target is 0 or 1, simply set it
                else
                    current.Value -= diff; // otherwise gradually update the panning value based on the difference

                // set the panning of the current channel
                CurrentPannings[channel] = current;
                SetPanning(channel, current);
            }
        }

        /// <summary>
        /// Method for advancing forward one tick.
        /// </summary>
        private void AdvanceTick()
        {
            if (CurrentTick % TicksPerBeat == 0)
                TriggerRow(); // trigger row on each beat

            CurrentTick++; // advance to next tick

            // based on Speed, handle row change
            if (CurrentTick >= Speed)
            {
                CurrentTick = 0; // reset tick to 0
                CurrentRow++; // advance to next row
            }

            if (
                Patterns[CurrentPattern] != null &&
                (CurrentRow >= Patterns[CurrentPattern].RowCount || CurrentRow >= EarlyStoppingIndex)
            )
            {
                CurrentRow = 0; // loop back to the start if no rows left

                // reset to first pattern if at end of Patterns array, otherwise advance to next
                if (CurrentPattern == Patterns.Count - 1)
                    CurrentPattern = 0;
                else
                    CurrentPattern++;
            }
        }

        /// <summary>
        /// Method to trigger a note.
        /// </summary>
        private void TriggerNote(int channel, int note, int bank, int instrument, int velocity, PanEffect panning)
        {
            Voice voice = ActiveVoices[channel]; // get active voice for current channel (column)

            if (voice == null) // if no voice active in this channel
            {
                voice = new Voice(-1, -1, -1, -1); // hack to get the first note to play at full volume
                HandleNoteTrigger(channel, note, bank, instrument, velocity, panning, voice);
            }
            else
                HandleNoteTrigger(channel, note, bank, instrument, velocity, panning, voice);

            ActiveVoices[channel] = new Voice(note, bank, instrument, velocity); // create a voice and update ActiveVoices array
        }

        /// <summary>
        /// Method to handle note being triggered. If an active voice exists, it is stopped here.
        /// </summary>
        private void HandleNoteTrigger(int channel, int note, int bank, int instrument, int velocity, PanEffect panning, Voice voice)
        {
            if (ChannelMuteStatuses[channel]) { return; } // don't trigger notes if channel muted

            // if voice exists, turn it off
            // also if voice exists and stop note is present, turn off active voice
            if (voice != null || (voice != null && note == ProgramConstants.StopNote))
            {
                //if (note == voice.Note && instrument == voice.Instrument) { return; }; // don't retrigger if the same note is already playing

                NoteOff(channel, voice.Note);
                SetBank(channel, bank);
                SetInstrument(channel, instrument);
                SetPanning(channel, panning);
                NoteOn(channel, note, velocity);
            }

            // play note
            SetBank(channel, bank);
            SetInstrument(channel, instrument);
            SetPanning(channel, panning);
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
                int scaledVelocity = (int)(cell.Velocity * ProgramConstants.MaxVolume / ProgramConstants.MaxDisplayVolume);
                PanEffect scaledPanning = new PanEffect(
                    direction: cell.Panning.Direction,
                    value: (int)(cell.Panning.Value * ProgramConstants.DefaultPanning / ProgramConstants.MaxDisplayPanning)
                );

                if (scaledVelocity >= 0)
                    TargetVolumes[cell.Channel] = scaledVelocity;
                if (scaledPanning.Value >= 0)
                    TargetPannings[cell.Channel] = scaledPanning;
                if (cell.Note >= 0 || cell.Note == ProgramConstants.StopNote)
                    TriggerNote(cell.Channel, cell.Note, cell.Bank, cell.Instrument, scaledVelocity, scaledPanning);

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
        public static string CalculateMidiNote(int note)
        {
            string[] notes = ["C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-"];
            int octave = (int)Math.Floor(note / 12.0);
            return $"{notes[note % 12]}{octave}"; // return note in the form [note][octave], eg. C-4 or C#4
        }

        /// <summary>
        /// Method to start a MIDI note.
        /// </summary>
        public void NoteOn(int channel, int note, int velocity)
        {
            synthesizer.NoteOn(channel, note, velocity);
        }

        /// <summary>
        /// Method to stop a MIDI note.
        /// </summary>
        public void NoteOff(int channel, int note)
        {
            synthesizer.NoteOff(channel, note);
        }

        /// <summary>
        /// Method to select the instrument which should be assigned to the MIDI channel.
        /// </summary>
        public void SetInstrument(int channel, int programNumber)
        {
            synthesizer.ProcessMidiMessage(channel, 0xC0, programNumber, 0);
        }

        /// <summary>
        /// Method to select the bank which should be assigned to the MIDI channel.
        /// </summary>
        public void SetBank(int channel, int bankNumber)
        {
            synthesizer.ProcessMidiMessage(channel, 0xB0, 0, bankNumber);
        }

        /// <summary>
        /// Method to set the velocity (volume) of the MIDI channel.
        /// </summary>
        public void SetVolume(int channel, int volume)
        {
            synthesizer.ProcessMidiMessage(channel, 0xB0, 11, volume);
        }

        /// <summary>
        /// Method to set the panning of the MIDI channel.
        /// 0 = panned fully left
        /// 127 = panned fully right
        /// </summary>
        public void SetPanning(int channel, PanEffect panning)
        {
            if (panning.Value < 0) { return; }
            int panningAmount = panning.Value * (panning.Direction == EffectType.PanningLeft ? -1 : 1);
            synthesizer.ProcessMidiMessage(channel, 0xB0, 10, (int)(ProgramConstants.DefaultPanning + panningAmount));
        }

        /// <summary>
        /// Method to trigger note temporarily. TODO: this doesn't work as expected, decide if should keep
        /// </summary>
        public void TriggerNoteWithKeyboard(int channel, int note, int bank, int instrument, int velocity, bool trigger = false)
        {
            if (trigger) {
                SetBank(channel, bank);
                SetInstrument(channel, instrument);
                NoteOn(channel, note, velocity);
            }
            else
            {
                NoteOff(channel, note);
            }
        }
    }
}
