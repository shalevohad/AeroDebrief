namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;

// abstract base class for a "stream" of audio information. the stream may return samples
// from the audio engine or construct samples from other streams (ie, mix).
internal abstract class AudioRecordingStream
{
    // return a tag string that identifies the stream.
    public string Tag { get; set; }

    // return the number of samples available in the stream.
    public abstract int Count();

    // read up to maxSampleCount samples from the stream and return them in the provided
    // buffer. returns the number of samples written to the buffer;
    public abstract int Read(float[] samples, int maxSampleCount);
}