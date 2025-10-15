using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server.TransmissionLogging;

internal class TransmissionLog
{
    public TransmissionLog(DateTime time, string frequency)
    {
        TransmissionFrequency = frequency;
        TransmissionStart = time;
        TransmissionEnd = time;
    }

    public string TransmissionFrequency { get; set; }
    public DateTime TransmissionStart { get; set; }
    public DateTime TransmissionEnd { get; set; }

    public bool IsComplete()
    {
        return DateTime.Now.Ticks - TransmissionEnd.Ticks > 4000000 ? true : false;
    }
}