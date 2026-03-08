using System.ComponentModel;

namespace sfTracker.Tracker;

/// <summary>
/// Class which models a channel's settings in the tracker.
/// </summary>
public class TrackerColumn : INotifyPropertyChanged
{
    private bool isMuted; 
    private bool isSolo;
    public int Index { get; set; }
    public int DisplayIndex => Index + 1; // for "Channel X" display on column header
    public double ColumnWidth { get; set; }

    // bool indicating if channel should be muted
    public bool IsMuted
    {
        get => isMuted;
        set
        {
            isMuted = value;
            OnPropertyChanged(nameof(IsMuted));
        }
    }

    // bool indicating if channel should be solo
    public bool IsSolo
    {
        get => isSolo;
        set
        {
            isSolo = value;
            OnPropertyChanged(nameof(IsSolo));
        }
    }

    // simple event handler to change mute/solo statuses
    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}