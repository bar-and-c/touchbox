#undef USE_PURE_DATA_INSTEAD_OF_HOMEBREW

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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

    class SynthesizerVoice 
    {
        private List<HarmonicPartial> _partials;
        private const int _numberOfPartials = 20;

        // TODO: An ugly way to keep track of voices in Synthesizer in order to get "note off". Maybe use key numbers for "base frequency"?
        public int KeyNumber { get; set; }

        public MixingSampleProvider MixingSampleProvider { get; private set; }

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


            _partials = new List<HarmonicPartial>(_numberOfPartials);

            for (int i = 0; i < _numberOfPartials; i++)
            {
                _partials.Add(new HarmonicPartial(i + 1, 4000f + 1000f * i, 15000.4f + 5000f * i, 0.2f, 40000.8f + 10000f * i));
            }

            MixingSampleProvider = new MixingSampleProvider(_partials);

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
                        _partials[i].RelativeAmplitude = _amplitudeRelations[_shape][i];
                    }
                }
            }
        }

        public IEnumerable<ISampleProvider> SampleProviders
        {
            get { return _partials; }
        }

        internal void NoteOn(float frequency, float initialAmplitude)
        {
            for (int i = 0; i < _numberOfPartials; i++)
            {
                _partials[i].Frequency = frequency * (i + 1);
                _partials[i].BaseAmplitude = initialAmplitude;
                _partials[i].Gate = true;
            }
        }

        internal void NoteOff()
        {
            for (int i = 0; i < _numberOfPartials; i++)
            {
                _partials[i].Gate = false;
            }
        }


        internal void Modulate(float pressure)
        {
            // TODO: Hard to find some decent modulation strategy. For now I just push up some partails that are not in the current square wave. 
            _partials[1].ModulationAmplitude = pressure;
            _partials[3].ModulationAmplitude = pressure * 2;
            _partials[5].ModulationAmplitude = pressure * 3;
            _partials[7].ModulationAmplitude = pressure * 2;
            _partials[13].ModulationAmplitude = pressure;
            /*
            for (int i = 0; i < _numberOfPartials; i++)
            {
                _partials[i].ModulationAmplitude = pressure * i;
                System.Diagnostics.Debug.WriteLine("SynthesizerVoice.Modulate(partial[{0}].Mod: {1})", i, _partials[i].ModulationAmplitude);
            }
             * */
        }
    }
}
