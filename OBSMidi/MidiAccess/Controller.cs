/*
 * Here lives the nanoKontrol2
 */

using System;
using NAudio.Midi;

namespace MidiAccess
{
    public class Controller
    {
        private readonly MidiIn midiIn;
        private readonly MidiOut midiOut;

        /*
         * Creates controls and listeners for nanoKontrol2
         */
        public Controller(string inputDeviceName, string outputDeviceName)
        {
            try
            {
                this.midiIn = MidiInformation.GetInputDeviceWithName(inputDeviceName);
                this.midiOut = MidiInformation.GetOutputDeviceWithName(outputDeviceName);
            }catch (Exception e)
            {
                throw e;
            }
            this.midiIn.Start();
            this.midiIn.MessageReceived += MessageReceived;
        }

        /*
         * Gracefully disconnect
         */
        public void Dispose()
        {
            this.midiIn.Dispose();
            this.midiOut.Dispose();
        }

        /*
         * MIDI-Input received
         * Convert and forward to Kontrol2OBS(.cs)
         */
        private void MessageReceived(object sender, MidiInMessageEventArgs eventArgs)
        {
            byte[] message = BitConverter.GetBytes(eventArgs.RawMessage);
            OnMidiMessageReceived.Invoke(sender, new MidiMessageReceivedEventArgs()
            {
                control = message[1],
                value = message[2]
            });
        }

        /*
         * Event Handler
         */
        public event MidiMessageReceivedEventHandler OnMidiMessageReceived;
        public class MidiMessageReceivedEventArgs : EventArgs
        {
            public byte control, value;
        }
        public delegate void MidiMessageReceivedEventHandler(object sender, MidiMessageReceivedEventArgs e);


        /*
         * Sends Message to nanoKontrol2 to turn on/off LED
         */
        public void ToggleLED(byte control, bool status)
        {
            if (status)
                this.midiOut.SendBuffer(new byte[] { 0xB0, control, 0x7F }); //ON Message
            else
                this.midiOut.SendBuffer(new byte[] { 0xB0, control, 0x00 }); //OFF Message
        }
    }
}
