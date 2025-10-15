using System;
using System.Runtime.InteropServices;
using SharpDX.XInput;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Input;

/// <summary>
///     Controller class that masquerades as a SharpDX.DirectInput.Device.
/// </summary>
public class XInputController : IDisposable
{
    private Controller _controller = new Controller(UserIndex.One);
    public static Guid DeviceGuid => InformationData.ProductGuid;

    // Dispose interface, assume disposed on disconnect.
    public bool IsDisposed => _controller == null || !_controller.IsConnected;

    public InformationData Information { get; }

    public void Dispose()
    {
        Unacquire();
    }

    // SharpDX.DirectInput.Device masquerading.
    public bool Poll()
    {
        // noop.
        return !IsDisposed;
    }

    public GamepadButtonFlags GetCurrentState()
    {
        return _controller.GetState().Gamepad.Buttons;
    }

    public void Unacquire()
    {
        _controller = null;
    }

    public struct InformationData
    {
        // Randomly generated Guid.

        public string ProductName => "XInputController";

        public Guid InstanceGuid => ProductGuid;

        public static Guid ProductGuid { get; } = new("bb78c26f-9bfd-41c1-81f0-2f1258d25075");
    }
}