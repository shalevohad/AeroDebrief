using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;

internal class TransmissionAssembler
{
    private readonly Dictionary<string, ClientTransmissionBuffer> _clientAudioBuffers;
    private short[] _sampleRemainders;

    public TransmissionAssembler()
    {
        _clientAudioBuffers = new Dictionary<string, ClientTransmissionBuffer>();
        _sampleRemainders = new short[48000 * 2];
    }

    private short[] SplitRemainder(short[] sample)
    {
        var splitArrays = AudioManipulationHelper.SplitSampleByTime(48000 * 2, sample);
        var fullLengthRemainder = new short[48000 * 2];
        splitArrays.Item2.CopyTo(fullLengthRemainder, 0);
        _sampleRemainders =
            AudioManipulationHelper.MixSamplesClipped(_sampleRemainders, fullLengthRemainder, 48000 * 2);

        return splitArrays.Item1;
    }

    public void AddTransmission(ClientAudio audio)
    {
        var guid = audio.OriginalClientGuid;

        if (!_clientAudioBuffers.ContainsKey(guid)) _clientAudioBuffers.Add(guid, new ClientTransmissionBuffer());

        _clientAudioBuffers[guid].AddSample(audio);
    }

    public short[] GetAssembledSample()
    {
        var finalShortArray = new short[48000 * 2];
        _sampleRemainders.CopyTo(finalShortArray, 0);
        _sampleRemainders = new short[48000 * 2];

        foreach (var sample in _clientAudioBuffers.Values)
        {
            var clientPCM = sample.OutputPCM();
            short[] trimmedClientPCM;
            if (clientPCM.Length > 96000)
            {
                trimmedClientPCM = SplitRemainder(clientPCM);
            }
            else
            {
                trimmedClientPCM = new short[48000 * 2];
                clientPCM.CopyTo(trimmedClientPCM, 0);
            }

            finalShortArray = AudioManipulationHelper.MixSamplesClipped(finalShortArray, trimmedClientPCM, 48000 * 2);
        }


        return finalShortArray;
    }
}