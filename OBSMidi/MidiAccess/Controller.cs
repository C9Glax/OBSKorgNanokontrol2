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

        public void ToggleLED(byte control, bool status)
        {
            if (status)
                this.midiOut.SendBuffer(new byte[] { 0xB0, control, 0x7F });
            else
                this.midiOut.SendBuffer(new byte[] { 0xB0, control, 0x00 });
        }
    }
}
