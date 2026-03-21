using MeltySynth;
using sfTracker.Common;
using sfTracker.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace sfTracker.GUI
{
    /// <summary>
    /// Class which stores data which needs to be bound to GUI elements.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<SoundFontPreset> Presets { get; set; } = [];

        private SoundFontPreset selectedPreset;
        public SoundFontPreset SelectedPreset
        {
            get => selectedPreset;
            set
            {
                if (!ReferenceEquals(selectedPreset, value))
                {
                    selectedPreset = value;
                    OnPropertyChanged(nameof(SelectedPreset));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Method to load a SoundFont and store an array of presets contained within it.
        /// </summary>
        public ObservableCollection<SoundFontPreset> LoadSoundFont(string path)
        {
            var soundFont = new SoundFont(path);

            Presets.Clear();

            IReadOnlyList<Preset> presets = [.. soundFont.Presets.OrderBy(p => p.PatchNumber)];

            for (int i = 0; i < presets.Count; i++)
            {
                Presets.Add(new SoundFontPreset
                {
                    ID          = i,
                    Name        = presets[i].Name,
                    Bank        = presets[i].BankNumber,
                    Instrument  = presets[i].PatchNumber,
                    DisplayID   = i.ToString().PadLeft(3, '0'), // display ID makes SoundFonts display consistently
                });
            }

            SelectedPreset = Presets[0]; // set selected preset on load

            return Presets;
        }

        public ObservableCollection<TrackerColumn> Columns { get; } = [];

        /// <summary>
        /// Method to generate an array of tracker columns which are used for setting mute/solo status.
        /// </summary>
        public void GetColumns(int channelCount, double columnWidth)
        {
            for (int i = 0; i < channelCount; i++)
                Columns.Add(new TrackerColumn { Index = i, ColumnWidth = columnWidth });
        }

        /// <summary>
        /// Method to reset mute/solo statuses for each column.
        /// </summary>
        public void ResetColumns()
        {
            foreach (var column in Columns)
            {
                column.IsMuted = false;
                column.IsSolo = false;
            }
        }

        private int bpm = ProgramConstants.DefaultBPM;
        public int BPM
        {
            get => bpm;
            set
            {
                int clampedBPM = GetClampedValue(value, ProgramConstants.MinBPM, ProgramConstants.MaxBPM);
                if (bpm != clampedBPM)
                {
                    bpm = clampedBPM;
                    OnPropertyChanged(nameof(BPM));
                }
            }
        }

        private int speed = ProgramConstants.DefaultSpeed;
        public int Speed
        {
            get => speed;
            set
            {
                int clampedSpeed = GetClampedValue(value, ProgramConstants.MinSpeed, ProgramConstants.MaxSpeed);
                if (speed != clampedSpeed)
                {
                    speed = clampedSpeed;
                    OnPropertyChanged(nameof(Speed));
                }
            }
        }

        private int rowCount = ProgramConstants.DefaultRowCount;
        public int RowCount
        {
            get => rowCount;
            set
            {
                int clampedRowCount = GetClampedValue(value, ProgramConstants.MinRowCount, ProgramConstants.MaxRowCount);
                if (rowCount != clampedRowCount)
                {
                    rowCount = clampedRowCount;
                    OnPropertyChanged(nameof(RowCount));
                }
            }
        }

        private int rowHighlight = ProgramConstants.DefaultRowHighlight;
        public int RowHighlight
        {
            get => rowHighlight;
            set
            {
                int clampedRowHighlight = GetClampedValue(value, ProgramConstants.MinRowHighlight, ProgramConstants.MaxRowHighlight);
                if (rowHighlight != clampedRowHighlight)
                {
                    rowHighlight = clampedRowHighlight;
                    OnPropertyChanged(nameof(RowHighlight));
                }
            }
        }

        private string windowTitle = "sfTracker";
        public string WindowTitle
        {
            get => windowTitle;
            set
            {
                if (windowTitle != value)
                {
                    string displayName = value != "" ? $"{value} - " : value; // sfTracker default, [project name] - sfTracker if project has name
                    windowTitle = $"{displayName}sfTracker";
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        private static int GetClampedValue(int value, int minValue, int maxValue)
        {
            return Math.Clamp(value, minValue, maxValue);
        }

        /// <summary>
        /// Method to set view model data directly.
        /// </summary>
        public void SetViewModelData(int bpm, int speed, int rowCount, int rowHighlight, string title)
        {
            BPM = bpm;
            Speed = speed;
            RowCount = rowCount;
            RowHighlight = rowHighlight;
            WindowTitle = title;
        }
    }
}
