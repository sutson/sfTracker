using MeltySynth;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using sfTracker.Tracker;
using System;

namespace sfTracker.Audio
{
    // <summary>
    /// Class which initialises the MeltySynth and NAudio functionality, allowing audio playback to be started or stopped
    /// </summary>
    public class SynthEngine : IDisposable
    {
        private Synthesizer synthesizer;
        private readonly TrackerEngine tracker;
        private readonly MeltySynthWaveProvider waveProvider;
        private readonly WasapiOut output;
        private bool isPlayingAudio;
        public TrackerEngine Tracker => tracker;

        public SynthEngine(string soundFontPath, int sampleRate = 44100)
        {
            // initialise SoundFont handler, tracker and WaveProvider
            synthesizer = new Synthesizer(new SoundFont(soundFontPath), sampleRate);
            //synthesizer.MasterVolume = 1.5f; // TODO: implement master volume slider or something so the volume can be louder
            tracker = new TrackerEngine(synthesizer, sampleRate);
            waveProvider = new MeltySynthWaveProvider(synthesizer, tracker, sampleRate);

            // initialise NAudio output
            output = new WasapiOut(AudioClientShareMode.Shared, true, 5);
            output.Init(waveProvider);
            output.Play();
            isPlayingAudio = false;
        }

        /// <summary>
        /// Method to start or stop the tracker.
        /// TODO: currently this just accepts keyboard input, obviously this should change.
        /// </summary>
        public void Start()
        {
            if (isPlayingAudio)
            {
                StopAudio();
                ResetTracker(); // reset tracker TODO: need to reset to the start of current pattern
            }
            else
            {
                PlayAudio();
            }
        }

        /// <summary>
        /// Method to begin audio playback via WaveOutEvent.
        /// </summary>
        public void PlayAudio()
        {
            //Console.WriteLine("Playing audio");
            waveProvider.Start();
            isPlayingAudio = true;
        }

        /// <summary>
        /// Method to stop audio playback via WaveOutEvent.
        /// </summary>
        public void StopAudio()
        {
            //Console.WriteLine("Stopping audio");
            StopActiveNotes(); // Ensure that all notes are properly turned off when stopping the audio
            waveProvider.Stop();
            isPlayingAudio = false;
        }

        /// <summary>
        /// Method to reset the tracker by moving back to the start of the pattern.
        /// TODO: need to reset to the start of the CURRENT pattern when multiple patterns exist
        /// </summary>
        public void ResetTracker()
        {
            tracker.CurrentRow = 0;  // reset row
            tracker.CurrentPattern = 0; // reset pattern
            tracker.CurrentTick = 0;  // reset tick counter
            tracker.tickSampleCounter = 0;  // reset sample counter

            synthesizer.Reset(); // reset MeltySynth Synthesizer
        }

        /// <summary>
        /// Method to stop all actively playing notes to prevent audio leaks when playing audio after stopping.
        /// </summary>
        private void StopActiveNotes()
        {
            for (int i = 0; i < tracker.ActiveVoices.Length; i++)
            {
                if (tracker.ActiveVoices[i] != null)
                    tracker.NoteOff(i, tracker.ActiveVoices[i].Note); // trigger NoteOff() for each active voice
            }
            
            Array.Clear(tracker.ActiveVoices, 0, tracker.ActiveVoices.Length); // clear the active voice array
        }

        public void Dispose()
        {
            output.Stop();
            output.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
