using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sfTracker.Tracker
{
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
