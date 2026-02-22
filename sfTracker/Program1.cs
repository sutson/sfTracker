using MeltySynth;
using MyTracker;
using sfTracker.Audio;
using sfTracker.Playback;

//var engine = new SynthEngine("Kirby's_Dream_Land_3.sf2");
////var engine = new SynthEngine("Live A Live Soundfont.sf2");
////engine.Tracker.GetInstrumentsInSoundFont(new SoundFont("Kirby's_Dream_Land_3.sf2"));

//var pattern  = new Pattern(rowCount: 4, channels: 4);
//var pattern2 = new Pattern(rowCount: 4, channels: 4);

//pattern.Rows[0].Cells[0] = new Cell { Channel = 0, Note = 60, Instrument = 44, Velocity = 100 };
////pattern.Rows[0].Cells[1] = new Cell { Channel = 1, Note = 64, Instrument = 2, Velocity = 100 };
////pattern.Rows[0].Cells[2] = new Cell { Channel = 2, Note = 64, Instrument = 6, Velocity = 100 };

////pattern.Rows[1].Cells[0] = new Cell { Channel = 0, Note = 64, Instrument = 7 };
////pattern.Rows[1].Cells[1] = new Cell { Channel = 1, Note = 67, Instrument = 2 };
////pattern.Rows[1].Cells[2] = new Cell { Channel = 2, Note = 67, Instrument = 6 };

////pattern.Rows[2].Cells[0] = new Cell { Channel = 0, Note = 67, Instrument = 7 };
////pattern.Rows[2].Cells[1] = new Cell { Channel = 1, Note = 72, Instrument = 2 };
////pattern.Rows[2].Cells[2] = new Cell { Channel = 2, Note = 72, Instrument = 6 };

////pattern.Rows[3].Cells[0] = new Cell { Channel = 0, Note = 72, Instrument = 7 };
////pattern.Rows[3].Cells[1] = new Cell { Channel = 1, Note = 76, Instrument = 2 };
////pattern.Rows[3].Cells[2] = new Cell { Channel = 2, Note = 76, Instrument = 6 };

//pattern2.Rows[0].Cells[0] = new Cell { Channel = 0, Note = 60, Instrument = 44, Velocity = 100 };
////pattern2.Rows[0].Cells[1] = new Cell { Channel = 1, Note = 72, Instrument = 2, Velocity = 100 };
////pattern2.Rows[0].Cells[2] = new Cell { Channel = 2, Note = 72, Instrument = 6, Velocity = 100 };

////pattern2.Rows[2].Cells[0] = new Cell { Channel = 0, Note = 72, Instrument = 7 };
////pattern2.Rows[2].Cells[1] = new Cell { Channel = 1, Note = 76, Instrument = 2 };
////pattern2.Rows[2].Cells[2] = new Cell { Channel = 2, Note = 76, Instrument = 6 };

////pattern.Rows[4].Cells[0] = new Cell { Channel = 0, Note = 67, Instrument = 7 };
////pattern.Rows[4].Cells[1] = new Cell { Channel = 1, Note = 72, Instrument = 2 };
////pattern.Rows[4].Cells[2] = new Cell { Channel = 2, Note = 72, Instrument = 12 };

////pattern.Rows[5].Cells[0] = new Cell { Channel = 0, Note = 64, Instrument = 7 };
////pattern.Rows[5].Cells[1] = new Cell { Channel = 1, Note = 67, Instrument = 2 };
////pattern.Rows[5].Cells[2] = new Cell { Channel = 2, Note = 67, Instrument = 12 };

//engine.Tracker.SetBPM(60);
//engine.Tracker.Patterns = [pattern, pattern2]; // TODO: allow multiple patterns to be triggered sequentially
//// TODO: also make it so a note which is held through multiple patterns isn't retriggered

//engine.Start();
//engine.Dispose();

//Console.ReadKey();                      // Keep app running

//var app = new App();
//app.InitializeComponent();
//app.Run();




//pattern = new Pattern(rowCount: 16, channels: 4);
//pattern.Rows[0].Cells[0] = new Cell { Channel = 0, Note = 60, Instrument = 2, Velocity = 100 };
//pattern.Rows[0].Cells[1] = new Cell { Channel = 1, Note = 62, Instrument = 2, Velocity = 100 };
//pattern.Rows[0].Cells[2] = new Cell { Channel = 2, Note = 64, Instrument = 2, Velocity = 100 };
//pattern.Rows[0].Cells[3] = new Cell { Channel = 3, Note = 67, Instrument = 2, Velocity = 100 };

//pattern.Rows[1].Cells[0] = new Cell { Channel = 0, Note = 60, Instrument = 2, Velocity = 100 };
//pattern.Rows[1].Cells[1] = new Cell { Channel = 1, Note = 62, Instrument = 2, Velocity = 100 };
//pattern.Rows[1].Cells[2] = new Cell { Channel = 2, Note = 64, Instrument = 2, Velocity = 100 };
//pattern.Rows[1].Cells[3] = new Cell { Channel = 3, Note = 67, Instrument = 2, Velocity = 100 };

//pattern.Rows[2].Cells[0] = new Cell { Channel = 0, Note = 60, Instrument = 2, Velocity = 100 };
//pattern.Rows[2].Cells[1] = new Cell { Channel = 1, Note = 62, Instrument = 2, Velocity = 100 };
//pattern.Rows[2].Cells[2] = new Cell { Channel = 2, Note = 64, Instrument = 2, Velocity = 100 };
//pattern.Rows[2].Cells[3] = new Cell { Channel = 3, Note = 67, Instrument = 2, Velocity = 100 };

//pattern.Rows[3].Cells[0] = new Cell { Channel = 0, Note = 60, Instrument = 2, Velocity = 100 };
//pattern.Rows[3].Cells[1] = new Cell { Channel = 1, Note = 62, Instrument = 2, Velocity = 100 };
//pattern.Rows[3].Cells[2] = new Cell { Channel = 2, Note = 64, Instrument = 2, Velocity = 100 };
//pattern.Rows[3].Cells[3] = new Cell { Channel = 3, Note = 67, Instrument = 2, Velocity = 100 };

//pattern.Rows[1].Cells[0] = new Cell { Channel = 0, Note = 62, Instrument = 44, Velocity = 100 };
//pattern.Rows[2].Cells[0] = new Cell { Channel = 0, Note = 64, Instrument = 44, Velocity = 100 };
//pattern.Rows[3].Cells[0] = new Cell { Channel = 0, Note = 66, Instrument = 44, Velocity = 100 };

//engine.Tracker.Patterns = [pattern];
