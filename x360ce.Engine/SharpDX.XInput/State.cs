namespace SharpDX.XInput
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public struct State
    {
        public int PacketNumber;
        public SharpDX.XInput.Gamepad Gamepad;



        public State (State other)
        {
            PacketNumber = other.PacketNumber;
            Gamepad = other.Gamepad;
        }

        public State Clone ()
        {
            return new State (this);
        }

        public State AddButton (GamepadButtonFlags button)
        {
            Gamepad.Buttons |= button;

            return this;
        }

        public State RemoveButton (GamepadButtonFlags button)
        {
            Gamepad.Buttons &= ~button;

            return this;
        }

        public bool ButtonIsPressed (GamepadButtonFlags button)
        {
            return (Gamepad.Buttons & button) != 0;
        }
    }
}

