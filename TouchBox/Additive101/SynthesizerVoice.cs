#undef USE_PURE_DATA_INSTEAD_OF_HOMEBREW

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

#if USE_PURE_DATA_INSTEAD_OF_HOMEBREW
using LibPDBinding;
#endif

/*
 * TODO: 
 * Would like to add some sort of modulation of partials, of course. In a clever way. 
 * Ideas: 
 *  a) A new enum, like WaveShape but with modulation strategies, like "Square", "Tubedist", <insert other clever ideas>,
 *     so that a change in modulation affects a particular set of partials in a particular way.
 *  b) Morphing between WaveShapes, by ADSR or pressure. E.g. if the WaveShape of today instead is "InitialShape", let
 *     there be another WaveShape, say "ModulatedShape", and that a change in modulation moves the shape in that direction. 
 *     E.g. if we have a Square and modulate towards a Sine, increased pressure weighs in more of the Sine's relative amplitude 
 *     per partial and less of Square's; like modRelAmp[i] = (Square[i] * (1 - mod) + Sine[i] * mod) / 2.
 *     Or in more general terms: ActualRelativeAmplitude[i] = (InitialRelativeAmplitude[i] * (1 - mod) + ModulatedRelativeAmplitude[i] * mod) / 2
 *  c) More complex envelopes than ADSR. Is it possible to chain them? Or simpler to re-write the NAudio code? 
 *  d) Use X/Y-position for modulation as well, at least subtly. And/or in combination with multiple touch.
 */
namespace Additive101
{
    public enum WaveShapes
    {
        Sine,
        Square,
        Saw,
        Triangle
    };

#if USE_PURE_DATA_INSTEAD_OF_HOMEBREW
    class SynthesizerVoice : WaveProvider32
    {
#else
    class SynthesizerVoice
    {
        private List<HarmonicPartial> _partials;
#endif
        private const int _numberOfPartials = 20;

        private WaveShapes _shape;

        /* http://www.kvraudio.com/forum/printview.php?t=286235&start=15 :
            Sine = just the first partial 
            Saw = all partials up to infinity with decreasing amplitude, e.g partial two has 1/2 amplitude of partial one, partial three has 1/3 amplitude of partial 1 etc. 
            square = all odd numbered partials up to infinity, e.g 1, 3, 5 etc, with decreasing amplitude as saw 
            triangle = As square but amplitude of partials decreases much more quickly 
         */
        private static Dictionary<WaveShapes, float[]> _amplitudeRelations = new Dictionary<WaveShapes, float[]>
        {
            {WaveShapes.Sine, new float[_numberOfPartials] {1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}},
            {WaveShapes.Square, new float[_numberOfPartials] {1, 0, 1/3f, 0, 1/5f, 0, 1/7f, 0, 1/9f, 0, 1/11f, 0, 1/13f, 0, 1/15f, 0, 1/17f, 0, 1/19f, 0}},
            {WaveShapes.Saw, new float[_numberOfPartials] {1, 1/2f, 1/3f, 1/4f, 1/5f, 1/6f, 1/7f, 1/8f, 1/9f, 1/10f, 1/11f, 1/12f, 1/13f, 1/14f, 1/15f, 1/16f, 1/17f, 1/18f, 1/19f, 1/20f}},
            {WaveShapes.Triangle, new float[_numberOfPartials] {1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}}
        };

        public SynthesizerVoice()
        {
#if USE_PURE_DATA_INSTEAD_OF_HOMEBREW
            LibPD.OpenAudio(0, 1, 44100);
            LibPD.OpenPatch(@"C:\Users\hakan.CORP\Desktop\my_additive.pd");

            LibPDPrint del = delegate(string text)
			{ 
                Console.WriteLine("PD Print: " + text);
            };	
			LibPD.Print += del;

            LibPD.SendMessage("baseampl", "test", new object[] {0.5f});
#else
            _partials = new List<HarmonicPartial>(_numberOfPartials);

            for (int i = 0; i < _numberOfPartials; i++)
            {
                _partials.Add(new HarmonicPartial(i + 1, 4000f + 1000f * i, 15000.4f + 5000f * i, 0.2f, 40000.8f + 10000f * i));
            }
#endif

            Shape = WaveShapes.Square; // TODO: Sine doesn't work...
        }

        public WaveShapes Shape
        {
            get
            {
                return _shape;
            }
            set
            {
                if (value != _shape)
                {
                    _shape = value;
                    for (int i = 0; i < _numberOfPartials; i++)
                    {
#if !USE_PURE_DATA_INSTEAD_OF_HOMEBREW
                        _partials[i].RelativeAmplitude = _amplitudeRelations[_shape][i];
#endif
                    }
                }
            }
        }

#if !USE_PURE_DATA_INSTEAD_OF_HOMEBREW
        public IEnumerable<ISampleProvider> SampleProviders
        {
            get { return _partials; }
        }
#endif

        internal void NoteOn(float frequency, float initialAmplitude)
        {
#if !USE_PURE_DATA_INSTEAD_OF_HOMEBREW
            for (int i = 0; i < _numberOfPartials; i++)
            {
                _partials[i].Frequency = frequency * (i + 1);
                //Debug.WriteLine("f{0}: {1}", i, _partials[i].Frequency);
                _partials[i].BaseAmplitude = initialAmplitude;
                _partials[i].Gate = true;
            }
            Debug.WriteLine("{0} - Env state: {1}", DateTime.Now.ToString("hh:mm:ss.fff"), _partials[0]._envelopeGenerator.State);
#endif
        }

        internal void NoteOff()
        {
#if !USE_PURE_DATA_INSTEAD_OF_HOMEBREW
            for (int i = 0; i < _numberOfPartials; i++)
            {
                _partials[i].Gate = false;
            }
            Debug.WriteLine("Env state: {0}", _partials[0]._envelopeGenerator.State);
#endif
        }

#if USE_PURE_DATA_INSTEAD_OF_HOMEBREW
        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            return 0;
        }
#endif
    }
}
