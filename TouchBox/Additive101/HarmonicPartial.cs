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


        /* TODO: THis is what the author says about getting sample rate: 
         * With WasapiOut at least I think you can get it just by asking for the WaveFormat before you call Init.
         * (It's been a while, but I think that's how it works from memory). By the way, best to ask NAudio questions
         * over at the CodePlex discussion site if possible (naudio.codeplex.com) 
         * 
         * I still haven't found how to get the device's preferred sample rate. 
         */
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

            ModulationAmplitude = 0; // TODO: Lots to do about modulation.
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

        public float ModulationAmplitude { get; set; }


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
                }
            }
        }

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;

            for (int n = 0; n < sampleCount; n++)
            {
                // Get envelope
                _envelopeGenerator.Process();

                // Get sine
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

                // Get amplitude
                float addedAmplitude = (ModulationAmplitude + RelativeAmplitude * (1 - ModulationAmplitude));
                float totalAmplitude = addedAmplitude * BaseAmplitude * _envelopeGenerator.GetOutput();

                // Set sample value
                buffer[n + offset] = totalAmplitude * sineValue;
            }

            return sampleCount;
        }


    }
}
