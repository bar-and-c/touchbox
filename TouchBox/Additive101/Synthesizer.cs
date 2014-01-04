using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Win8.Wave.WaveOutputs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Additive101
{
    class Synthesizer
    {
        private List<SynthesizerVoice> _voices;
        private const int _numberOfVoices = 10; // TODO: Figure out a good way for polyphony later. 

        // TODO: Is it necessary to keep track of active voices? With one voice per key (or just one), maybe not, but in the long run I think so.
        private List<SynthesizerVoice> _activeVoices;
        private List<SynthesizerVoice> _availableVoices;
        private Object _lock;

        private MixingSampleProvider _sampleMixer;
        private SampleToWaveProvider _sampleToWaveProvider;

#if USE_WAVEOUT // Not possible in Windows Store App
        private WaveOut _waveOut;
#else
        private WasapiOutRT _waveOut;
#endif

        public Synthesizer()
        {
            _voices = new List<SynthesizerVoice>(_numberOfVoices);
            _activeVoices = new List<SynthesizerVoice>();
            _availableVoices = new List<SynthesizerVoice>();
            for (int i = 0; i < _numberOfVoices; i++)
            {
                SynthesizerVoice voice = new SynthesizerVoice();
                _voices.Add(voice);
                _availableVoices.Add(voice);
                // TODO: In a system with patches, the voices must be initialized as well. For now I hard code the timbre elsewhere.
            }

            _lock = new Object();

            InitializeNAudio();
        }

        private async void InitializeNAudio()
        {
            _sampleMixer = new MixingSampleProvider(_voices[0].SampleProviders);
            _sampleToWaveProvider = new SampleToWaveProvider(_sampleMixer);

            _waveOut = new WasapiOutRT(AudioClientShareMode.Shared, 10);
            //            _waveOut = new WaveOut(WaveCallbackInfo.FunctionCallback());

            await _waveOut.Init(_sampleToWaveProvider);
            _waveOut.Play();
        }

        public void Close()
        {
            _waveOut.Dispose();
        }

        float FrequencyFromMidiNote(int note)
        {
            // http://www.phys.unsw.edu.au/jw/notes.html
            return (float) Math.Pow(2, (note - 69) / 12.0f) * 440;
        }

        float AmplitudeFromMidiVelocity(int velocity)
        {
            return velocity / 128.0f;
        }

        public void NoteOn(int keyNote, int velocity)
        {
            float frequency = FrequencyFromMidiNote(keyNote);
            float amplitude = AmplitudeFromMidiVelocity(velocity);
            lock (_lock)
            {
                SynthesizerVoice availableVoice = GetAvailableVoice();
                availableVoice.NoteOn(frequency, amplitude);
                availableVoice.KeyNumber = keyNote;
            }
        }

        public void NoteOff(int keyNote)
        {
            lock (_lock)
            {
                SynthesizerVoice voice = GetActiveVoiceWithKey(keyNote);
                if (voice != null) // Otherwise it could've been kicked out by "too many fingers", lack of polyphony
                {
                    voice.NoteOff();
                    ReleaseActiveVoice(voice);
                }
            }
        }

        private void ReleaseActiveVoice(SynthesizerVoice voice)
        {
            lock (_lock)
            {
                if (_activeVoices.Contains(voice))
                {
                    _activeVoices.Remove(voice);
                    voice.NoteOff();
                    _availableVoices.Add(voice); // Put it last in the available list
                }
            }
        }

        private SynthesizerVoice GetActiveVoiceWithKey(int keyNote)
        {
            SynthesizerVoice voice = null;
            lock (_lock)
            {
                foreach (SynthesizerVoice v in _activeVoices)
                {
                    if (v.KeyNumber == keyNote)
                    {
                        voice = v;
                        break;
                    }
                }
            }

            return voice;
        }

        // void Aftertouch()

        // void Pitchbend()

        private SynthesizerVoice GetAvailableVoice()
        {
            SynthesizerVoice voice = null;
            lock (_lock)
            {
                if (_availableVoices.Count > 0)
                {
                    voice = _availableVoices[0];
                    _availableVoices.Remove(voice);
                    _activeVoices.Add(voice);
                }
                else
                {
                    // Get the first active voice, it's been in the list the longest
                    voice = _activeVoices[0];
                    _activeVoices.Remove(voice);
                    voice.NoteOff();
                    _activeVoices.Add(voice); // Put it last in list
                }
            }

            return voice;
        }


        internal void Modulate(int midiNote, float pressure)
        {
            System.Diagnostics.Debug.WriteLine("Synthesizer.Modulate({0})", pressure);
            int numberOfVoices = _voices.Count;

            for (int i = 0; i < numberOfVoices; i++)
            {
                _voices[i].Modulate(pressure);
            }
        }
    }
}
