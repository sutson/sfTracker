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
            // if audio isn't playing, clear the buffer
            if (!isPlaying)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

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

            int framesDone = 0; // track how many frames have been generated
            // keep rendering audio until all the frames are done
            while (framesDone < framesRequired)
            {
                double framesUntilNextTick = tracker.GetFramesUntilNextTick(); // calculate frames until next tracker "tick"

                // this calculation should ensure that timing boundaries are respected and audio doesn't become mistimed
                // (framesRequired - framesDone) determines the number of frames still needed to complete this block
                int framesThisBlock = Math.Min((int)framesUntilNextTick, framesRequired - framesDone);

                // update volumes of all channels inside Read()
                // to ensure they are changed before rendering
                tracker.UpdateAllChannelVolumes();

                // RenderInterleaved fills the float buffer with audio data
                // the multiplication by 2 is needed to account for stereo data
                // (framesDone * 2) is the starting index to write to floatBuffer
                // (framesThisBlock * 2) is the length of the data being written to floatBuffer this block
                synthesizer.RenderInterleaved(floatBuffer.AsSpan(framesDone * 2, framesThisBlock * 2));

                tracker.Advance(framesThisBlock); // advance the tracker by the number of frames rendered

                framesDone += framesThisBlock; // update count for number of frames completed
            }

            Buffer.BlockCopy(floatBuffer, 0, buffer, offset, count); // copy float buffer data back into byte buffer
            
            return count;
        }
    }
}