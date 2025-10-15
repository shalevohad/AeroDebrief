using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Input;

public class InputDevice
{
    public InputBinding InputBind { get; set; }

    public string DeviceName { get; set; }

    public int Button { get; set; }
    public Guid InstanceGuid { get; set; }
    public int ButtonValue { get; set; }

    public bool IsSameBind(InputDevice compare)
    {
        return Button == compare.Button &&
               compare.InstanceGuid == InstanceGuid &&
               ButtonValue == compare.ButtonValue;
    }
}