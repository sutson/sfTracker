using MeltySynth;
using sfTracker.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace sfTracker.GUI
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<SoundFontPreset> Presets { get; set; } = [];

        private SoundFontPreset _selectedPreset;
        public SoundFontPreset SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset != value)
                {
                    _selectedPreset = value;
                    OnPropertyChanged(nameof(SelectedPreset));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void LoadSoundFont(string path)
        {
            var soundFont = new SoundFont(path);

            Presets.Clear();

            IReadOnlyList<Preset> presets = [.. soundFont.Presets.OrderBy(p => p.PatchNumber)];

            for (int i = 0; i < presets.Count; i++)
            {
                Presets.Add(new SoundFontPreset
                {
                    Name        = presets[i].Name,
                    DisplayID   = i,
                    Bank        = presets[i].BankNumber,
                    Instrument  = presets[i].PatchNumber
                });
            }
        }
    }
}
