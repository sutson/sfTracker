using MeltySynth;
using NAudio.Wave;
using sfTracker.Tracker;
using System;

namespace sfTracker.Audio
{
    /// <summary>
    /// Class which instantiates NAudio output logic, allowing audio to be rendered
    /// </summary>
    public class MeltySynthWaveProvider(Synthesizer synthesizer, TrackerEngine tracker, int sampleRate) : IWaveProvider
    {
        private readonly Synthesizer synthesizer = synthesizer;
        private readonly TrackerEngine tracker = tracker;
        private readonly WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2); // instantiate PCM for audio playback
        private volatile bool isPlaying;

        private float[] floatBuffer = [];

        public void Start()
        {
            isPlaying = true;
        }

        public void Stop()
        {
            isPlaying = false;
        }

        public WaveFormat WaveFormat => waveFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            // calculate how many samples are required to render
            // count is the total number of bytes requested to render
            // this must be converted to float samples so
            // divide by sizeof(float) to convert bytes -> float samples needed
            int samplesRequired = count / sizeof(float);

            // divide by 2 to account for stereo (2 float samples needed for L and R channels)
            int framesRequired = samplesRequired / 2;

            // allocate enough space for the buffer if necessary
            // this only resizes the buffer if the number of samples requested
            // is more than the current size
            if (floatBuffer.Length < samplesRequired)
                floatBuffer = new float[samplesRequired];

            // render frames, then copy float buffer data back into byte buffer
            RenderAudio(framesRequired);
            Buffer.BlockCopy(floatBuffer, 0, buffer, offset, count);

            return count;
        }

        /// <summary>
        /// Method to handle exporting audio to a .wav file.
        /// </summary>
        public void ExportWav(string path)
        {
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels: 2); // instantiate PCM

            using var writer = new WaveFileWriter(path, waveFormat); // initialise the writer
            
            // create the float buffer
            const int bufferFrames = 1024;
            float[] floatBuffer = new float[bufferFrames * 2];

            synthesizer.Reset(); // reset the synthesiser before starting the render

            while (true)
            {
                RenderAudio(bufferFrames, writer: writer, isExport: true); // render audio as export

                if (tracker.SongHasFinished)
                    break;
            }
        }

        /// <summary>
        /// Method to handle audio rendering.
        /// </summary>
        private void RenderAudio(int framesRequired, WaveFileWriter writer = null, bool isExport = false)
        {
            int framesDone = 0; // track how many frames have been generated

            // keep rendering audio until all the frames are done
            while (framesDone < framesRequired)
            {
                int framesThisBlock;

                if (isPlaying || isExport)
                {
                    double framesUntilNextTick = tracker.GetFramesUntilNextTick(); // calculate frames until next tracker "tick"

                    // this calculation should ensure that timing boundaries are respected and audio doesn't become mistimed
                    // (framesRequired - framesDone) determines the number of frames still needed to complete this block
                    framesThisBlock = Math.Min((int)framesUntilNextTick, framesRequired - framesDone);

                    // update volumes of all channels inside Read()
                    // to ensure they are changed before rendering
                    tracker.UpdateAllChannelVolumes();
                    tracker.UpdateAllChannelPannings();
                }
                else
                {
                    framesThisBlock = framesRequired - framesDone; // freeze frames while not playing
                }

                // RenderInterleaved fills the float buffer with audio data
                // the multiplication by 2 is needed to account for stereo data
                // (framesDone * 2) is the starting index to write to floatBuffer
                // (framesThisBlock * 2) is the length of the data being written to floatBuffer this block
                synthesizer.RenderInterleaved(floatBuffer.AsSpan(framesDone * 2, framesThisBlock * 2));

                if (isPlaying || isExport)
                    tracker.Advance(framesThisBlock); // advance the tracker by the number of frames rendered

                framesDone += framesThisBlock; // update count for number of frames completed
            }

            // handle audio export file writing
            if (isExport)
                writer.WriteSamples(floatBuffer, 0, framesDone * 2);
        }
    }
}