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
        private const int _numberOfVoices = 1; // TODO: Figure out a good way for polyphony later. 

        // TODO: Is it necessary to keep track of active voices? With one voice per key (or just one), maybe not, but in the long run I think so.
        private Dictionary<int, SynthesizerVoice> _activeVoices;
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
            for (int i = 0; i < _numberOfVoices; i++)
            {
                _voices.Add(new SynthesizerVoice());
                // TODO: In a system with patches, the voices must be initialized as well. For now I hard code the timbre elsewhere.
            }

            _activeVoices = new Dictionary<int, SynthesizerVoice>();
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
                _activeVoices[keyNote] = availableVoice;
            }
        }

        public void NoteOff(int keyNote)
        {
            lock (_lock)
            {
                if (_activeVoices.ContainsKey(keyNote)) // Otherwise it could've been kicked out by "too many fingers", lack of polyphony
                {
                    SynthesizerVoice availableVoice = _activeVoices[keyNote];
                    availableVoice.NoteOff();
                    _activeVoices.Remove(keyNote);
                }
            }
        }

        // void Aftertouch()

        // void Pitchbend()

        private SynthesizerVoice GetAvailableVoice()
        {
            // TODO: When more than one voice, remove the unlucky voice form _activeVoices
            return _voices[0];
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
