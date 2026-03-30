using sfTracker.Audio;
using sfTracker.GUI;
using System.Collections.Generic;
using System.Linq;

namespace sfTracker.Helpers
{
    /// <summary>
    /// Class which stores some functions for handling clicks of the channel header buttons (mute/solo).
    /// </summary>
    public class ChannelSettings
    {
        public static int HandleChannelButtonClick(double x, double[] startPositions, int channelCount, double buttonSize)
        {
            for (int channel = 0; channel < channelCount; channel++)
            {
                // check if current channel's button is within the x bounds of click
                // if it is, return the channel number
                double xLowerBound = startPositions[channel];
                double xUpperBound = startPositions[channel] + buttonSize;
                if (x < xLowerBound || x > xUpperBound) { continue; }
                return channel;
            }

            // return -1 if no match found
            return -1;
        }

        public static void HandleMute(int channel, MainViewModel vm, SynthEngine engine)
        {
            // do nothing if invalid channel or if channel already muted
            if (channel == -1 || vm.Columns[channel].IsMuted) { return; }

            UpdateMuteStatues(channel, isMuted: true, vm, engine); // update mute statuses for channel
            engine.StopNoteInChannel(channel); // kill currently playing notes in channel
        }

        public static void HandleUnmute(int channel, MainViewModel vm, SynthEngine engine)
        {
            // do nothing if invalid channel or if channel not already muted
            if (channel == -1 || !vm.Columns[channel].IsMuted) { return; }

            UpdateMuteStatues(channel, isMuted: false, vm, engine); // update mute statuses for channel

            // if unmuting a channel which isn't already solo
            // remove solo from all channels
            if (!vm.Columns[channel].IsSolo)
                foreach (var column in vm.Columns) { column.IsSolo = false; }
        }

        public static void UpdateMuteStatues(int channel, bool isMuted, MainViewModel vm, SynthEngine engine)
        {
            vm.Columns[channel].IsMuted = isMuted; // update mute status
            engine.Tracker.ChannelMuteStatuses[channel] = isMuted; // update channel statuses (used for preventing playback if muted)
        }

        public static void HandleSolo(int channel, MainViewModel vm, SynthEngine engine, int channelCount)
        {
            // if channel not valid, do nothing
            if (channel == -1) { return; }

            // get list of channels which should be muted
            List<int> channelsToMute = [.. Enumerable.Range(0, channelCount).Where(x => x != channel)];

            if (vm.Columns[channel].IsSolo) // if channel is already solo
            {
                vm.Columns[channel].IsSolo = false; // remove solo setting
                for (int ch = 0; ch < channelCount; ch++) // for each channel, remove mute
                    HandleUnmute(ch, vm, engine);
            }
            else // if channel not solo
            {
                vm.Columns[channel].IsSolo = true; // solo it
                if (vm.Columns[channel].IsMuted) // if the channel is currently muted, unmute it
                    HandleUnmute(channel, vm, engine);
                foreach (int ch in channelsToMute) // for all other channels, mute them if they aren't already and remove solo value
                {
                    vm.Columns[ch].IsSolo = false;
                    HandleMute(ch, vm, engine);
                }
            }
        }
    }
}
