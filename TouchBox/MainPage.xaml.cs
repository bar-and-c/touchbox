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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace TouchBox
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        Synthesizer _synth;
        private MidiOut[] _midiOutputs;
        int _selectedMidiDevice = 0;
        int _midiChannel = 7;

        public MainPage()
        {
            this.InitializeComponent();

            SynthPatches = new List<string>();
            SynthPatches.Add("apa");
            SynthPatches.Add("bepa");

            MidiSetup();

            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
        }


        void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            _synth = new Synthesizer();
        }

        void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _synth.Close();
        }

        private void KeyboardGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
//            Debug.WriteLine("{0} - PointerPressed", DateTime.Now.ToString("hh:mm:ss.fff"));
            FrameworkElement feSource = e.OriginalSource as FrameworkElement;
            string keyName = feSource.Name;
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

        private int GetLowestKeyFromName(string keyName)
        {
            string noteName = keyName.Substring(0, keyName.Length - 1);
            return _noteNumbers[noteName];
        }

        private void KeyboardGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
//            Debug.WriteLine("Released");
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

        private void DrumGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("{0} - DrumGrid_PointerPressed", DateTime.Now.ToString("hh:mm:ss.fff"));

        }

        private void KeyboardGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            FrameworkElement feSource = e.OriginalSource as FrameworkElement;
            string keyName = feSource.Name;
            int midiNote = -1;
            try
            {
                midiNote = GetMidiNoteFromName(keyName, midiNote);
            }
            catch (Exception) { /* Not a key*/ }
            if (midiNote != -1)
            {
                float pressure = e.GetCurrentPoint(feSource).Properties.Pressure;
                pressure = (pressure - 0.54f) * 2;
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


        #region MIDI stuff

        void MidiSetup()
        {
            int numDevices = MidiOut.NumberOfDevices;
            //Debug.WriteLine("Num devices: {0}", numDevices);
            _midiOutputs = new MidiOut[numDevices];
            for (int device = 0; device < numDevices; device++)
            {
              //  Debug.WriteLine("Device {0}: {1}", device, MidiOut.DeviceInfo(device).ProductName);
                _midiOutputs[device] = new MidiOut(device);
                Binding b = new Binding();
                b.Path = new PropertyPath("UseMidiSynth");
                b.Source = this;
                RadioButton r = new RadioButton()
                {
                    Content = MidiOut.DeviceInfo(device).ProductName,
                    IsChecked = (device == 0),
                };
                r.SetBinding(IsEnabledProperty, b);
                _midiSynthChoice.Children.Add(r);

                // Set up all MIDI channels to have different patches
                // TODO: But there are only 16 channels, RIGHT?
                for (int midiChannel = 0; midiChannel < 16; midiChannel++)
                {
                    try
                    {
                        int bank = 0;
                        int patch = 0;
                        switch (midiChannel)
                        {
                            /* Patches:
                             * Drums: http://www.voidaudio.net/percussion.html 
                             * Other: http://www.voidaudio.net/gsinstrument.html
                             */
                            case 0:
                                patch = 1; // Piano
                                break;
                            case 1:
                                bank = 17;
                                patch = 13; // Marimba
                                //  _midiOutputs[i].Send(MidiMessage.ChangeControl((int)MidiController.BankSelect, bank, j).RawData);
                                //    _midiOutputs[i].Send(MidiMessage.ChangeControl((int)MidiController.BankSelectLsb, 0, j).RawData);
                                break;
                            case 2:
                                patch = 39; // Synth bass
                                break;
                            case 5:
                                bank = 0; // A specific kind (distorted)
                                patch = 5; // Electric piano
                                /* TODO: I can't seem to select banks properly, OR not all GS patches exist on these synths. 
                                 * The theory is that bank select (at least MSB) should be done before patch change. 
                                 * If it doesn't work (or if MS GS is too limited), just find cool enough sounds... */
                                _midiOutputs[device].Send(MidiMessage.ChangeControl((int)MidiController.BankSelect, bank, midiChannel).RawData);
                                _midiOutputs[device].Send(MidiMessage.ChangeControl((int)MidiController.BankSelectLsb, bank, midiChannel).RawData);
                                break;
                            case 6:
                                patch = 85; // Synt!
                                break;
                            case 7:
                                patch = 51; // Synth strings
                                break;
                            case 8:
                                patch = 19; // organ
                                break;
                            case 9:
                                patch = 63; // synth brass
                                break;
                            case 10: // Drum channel
                                patch = 25;
                                // TODO: I have tried, but not succeeded, to get all drum kits. Not yet sure how it works.
                                break;
                            default:
                                patch = midiChannel;
                                break;
                        }
                        _midiOutputs[device].Send(MidiMessage.ChangePatch(patch, midiChannel).RawData);
                    }
                    catch (Exception e)
                    {
//                        Debug.WriteLine("Exception for device {0}, patch {1}: {2}", device, midiChannel, e.Message);
                        break;
                    }
                }
            }
        }

        private void MidiNoteOff(int midiNote, int midiChannel)
        {
            _midiOutputs[_selectedMidiDevice].Send(MidiMessage.StopNote(midiNote, 100, midiChannel).RawData);
        }

        private void MidiKeyAftertouch(int midiNote, int midiChannel, int amount)
        {
            _midiOutputs[_selectedMidiDevice].Send(KeyAftertouch(amount, midiNote, midiChannel).RawData);
        }

        private void MidiChannelAftertouch(int midiChannel, int amount)
        {
            _midiOutputs[_selectedMidiDevice].Send(ChannelAftertouch(amount, midiChannel).RawData);
        }

        private void MidiModulation(int midiChannel, int amount)
        {
//            Debug.WriteLine("MIDI Modulation: ch: {0}, amt: {1}", midiChannel, amount);
            _midiOutputs[_selectedMidiDevice].Send(MidiMessage.ChangeControl((int)MidiController.Modulation, amount, midiChannel).RawData);
        }

        private void MidiNoteOn(int midiNote, int midiChannel, int midiVelocity)
        {
//            Debug.WriteLine("MIDI note on, channel: {0} velocity: {1}", midiChannel, midiVelocity);
            _midiOutputs[_selectedMidiDevice].Send(MidiMessage.StartNote(midiNote, midiVelocity, midiChannel).RawData);
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
        private void _soundSourceSynth_Click(object sender, RoutedEventArgs e)
        {
            UseMidiSynth = false;

        }

        private void _soundSourceMIDI_Click(object sender, RoutedEventArgs e)
        {
            UseMidiSynth = true;

        }

        void UpdateSoundSource()
        {
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

        private void NUDButtonUP_Click(object sender, RoutedEventArgs e)
        {

        }

        private void NUDButtonDown_Click(object sender, RoutedEventArgs e)
        {

        }

        public List<String> SynthPatches { get; set; }
    }
}
