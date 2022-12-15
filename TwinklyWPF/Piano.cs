using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace TwinklyWPF
{
    public class Piano : IDisposable
    {
        MidiIn midiIn;
        MidiOut midiOut;
        bool monitoring;
        Stopwatch stopwatch = new Stopwatch();

        // cooked MIDI state

        public double[] Knobs = new double[8];

        NoteDownCounter[] chromaCount = new NoteDownCounter[12];
        const double chromaPowerDecay = -1.0;  // exponential decay. -5 => halflife: .14 s; -3 => .23 s; -1 => .69

        // track last bass note
        NoteState bassNote = new NoteState();
        const double bassBumpDecay = -4.0f;   // exponential decay factor. k=-40 ==> halflife 0.17s

        // track high notes
        public NoteState[] melodyNotesRing { get; } = new NoteState[12];
        int melodyNotesRingIndex = 0;

        // handlers can receive a keydown event after it's processed here
        public event EventHandler PianoKeyDownEvent;
        public class PianoKeyDownEventArgs : EventArgs
        {
            public NoteEvent NoteEvent;

            public PianoKeyDownEventArgs(NoteEvent evt)
            {
                this.NoteEvent = evt;
            }
        }

        public void Initialize()
        {

            // MIDI

            for (int device = 0; device < MidiIn.NumberOfDevices; device++)
            {
                App.Log(MidiIn.DeviceInfo(device).ProductName);
            }

            StartMonitoring();
        }

        public double CurrentTime => stopwatch.ElapsedMilliseconds * 0.001;
        public double BassBump
        {
            get
            {
                return (bassNote.note > 0 ?
                    // bassNote.velocity / (1.0f + bassNoteAge())    // slow decay: 1/(1+x)
                    decay(bassNote.velocity, bassNote.startTime, bassBumpDecay)         // exponential decay: e^-x. k=-40 ==> halflife 0.17s
                    : 0);
            }
        }

        public double decay(double initialValue, double startTime, double decayRate)
        {
            return initialValue * Math.Exp(decayRate * (CurrentTime - startTime));
        }

        public NoteState[] MelodyNotes => melodyNotesRing;

        //inline auto chromaCount() const { return m_chromaCount; }

        // returns one octave of values
        public double[] ChromaPower()
        {
            var t = CurrentTime;
            var chromaPower = new double[chromaCount.Length];
            for (int i = 0; i < 12; ++i)
            {
                chromaPower[i] = chromaCount[i].noteCount == 0
                    ? decay(chromaCount[i].velocity, chromaCount[i].startTime, chromaPowerDecay)
                    : 1.0f;
            }
            return chromaPower;
        }

        #region Start/Stop

        private void StartMonitoring()
        {
            if (MidiIn.NumberOfDevices <= 0)
            {
                MessageBox.Show("No MIDI input devices available");
                return;
            }

            if (midiIn != null)
            {
                midiIn.Dispose();
                midiIn.MessageReceived -= midiIn_MessageReceived;
                midiIn.ErrorReceived -= midiIn_ErrorReceived;
                midiIn = null;
            }

            midiIn = new MidiIn(0);
            midiIn.MessageReceived += midiIn_MessageReceived;
            midiIn.ErrorReceived += midiIn_ErrorReceived;

            midiIn.Start();
            monitoring = true;
            //buttonMonitor.Text = "Stop";
            //comboBoxMidiInDevices.Enabled = false;

            stopwatch.Restart();
        }

        void midiIn_ErrorReceived(object sender, MidiInMessageEventArgs e)
        {
            App.Log(String.Format("Time {0} Message 0x{1:X8} Event {2}",
                e.Timestamp, e.RawMessage, e.MidiEvent));
        }

        private void StopMonitoring()
        {
            if (monitoring)
            {
                midiIn.Stop();
                monitoring = false;
                //buttonMonitor.Text = "Monitor";
                //comboBoxMidiInDevices.Enabled = true;
            }
        }

        void midiIn_MessageReceived(object sender, MidiInMessageEventArgs e)
        {
            switch (e.MidiEvent.CommandCode)
            {
                case MidiCommandCode.NoteOn:
                    HandleMidiNoteOn((NoteEvent)e.MidiEvent);
                    break;

                case MidiCommandCode.NoteOff:
                    HandleMidiNoteOff((NoteEvent)e.MidiEvent);
                    break;

                case MidiCommandCode.ControlChange:
                    {
                        var evt = (ControlChangeEvent)e.MidiEvent;
                        int k = (int)evt.Controller;
                        // 14-17: pots  (mapping to Knobs[0-3])
                        // 3-6: sliders (mapping to Knobs[4-7])
                        if (k >= 14 && k <= 17)
                            Knobs[k - 14] = evt.ControllerValue / 127.0;
                        else if (k >= 3 && k <= 6)
                            Knobs[k + 1] = evt.ControllerValue / 127.0;
                        else
                            App.Log($"Controller: {evt.Controller} = {evt.ControllerValue}");
                    }
                    break;

                case MidiCommandCode.AutoSensing:
                case MidiCommandCode.PitchWheelChange:
                    break;

                case MidiCommandCode.Sysex:
                    App.Log(String.Format("Time {0} Message 0x{1:X8} Event {2}",
                        e.Timestamp, e.RawMessage, e.MidiEvent));
                    break;

                default:
                    App.Log(String.Format("Time {0} Message 0x{1:X8} Event {2}",
                        e.Timestamp, e.RawMessage, e.MidiEvent));
                    break;
            }
        }

        public double TimeOfLastNote = double.MinValue;

        void HandleMidiNoteOn(NoteEvent evt)
        {
            // pad toggles don't send note-off, instead they send another note-on with zero velocity
            if(evt.Velocity==0)
            {
                HandleMidiNoteOff(evt);
                return;
            }

            var t = CurrentTime;

            //previousNoteCount = noteCountSmoothed();
            //previousNoteCountTime = t;

            //m_notes[evt.NoteNumber] = evt.Velocity;
            //m_notesDown.insert(evt.NoteNumber);

            var c = evt.NoteNumber % 12;
            ++chromaCount[c].noteCount;
            chromaCount[c].velocity = evt.Velocity / 127.0f;
            chromaCount[c].startTime = t;

            // reset noteCountSmoothed--smoothing doesn't apply to noteCount increasing
            // always making certain that noteCountSmoothed never drops suddenly
            //if (previousNoteCount < m_notesDown.size())
            //{
            //    previousNoteCount = m_notesDown.size();
            //}

            if (evt.NoteNumber < PandaKeys.MiddleC())
            {
                bassNote.note = evt.NoteNumber;
                bassNote.velocity = evt.Velocity / 127.0f;
                bassNote.startTime = t;
            }
            else
            {
                // add high note 
                melodyNotesRing[melodyNotesRingIndex].note = evt.NoteNumber;
                melodyNotesRing[melodyNotesRingIndex].velocity = evt.Velocity / 127.0f;
                melodyNotesRing[melodyNotesRingIndex].startTime = t;
                melodyNotesRingIndex = (melodyNotesRingIndex + 1) % melodyNotesRing.Length;
            }
            //VERBOSE("[ ] %8lf: %4s %02x %02x\n", message.timestamp, msgTypeString, message[1], message[2]);

            if (PianoKeyDownEvent != null)
                PianoKeyDownEvent(this, new PianoKeyDownEventArgs(evt));

            TimeOfLastNote = t;
        }

        void HandleMidiNoteOff(NoteEvent evt)
        {
            var t = CurrentTime;

            //previousNoteCount = noteCountSmoothed();
            //previousNoteCountTime = t;

            //m_notes[evt.NoteNumber] = 0;
            //m_notesDown.erase(evt.NoteNumber);

            var c = evt.NoteNumber % 12;
            if (chromaCount[c].noteCount > 0)
                --chromaCount[c].noteCount;

            TimeOfLastNote = t;
        }

        #endregion

        #region IDispose

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    StopMonitoring();
                    //if (midiIn != null)
                    {
                        midiIn?.Dispose();
                        midiIn = null;
                    }
                    //if (midiOut != null)
                    {
                        midiOut?.Dispose();
                        midiOut = null;
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    public class PandaKeys
    {
        public static int LowC() { return C(0); }   // 0x30, lowest note
        public static int MiddleC() { return C(1); }   // 0x3C
        public static int HighC() { return C(2); }   // 0x48, highest note
        public static int C(int octave) { return 0x30 + 12 * octave; }     // C0 = 30
        public static int D(int octave) { return 0x1A + 12 * octave; }     // D0 = 1A
        public static int E(int octave) { return 0x1C + 12 * octave; }     // E0 = 1C
        public static int F(int octave) { return 0x1D + 12 * octave; }     // F0 = 1D
        public static int G(int octave) { return 0x1F + 12 * octave; }     // G0 = 1F
        public static int A(int octave) { return 0x21 + 12 * octave; }     // A0 = 21
        public static int Bf(int octave) { return 0x22 + 12 * octave; }
        public static int B(int octave) { return 0x23 + 12 * octave; }
    }


    [DebuggerDisplay("NoteDownCounter: {noteCount} {velocity} {startTime}")]
    public struct NoteDownCounter
    {
        public double noteCount;
        public double velocity;
        public double startTime;
    }

    [DebuggerDisplay("NoteState: {note} {velocity} {startTime}")]
    public struct NoteState
    {
        public NoteState() { }
        public int      note = 0;
        public double   velocity = 0;
        public double   startTime = 0;  // not the MIDI event time, but the local stopwatch time, in seconds
    }

}
