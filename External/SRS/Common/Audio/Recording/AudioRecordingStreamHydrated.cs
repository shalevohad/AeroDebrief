using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;

// AudioRecordingStream for a stream of audio samples that excludes dead air. this class
// provides the ability to (with some inaccuracies) reconstruct (or "hydrate") the dead
// air.
internal class AudioRecordingStreamHydrated : AudioRecordingStream
{
    private readonly float[] _deadSamples;
    private readonly CircularFloatBuffer _fullSamples;

    private readonly int _samplesPerMsec = Constants.OUTPUT_SAMPLE_RATE / 1000;

    private long _curStreamTime;
    private int _prevSampleCount;
    private long _prevTickTime;

    // construct a new hydrated stream. the buffer size must be large enough to accomodate
    // the maximum number of samples (plus some guardband) that can arrive between calls
    // to WriteRawSamples() for the instance.
    public AudioRecordingStreamHydrated(int bufferSize, string tag)
    {
        _fullSamples = new CircularFloatBuffer(bufferSize);
        _deadSamples = new float[bufferSize];
        Tag = tag;
    }

    // prepare the stream to start recording at the given initialTickTime (in ms).
    public void StartRecording(long initialTickTime)
    {
        _curStreamTime = initialTickTime;
        _prevTickTime = initialTickTime;
        _prevSampleCount = 0;
    }

    // count the samples available in the stream (these are hydrated).
    public override int Count()
    {
        return _fullSamples.Count;
    }

    // read hydrated samples (ie, with dead air) from the head of the stream.
    public override int Read(float[] samples, int maxSampleCount)
    {
        return _fullSamples.Read(samples, 0, maxSampleCount);
    }

    // write hydrated samples (ie, with dead air) to the tail of the stream.
    public int Write(float[] samples, int sampleCount)
    {
        return _fullSamples.Write(samples, 0, sampleCount);
    }

    // add raw "dehydrated" (ie, no dead air) samples into a hydrated stream, adding dead air
    // as necessary. method updates the instance's internal circular buffer that captures the
    // current hydrated stream and provides it to clients via Read(). it will also track a
    // stream time that represents the current amount of audio that has been emitted to the
    // stream (note that this can run slightly ahead or behind based on how things play out).
    //
    // this method should be called at a (roughly) regular interval that is consistent with the
    // sample buffer size set at construction/instantiation (sample buffer size should be set
    // to the max interval + guardband).
    //
    // the hydration process here is accurate enough for most usage models (such as pairing
    // an srs recording with tacview), but will likely not produce an exact match of the actual
    // audio. there are a number of sources of error that will cause deviations in the
    // recording. for example, we use a stopwatch with ms accuracy for the main time base. in
    // addition, the algorithm makes simplifying assumptions that can lead to some inaccuracies
    // as well.
    //
    // prior to writing raw samples with this method, users should first call StartRecording()
    // to get the instance set up.
    //
    // curTickTime          current system time in ms
    // curSamples           dehydrated (raw) samples collected since last WriteRawSamples()
    // curSampleCount       number of samples in curSamples
    public void WriteRawSamples(long curTickTime, float[] curSamples, int curSampleCount)
    {
        if (_curStreamTime < curTickTime)
        {
            if (curSampleCount == 0)
            {
                // current tick has no samples. emit dead air for the interval between the
                // current stream time and the current tick time and advance the stream time
                // to the current tick time.

                var count = (int)(curTickTime - _curStreamTime) * _samplesPerMsec;
                _fullSamples.Write(_deadSamples, 0, count);
                _curStreamTime = curTickTime;
            }
            else if (_prevSampleCount == 0)
            {
                // previous tick has no samples, current tick does: audio is beginning. we
                // assume all of the current samples happen right before the current tick time.
                // emit dead air followed by samples to anchor the end of the samples at the
                // current tick time. advance the stream time to the current tick time.
                //
                // this algorithm, while simple, can create inaccuracies in the output under
                // circumstances such as:
                //
                // (1) the current samples include samples from beyond the tick time.
                // (2) the current samples include two or more segments of active audio
                //     separated by dead air.
                //
                // these inaccuracies should be managable in most cases and may not be possible
                // to overcome without additional information on timing of dead air in the stream.
                // note that the inaccuracies can be mitigated with smaller intervals between
                // calls to this method.

                var countDead = (int)(curTickTime - _prevTickTime) * _samplesPerMsec - curSampleCount;
                if (countDead > 0) _fullSamples.Write(_deadSamples, 0, countDead);
                _fullSamples.Write(curSamples, 0, curSampleCount);
                _curStreamTime = curTickTime;
                if (countDead < 0)
                    // we've somehow got more data than time in the tick, we're going to have to
                    // advance the current stream time beyond the current tick time as we can't
                    // anchor the end of the clip to the current tick time.
                    _curStreamTime += -countDead / _samplesPerMsec;
            }
            else if (_prevSampleCount > 0)
            {
                // previous and current ticks have samples. audio continues. add samples to the
                // full stream and advance the stream time by the total duration of the samples.

                _fullSamples.Write(curSamples, 0, curSampleCount);
                _curStreamTime += curSampleCount / _samplesPerMsec;
            }

            _prevTickTime = curTickTime;
            _prevSampleCount = curSampleCount;
        }
    }
}