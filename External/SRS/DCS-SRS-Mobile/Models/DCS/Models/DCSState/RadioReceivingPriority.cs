using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Models.DCS.Models.DCSState;

public class RadioReceivingPriority
{
    public bool Decryptable;
    public byte Encryption;
    public double Frequency;
    public float LineOfSightLoss;
    public short Modulation;
    public double ReceivingPowerLossPercent;
    public DCSRadio ReceivingRadio;

    public RadioReceivingState ReceivingState;
}