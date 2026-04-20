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
        /// </summary>
        public void Start(int currentPattern)
        {
            if (isPlayingAudio)
            {
                StopAudio();
            }
            else
            {
                PlayAudio();
                ResetTracker(currentPattern); // reset tracker
            }
        }

        /// <summary>
        /// Method to begin audio playback via WaveOutEvent.
        /// </summary>
        public void PlayAudio()
        {
            // isKeyPress determines if the tracker logic should start, or just the audio engine
            waveProvider.Start();
            isPlayingAudio = true;
        }

        /// <summary>
        /// Method to stop audio playback via WaveOutEvent.
        /// </summary>
        public void StopAudio()
        {
            StopActiveNotes(); // Ensure that all notes are properly turned off when stopping the audio
            waveProvider.Stop();
            isPlayingAudio = false;
        }

        /// <summary>
        /// Method to reset the tracker by moving back to the start of the pattern.
        /// </summary>
        public void ResetTracker(int currentPattern)
        {
            tracker.CurrentRow = 0;  // reset row
            tracker.CurrentPattern = currentPattern; // reset pattern
            tracker.CurrentTick = 0;  // reset tick counter
            tracker.tickSampleCounter = 0;  // reset sample counter
            tracker.SongHasFinished = false; // reset song state

            synthesizer.Reset(); // reset MeltySynth Synthesizer
            tracker.ResetChannelVolumes(); // reset channel volumes
            tracker.ResetChannelPannings(); // reset channel pannings
        }

        /// <summary>
        /// Method to stop all actively playing notes to prevent audio leaks when playing audio after stopping.
        /// </summary>
        public void StopActiveNotes()
        {
            for (int i = 0; i < tracker.ActiveVoices.Length; i++)
            {
                if (tracker.ActiveVoices[i] != null)
                    tracker.NoteOff(i, tracker.ActiveVoices[i].Note); // trigger NoteOff() for each active voice
            }
            
            Array.Clear(tracker.ActiveVoices, 0, tracker.ActiveVoices.Length); // clear the active voice array
        }

        /// <summary>
        /// Method to stop actively playing note in a specific channel.
        /// </summary>
        public void StopNoteInChannel(int channel)
        {
            Voice voice = tracker.ActiveVoices[channel];
            if (voice == null) { return; }
            tracker.NoteOff(channel, voice.Note); // trigger NoteOff() for each active voice
            tracker.ActiveVoices[channel] = null;
        }

        /// <summary>
        /// Method to export the current song as an audio file (.wav format).
        /// </summary>
        public void ExportAudio(string fileName)
        {
            // the tracker must be reset to the beginning before rendering the song.
            // the wave provider output is stopped to allow the song to export, then restarted
            ResetTracker(0);
            output.Stop();
            waveProvider.ExportWav(fileName);
            output.Play();
        }

        public void Dispose()
        {
            output.Stop();
            output.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
