using System;
using NAudio.Midi;

namespace MidiAccess
{
    public class Controller
    {
        private readonly MidiIn midiIn;
        private readonly MidiOut midiOut;

        public Controller(string inputDeviceName, string outputDeviceName)
        {
            this.midiIn = MidiInformation.GetInputDeviceWithName(inputDeviceName);
            this.midiIn.Start();
            this.midiIn.MessageReceived += MessageReceived;
            this.midiOut = MidiInformation.GetOutputDeviceWithName(outputDeviceName);
        }

        public void Dispose()
        {
            this.midiIn.Dispose();
            this.midiOut.Dispose();
        }

        private void MessageReceived(object sender, MidiInMessageEventArgs eventArgs)
        {
            byte[] message = BitConverter.GetBytes(eventArgs.RawMessage);
            OnMidiMessageReceived.Invoke(sender, new MidiMessageReceivedEventArgs()
            {
                control = message[1],
                value = message[2]
            });
        }

        public event MidiMessageReceivedEventHandler OnMidiMessageReceived;
        public class MidiMessageReceivedEventArgs : EventArgs
        {
            public byte control, value;
        }
        public delegate void MidiMessageReceivedEventHandler(object sender, MidiMessageReceivedEventArgs e);

        public void ToggleLED(GroupControls control, byte group, bool status)
        {
            this.ToggleLED(this.GetControlValue(control, group), status);
        }

        public void ToggleLED(byte control, bool status)
        {
            if (status)
                this.midiOut.SendBuffer(new byte[] { 0xB0, control, 0x7F });
            else
                this.midiOut.SendBuffer(new byte[] { 0xB0, control, 0x00 });
        }
        

        private byte GetControlValue(GroupControls control, byte group)
        {
            switch (control)
            {
                case GroupControls.slider:
                    return Convert.ToByte(0 + group);
                case GroupControls.dial:
                    return Convert.ToByte(16 + group);
                case GroupControls.solo:
                    return Convert.ToByte(32 + group);
                case GroupControls.mute:
                    return Convert.ToByte(48 + group);
                case GroupControls.r:
                    return Convert.ToByte(64 + group);
                default:
                    return byte.MaxValue;
            }
        }
    }

    public struct Control
    {
        public const int previousTrack = 58, nextTrack = 59, cycle = 46, setMarker = 60, previousMarker = 61, nextMarker = 62, back = 43, forward = 44, stop = 42, play = 41, record = 45;
    }
    public enum GroupControls { slider, dial, solo, mute, r };
}
