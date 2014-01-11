using Additive101;
using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


/* TODO: 
 * - There is a problem with hanging notes; if you press a key and let your finger slide out of the key area the 
 * PointerExited event handler is called, but at that point there's no Name property to determine the key, so no 
 * "note off" method is called. 
 * 
 * - I think there are cases where some "all notes off" should be called, e.g. if the sound generation is changed 
 * while pressing a key. 
 */

namespace TouchBox
{

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        Synthesizer _synth;
        string _selectedMidiDevice;
        private Dictionary<string, MidiOut> _midiDevices;

        // For now, only use MIDI channel 1
        int _midiChannel = 1;

        private Dictionary<string, int> _noteNumbers = new Dictionary<string, int>() 
        {
            {"C",36},
            {"Ciss",37},
            {"D",38},
            {"Diss",39},
            {"E",40},
            {"F",41},
            {"Fiss",42},
            {"G",43},
            {"Giss",44},
            {"A",45},
            {"Aiss",46},
            {"B",47},
        };

        private bool _useMidiSynth;
        public bool UseMidiSynth
        {
            get { return _useMidiSynth; }
            set
            {
                if (value != _useMidiSynth)
                {
                    _useMidiSynth = value;
                    OnPropertyChanged("UseMidiSynth");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }


        public MainPage()
        {
            this.InitializeComponent();

            MidiSetup();

            // TODO: Trying to open/close synth when opening/closing app, but I fear this is the wrong way in Windows 8 (Store app)
            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;

        }


        void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            _synth = new Synthesizer();

            _soundSourceMIDI.IsChecked = true;
            UseMidiSynth = true;
        }

        void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _synth.Close();
        }


        private void _soundSourceSynth_Click(object sender, RoutedEventArgs e)
        {
            UseMidiSynth = false;
        }

        private void _soundSourceMIDI_Click(object sender, RoutedEventArgs e)
        {
            UseMidiSynth = true;
        }

        void RadiobuttonChecked(object sender, RoutedEventArgs e)
        {
            RadioButton r = (RadioButton)sender;
            _selectedMidiDevice = (string)r.Content;
        }


        private void KeyboardGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            FrameworkElement feSource = e.OriginalSource as FrameworkElement;
            string keyName = feSource.Name;

            if (string.IsNullOrEmpty(keyName))
                return;

            int midiNote = -1;
            try
            {
                midiNote = GetMidiNoteFromName(keyName, midiNote);
            }
            catch (Exception) { /* Not a key*/ }

            if (midiNote != -1)
            {
                float pressure = e.GetCurrentPoint(feSource).Properties.Pressure;
                if (UseMidiSynth)
                {
                    MidiNoteOn(midiNote, _midiChannel, MidiControlValueFromFlatfrogPressure(pressure));
                }
                else
                {
                    _synth.NoteOn(midiNote, (int)(pressure * 127));
                }
            }
        }


        private void KeyboardGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            FrameworkElement feSource = e.OriginalSource as FrameworkElement;
            string keyName = feSource.Name;
            if (string.IsNullOrEmpty(keyName))
                return; 

            // TODO: When we get here from "PointerExited" there's no name, and hence no key number.

            int midiNote = -1;
            try
            {
                midiNote = GetMidiNoteFromName(keyName, midiNote);
            }
            catch (Exception) { /* Not a key*/ }
            if (midiNote != -1)
            {
                if (UseMidiSynth)
                {
                    MidiNoteOff(midiNote, _midiChannel);
                }
                else
                {
                    _synth.NoteOff(midiNote);
                }
            }
        }


        private void KeyboardGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            FrameworkElement feSource = e.OriginalSource as FrameworkElement;
            string keyName = feSource.Name;

            if (string.IsNullOrEmpty(keyName))
                return;

            int midiNote = -1;
            try
            {
                midiNote = GetMidiNoteFromName(keyName, midiNote);
            }
            catch (Exception) { /* Not a key*/ }
            if (midiNote != -1)
            {
                float pressure = e.GetCurrentPoint(feSource).Properties.Pressure;
                pressure = (pressure - 0.54f) * 2.3f;
                pressure = Math.Min(1, pressure);
                pressure = Math.Max(0, pressure);
                if (UseMidiSynth)
                {
                    MidiModulation(_midiChannel, MidiControlValueFromFlatfrogPressure(pressure));
                }
                else
                {
                    _synth.Modulate(midiNote, pressure);
                }
            }
        }

        // TODO: Not yet implemented
        private void DrumGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("{0} - DrumGrid_PointerPressed", DateTime.Now.ToString("hh:mm:ss.fff"));

        }

        private int GetMidiNoteFromName(string keyName, int midiNote)
        {
            int lowestKeyNumber = GetLowestKeyFromName(keyName);
            int octave = GetOctaveFromName(keyName);
            midiNote = lowestKeyNumber + (octave - 1) * 12;
            return midiNote;
        }

        private int GetOctaveFromName(string keyName)
        {
            string octaveString = keyName.Substring(keyName.Length - 1, 1);
            return Int32.Parse(octaveString);
        }


        private int GetLowestKeyFromName(string keyName)
        {
            string noteName = keyName.Substring(0, keyName.Length - 1);
            return _noteNumbers[noteName];
        }


        #region MIDI stuff

        void MidiSetup()
        {
            int numDevices = MidiOut.NumberOfDevices;
            _midiDevices = new Dictionary<string, MidiOut>();

            for (int device = 0; device < numDevices; device++)
            {
                // Keep track of devices
                string deviceName = MidiOut.DeviceInfo(device).ProductName;
                _midiDevices[deviceName] = new MidiOut(device);
                if (device == 0)
                {
                    _selectedMidiDevice = deviceName;
                }

                // Add radiobutton for multiple MIDI devices (if any)
                /* TODO: This implementation is not tested, I haven't been able to detect another MIDI device in this 
                 * Windows Store app (similar code detected third party MIDI synth SW and loopback device in a Desktop app). */
                Binding b = new Binding();
                b.Path = new PropertyPath("UseMidiSynth");
                b.Source = this;
                RadioButton r = new RadioButton()
                {
                    Content = deviceName,
                    IsChecked = (device == 0),
                };
                r.Checked += RadiobuttonChecked;
                r.SetBinding(IsEnabledProperty, b);
                _midiSynthChoice.Children.Add(r);

                // For now, set all up for synth string sound (patch 51, according to General MIDI)
                _midiDevices[deviceName].Send(MidiMessage.ChangePatch(51, _midiChannel).RawData);

            }
        }

        private void MidiNoteOff(int midiNote, int midiChannel)
        {
            _midiDevices[_selectedMidiDevice].Send(MidiMessage.StopNote(midiNote, 100, midiChannel).RawData);
        }

        private void MidiKeyAftertouch(int midiNote, int midiChannel, int amount)
        {
            _midiDevices[_selectedMidiDevice].Send(KeyAftertouch(amount, midiNote, midiChannel).RawData);
        }

        private void MidiChannelAftertouch(int midiChannel, int amount)
        {
            _midiDevices[_selectedMidiDevice].Send(ChannelAftertouch(amount, midiChannel).RawData);
        }

        private void MidiModulation(int midiChannel, int amount)
        {
            _midiDevices[_selectedMidiDevice].Send(MidiMessage.ChangeControl((int)MidiController.Modulation, amount, midiChannel).RawData);
        }

        private void MidiNoteOn(int midiNote, int midiChannel, int midiVelocity)
        {
            _midiDevices[_selectedMidiDevice].Send(MidiMessage.StartNote(midiNote, midiVelocity, midiChannel).RawData);
        }



        // NAudio Extension:
        public static MidiMessage ChannelAftertouch(int amount, int channel)
        {
            return new MidiMessage((int)MidiCommandCode.ChannelAfterTouch + channel - 1, amount, 0);
        }
        public static MidiMessage KeyAftertouch(int amount, int key, int channel)
        {
            return new MidiMessage((int)MidiCommandCode.KeyAfterTouch + channel - 1, key, amount);
        }

        private static int MidiControlValueFromFlatfrogPressure(float pressure)
        {
            int outMax = 127;
            int amount = (int)(outMax * pressure);
            return amount;
        }

        #endregion


    }
}
