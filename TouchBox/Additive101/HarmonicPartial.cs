#define USE_WAVEFORM_LOOKUP_TABLE

using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Additive101
{
    class HarmonicPartial : WaveProvider32
    {
        public EnvelopeGenerator _envelopeGenerator;

        private int _partialNumber;

#if USE_WAVEFORM_LOOKUP_TABLE
        // See http://stackoverflow.com/questions/13466623/how-to-look-up-sine-of-different-frequencies-from-a-fixed-sized-lookup-table
        private static int _tableSize = 48000;
        private static float[] _sineLookupTable;
        static HarmonicPartial()
        {
            _sineLookupTable = new float[_tableSize];
            for (int i = 0; i < _tableSize; ++i)
            {
                _sineLookupTable[i] = (float)Math.Sin(2.0f * Math.PI * (float)i / (float)_tableSize);
            }
        }

        private float _phaseIncrement;
        private float _phaseAccumulator = 0.0f;
#else
        private int _sample;
#endif // USE_WAVEFORM_LOOKUP_TABLE 


        public HarmonicPartial(int partial, float attack, float decay, float sustain, float release)
            : base(48000, 1) // TODO: This sets the sample rate, which should be what the sound card provides, but HOW to know that? 
        {
            _partialNumber = partial;
            _envelopeGenerator = new EnvelopeGenerator() 
            {
                AttackRate = attack, 
                DecayRate = decay, 
                SustainLevel = sustain, 
                ReleaseRate = release
            };


            Frequency = 200;
            BaseAmplitude = 0.3f;
        }

        private float _frequency;
        public float Frequency 
        {
            get
            {
                return _frequency;
            }
            set
            {
                if (value != _frequency)
                {
                    _frequency = value;
#if USE_WAVEFORM_LOOKUP_TABLE
                    _phaseIncrement = (float)_frequency / WaveFormat.SampleRate * _tableSize;
#endif
                }
            }
        }

        public float BaseAmplitude { get; set; }

        public float RelativeAmplitude { get; set; }


        private bool _gate;
        public bool Gate 
        {
            get
            {
                return _gate;
            }

            set
            {
                if (value != _gate)
                {
                    _gate = value;
                    _envelopeGenerator.Gate(_gate);

#if DO_DEBUG_DUMP
                    if (!_gate && (_debugDataIndex > 0))
                    {
                        Console.WriteLine("Dumping debug data");
                        using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\Users\hakan.CORP\Desktop\testdata.txt"))
                        {
                            file.WriteLine("{0,-10}\t{1,-15}\t{2,-15}\t{3,-15}", "n", "ampl.", "sine", "buffered");
                            for (int i = 0; i < _debugDataIndex; i++)
                            {
                                DebugData d = _debugData[i];
                                file.WriteLine("{0,-10}\t{1,-15}\t{2,-15}\t{3,-15}", d.sample.ToString(), d.amplitude.ToString(), d.sine.ToString(), d.buffered.ToString());
                            }
                        }
                    }
#endif
                }
            }
        }

#if DO_DEBUG_DUMP
        private class DebugData
        {
            public int sample;
            public float amplitude;
            public float sine;
            public float buffered;
        }
        private const int _debugDataMax = 44100 * 60;
        private DebugData[] _debugData = new DebugData[_debugDataMax];
        private int _debugDataIndex = 0;
        private int _wrappedTimes = 0;
#endif
        bool _haveDoneTimeLog = false;
        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;
            if (_partialNumber == 1)
            {
//                Debug.WriteLine("{0} - Read is called", DateTime.Now.ToString("hh:mm:ss.fff"));
            }
            for (int n = 0; n < sampleCount; n++)
            {
                _envelopeGenerator.Process();
#if USE_WAVEFORM_LOOKUP_TABLE
                int phase_i = (int)_phaseAccumulator;        // get integer part of our phase
                float sineValue = _sineLookupTable[phase_i];          // get sample value from LUT

                _phaseAccumulator += _phaseIncrement;              // increment phase
                if (_phaseAccumulator >= (float)_tableSize)    // handle wraparound
                    _phaseAccumulator -= (float)_tableSize;
#else
                float sineValue = (float)Math.Sin((2 * Math.PI * _sample * Frequency) / sampleRate);

                _sample++;
                if (_sample >= sampleRate)
                {
                    /* TODO: Resetting _sample here seems like a mistake to me. When it wraps it introduces a discontinuity.
                     * I disable it for now. Will probably find another solution anyway.
                     */
                  //  _sample = 0;
#if DO_DEBUG_DUMP
                    _wrappedTimes++;
#endif
                }

#endif // USE_WAVEFORM_LOOKUP_TABLE

                float totalAmplitude = RelativeAmplitude * BaseAmplitude * _envelopeGenerator.GetOutput();
                buffer[n + offset] = totalAmplitude * sineValue;


#if DO_DEBUG_DUMP
                if (_gate && (_partialNumber == 1) && (_debugDataIndex < _debugDataMax) && (_wrappedTimes < 3))
                {
                    _debugData[_debugDataIndex] = new DebugData() { sample = _sample, amplitude = totalAmplitude, buffered = buffer[n + offset], sine = sineValue };
                    _debugDataIndex++;
                }
#endif
            }

            if (!_haveDoneTimeLog && (_partialNumber == 1) && (_envelopeGenerator.GetOutput() > 0))
            {
                _haveDoneTimeLog = true;
                Debug.WriteLine("{0} - First sound, buffer size: {1}", DateTime.Now.ToString("hh:mm:ss.fff"), sampleCount);
            }
            return sampleCount;
        }

    }
}
