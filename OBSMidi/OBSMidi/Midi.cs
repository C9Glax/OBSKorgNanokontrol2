using System;
using NAudio.Midi;
using OBSWebsocketSharp;

namespace OBSMidi
{
    public static class ExtensionMethod
    {
        public static double Map(this double value, double low1, double high1, double low2, double high2)
        {
            return (value - low1) / (high1 - low1) * (high2 - low2) + low2;
        }
    }

    class Midi
    {
        private readonly MidiIn deviceIn;
        private readonly MidiOut deviceOut;
        private OBSConnector obs;
        private SpecialSources specialSources;

        public Midi()
        {
            this.deviceIn = SelectMidiInput();
            this.deviceOut = SelectMidiOutput();
            this.Setup();
        }

        public Midi(int productIdIn, int productIdOut)
        {
            this.deviceIn = SelectMidiInput(productIdIn);
            this.deviceOut = SelectMidiOutput(productIdOut);
            this.Setup();
        }

        private void Setup()
        {
            SetupMidi(deviceIn, deviceOut);
            this.obs = new OBSConnector("ws://127.0.0.1:4444", "1234");
            this.obs.OnOBSSourceMuteStateChanged += OnOBSSourceMuteStateChanged;
            this.SetupOBS();
        }

        private void OnOBSSourceMuteStateChanged(object sender, OBSConnector.OBSSourceMuteStateChangedEventArgs e)
        {
            if(e.sourcename == this.specialSources.desktop1)
                this.deviceOut.Send(new NoteEvent(0, 1, e.muted ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 48, 127).GetAsShortMessage());
            else if (e.sourcename == this.specialSources.desktop2)
                this.deviceOut.Send(new NoteEvent(0, 1, e.muted ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 49, 127).GetAsShortMessage());
            else if (e.sourcename == this.specialSources.mic1)
                this.deviceOut.Send(new NoteEvent(0, 1, e.muted ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 50, 127).GetAsShortMessage());
            else if (e.sourcename == this.specialSources.mic2)
                this.deviceOut.Send(new NoteEvent(0, 1, e.muted ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 51, 127).GetAsShortMessage());
            else if (e.sourcename == this.specialSources.mic3)
                this.deviceOut.Send(new NoteEvent(0, 1, e.muted ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 52, 127).GetAsShortMessage());
        }

        private void SetupMidi(MidiIn deviceIn, MidiOut deviceOut)
        {
            deviceIn.MessageReceived += this.midiInMessageHandler;
            deviceIn.Start();
        }

        private void SetupOBS()
        {
            this.specialSources = obs.GetSpecialSources();
            this.SendMidi(new NoteEvent(0, 1, obs.GetMute(specialSources.desktop1) ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 48, 127).GetAsShortMessage());
            this.SendMidi(new NoteEvent(0, 1, obs.GetMute(specialSources.desktop2) ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 49, 127).GetAsShortMessage());
            this.SendMidi(new NoteEvent(0, 1, obs.GetMute(specialSources.mic1) ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 50, 127).GetAsShortMessage());
            this.SendMidi(new NoteEvent(0, 1, obs.GetMute(specialSources.mic2) ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 51, 127).GetAsShortMessage());
            this.SendMidi(new NoteEvent(0, 1, obs.GetMute(specialSources.mic3) ? MidiCommandCode.NoteOn : MidiCommandCode.NoteOff, 52, 127).GetAsShortMessage());
        }

        private void SendMidi(int message)
        {
            Console.WriteLine("TO: {0:X8}", message);
            this.deviceOut.Send(message);
        }

        private void midiInMessageHandler(object sender, MidiInMessageEventArgs eventArgs)
        {
            byte[] message = BitConverter.GetBytes(eventArgs.RawMessage);
            byte control = message[1];
            byte value = message[2];
            //Console.WriteLine("Time {0} Control {1} Value {2}", eventArgs.Timestamp, control, value);
            Console.WriteLine("FROM: {0:X8}", eventArgs.RawMessage);
            if(control == 0) //Volume
            {
                obs.SetVolume(this.specialSources.desktop1, Convert.ToDouble(value).Map(0, 127, 0, 1));
            }
            else if (control == 1) //Volume
            {
                obs.SetVolume(this.specialSources.desktop2, Convert.ToDouble(value).Map(0, 127, 0, 1));
            }
            else if (control == 2) //Volume
            {
                obs.SetVolume(this.specialSources.mic1, Convert.ToDouble(value).Map(0, 127, 0, 1));
            }
            else if (control == 3) //Volume
            {
                obs.SetVolume(this.specialSources.mic2, Convert.ToDouble(value).Map(0, 127, 0, 1));
            }
            else if (control == 4) //Volume
            {
                obs.SetVolume(this.specialSources.mic3, Convert.ToDouble(value).Map(0, 127, 0, 1));
            }
            else if (control >= 16 && control <= 23) //Dial
            {

            }
            else if (control >= 32 && control <= 39) //Solo
            {

            }
            else if (control == 48) //Mute
            {
                obs.SetMute(this.specialSources.desktop1, (value == 127));
            }
            else if (control == 49) //Mute
            {
                obs.SetMute(this.specialSources.desktop2, value == 127);
            }
            else if (control == 50) //Mute
            {
                obs.SetMute(this.specialSources.mic1, value == 127);
            }
            else if (control == 51) //Mute
            {
                obs.SetMute(this.specialSources.mic2, value == 127);
            }
            else if (control == 52) //Mute
            {
                obs.SetMute(this.specialSources.mic3, value == 127);
            }
            else if (control >= 64 && control <= 71) //R
            {

            }
            else if (control == 41) //Play
            {
                obs.StartStopStreaming();
            }
            else if (control == 42) //Stop
            {
                obs.SaveReplayBuffer();
            }
            else if (control == 43) //Previus
            {

            }
            else if (control == 44) //Next
            {

            }
            else if (control == 45) //Record
            {
                obs.StartStopRecording();
            }
        }

        public static MidiIn SelectMidiInput(int productID)
        {
            for (int device = 0; device < MidiIn.NumberOfDevices; device++)
            {
                if (MidiIn.DeviceInfo(device).ProductId == productID)
                    return new MidiIn(device);
            }

            throw new Exception("No MIDI Device with Product-ID " + productID + " found.");
        }

        public static MidiIn SelectMidiInput()
        {
            Console.WriteLine("IN:");
            for (int device = 0; device < MidiIn.NumberOfDevices; device++)
                Console.WriteLine("{0}) {1}", device, MidiIn.DeviceInfo(device).ProductName);
            Console.WriteLine("Your choice: ");
            MidiIn deviceIn = new MidiIn(Convert.ToInt32(Console.ReadKey().KeyChar.ToString()));

            return deviceIn;
        }

        public static MidiOut SelectMidiOutput(int productID)
        {
            for (int device = 0; device < MidiOut.NumberOfDevices; device++)
            {
                if (MidiOut.DeviceInfo(device).ProductId == productID)
                    return new MidiOut(device);
            }

            throw new Exception("No MIDI Device with Product-ID " + productID + " found.");
        }

        public static MidiOut SelectMidiOutput()
        {
            Console.WriteLine("OUT:");
            for (int device = 0; device < MidiOut.NumberOfDevices; device++)
                Console.WriteLine("{0}) {1}", device, MidiOut.DeviceInfo(device).ProductName);
            Console.WriteLine("Your choice: ");
            MidiOut deviceOut = new MidiOut(Convert.ToInt32(Console.ReadKey().KeyChar.ToString()));

            return deviceOut;
        }

        static void Main(string[] args)
        {
            new Midi();
            Console.ReadKey();
        }
    }
}
