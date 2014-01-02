using Additive101;
using System;
using System.Collections.Generic;
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
    public sealed partial class MainPage : Page
    {
        Synthesizer _synth; 

        public MainPage()
        {
            this.InitializeComponent();

            _synth = new Synthesizer();
        }

        private void KeyboardGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("{0} - PointerPressed", DateTime.Now.ToString("hh:mm:ss.fff"));
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
                _synth.NoteOn(midiNote, 100);
            }

#if apa
            switch (feSource.Name)
            {
                case "Ciss1":
                    _synth.NoteOn(49, 100);
                    break;
                case "secondTextBlock":
                    break;
                case "thirdTextBlock":
                    break;
            }
#endif
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
                _synth.NoteOff(midiNote);
            }
        }

        private void DrumGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("{0} - DrumGrid_PointerPressed", DateTime.Now.ToString("hh:mm:ss.fff"));

        }
    }
}
