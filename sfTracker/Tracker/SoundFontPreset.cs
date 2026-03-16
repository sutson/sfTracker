namespace sfTracker.Tracker
{
    /// <summary>
    /// Class for defining a SoundFont preset.
    /// </summary>
    public class SoundFontPreset
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int Bank { get; set; }
        public int Instrument { get; set; }
        public string DisplayID { get; set; }
        public string Display => $"{DisplayID}: {Name}";
    }
}
