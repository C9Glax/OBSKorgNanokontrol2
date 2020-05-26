using System;
using NAudio.Midi;
using OBSWebsocketSharp;
using System.Runtime.InteropServices;
using AudioSwitcher.AudioApi.CoreAudio;
using System.Collections.Generic;

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
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
        public const int VK_MEDIA_NEXT_TRACK = 0xB0;
        public const int VK_MEDIA_PLAY_PAUSE = 0xB3;
        public const int VK_MEDIA_PREV_TRACK = 0xB1;

        private readonly MidiIn midiIn;
        private readonly MidiOut midiOut;
        private OBSConnector obs;
        private SpecialSources specialSources;
        private Dictionary<string, CoreAudioDevice> audioDevices;
        private string[] scenes;

        public Midi(string url, string password)
        {
            this.midiIn = SelectMidiInput();
            this.midiOut = SelectMidiOutput();
            this.Setup(url, password);
        }

        public Midi(string url, string password, int productIdIn, int productIdOut)
        {
            this.midiIn = SelectMidiInput(productIdIn);
            this.midiOut = SelectMidiOutput(productIdOut);
            this.Setup(url, password);
        }

        private void Setup(string url, string password)
        {
            this.obs = new OBSConnector(url, password);
            this.obs.OnOBSSourceMuteStateChanged += OnOBSSourceMuteStateChanged;
            this.obs.OnOBSReplayStarted += OnOBSReplayStarted;
            this.obs.OnOBSReplayStopped += OnOBSReplayStopped;
            this.obs.OnOBSStreamStarted += OnOBSStreamStarted;
            this.obs.OnOBSStreamStopped += OnOBSStreamStopped;
            this.obs.OnOBSSceneSwitched += OnOBSSceneSwitched;
            this.SetupOBS();
            this.SetupAudioDevices();
            this.SetupMidi(midiIn, midiOut);

            while (Console.ReadKey().Key != ConsoleKey.Escape) ;
            for (byte cc = 0; cc < byte.MaxValue; cc++)
                this.LEDStatus(cc, false);
        }

        private void OnOBSSceneSwitched(object sender, OBSConnector.OBSSceneSwitchedEventArgs e)
        {
            string currentScene = e.newSceneName;
            for (int scenceIndex = 0; scenceIndex < this.scenes.Length && scenceIndex < 8; scenceIndex++)
                this.LEDStatus(Convert.ToByte(scenceIndex + 32), currentScene == this.scenes[scenceIndex]);
        }

        private void SetupAudioDevices()
        {
            Console.WriteLine("Setting-Up Audio Devices (This might take a while)");
            string guiddesktop1 = (string)this.obs.GetSourceSettings(this.specialSources.desktop1)["sourceSettings"]["device_id"];
            string guiddesktop2 = (string)this.obs.GetSourceSettings(this.specialSources.desktop2)["sourceSettings"]["device_id"];
            string guidmic1 = (string)this.obs.GetSourceSettings(this.specialSources.mic1)["sourceSettings"]["device_id"];
            string guidmic2 = (string)this.obs.GetSourceSettings(this.specialSources.mic2)["sourceSettings"]["device_id"];
            string guidmic3 = (string)this.obs.GetSourceSettings(this.specialSources.mic3)["sourceSettings"]["device_id"];
            this.audioDevices = new Dictionary<string, CoreAudioDevice>();
            foreach (CoreAudioDevice audioDevice in new CoreAudioController().GetDevices(AudioSwitcher.AudioApi.DeviceState.Active))
            {
                if (audioDevice.RealId == guiddesktop1)
                {
                    this.audioDevices.Add(this.specialSources.desktop1, audioDevice);
                    this.LEDStatus(48, audioDevice.IsMuted);
                }
                else if (audioDevice.RealId == guiddesktop2)
                {
                    this.audioDevices.Add(this.specialSources.desktop2, audioDevice);
                    this.LEDStatus(49, audioDevice.IsMuted);
                }
                else if (audioDevice.RealId == guidmic1)
                {
                    this.audioDevices.Add(this.specialSources.mic1, audioDevice);
                    this.LEDStatus(50, audioDevice.IsMuted);
                }
                else if (audioDevice.RealId == guidmic2)
                {
                    this.audioDevices.Add(this.specialSources.mic2, audioDevice);
                    this.LEDStatus(51, audioDevice.IsMuted);
                }
                else if (audioDevice.RealId == guidmic3)
                {
                    this.audioDevices.Add(this.specialSources.mic3, audioDevice);
                    this.LEDStatus(52, audioDevice.IsMuted);
                }
            }
            Console.WriteLine("Audio Devices Set-up");
        }

        private void OnOBSStreamStopped(object sender, OBSConnector.OBSStreamEventArgs e)
        {
            this.LEDStatus(41, false);
        }

        private void OnOBSStreamStarted(object sender, OBSConnector.OBSStreamEventArgs e)
        {
            this.LEDStatus(41, true);
        }

        private void OnOBSReplayStopped(object sender, OBSConnector.OBSReplayEventArgs e)
        {
            this.LEDStatus(45, false);
        }

        private void OnOBSReplayStarted(object sender, OBSConnector.OBSReplayEventArgs e)
        {
            this.LEDStatus(45, true);
        }

        private void OnOBSSourceMuteStateChanged(object sender, OBSConnector.OBSSourceMuteStateChangedEventArgs e)
        {
            if(e.sourcename == this.specialSources.desktop1)
                this.LEDStatus(64, !e.muted);
            else if (e.sourcename == this.specialSources.desktop2)
                this.LEDStatus(65, !e.muted);
            else if (e.sourcename == this.specialSources.mic1)
                this.LEDStatus(66, !e.muted);
            else if (e.sourcename == this.specialSources.mic2)
                this.LEDStatus(67, !e.muted);
            else if (e.sourcename == this.specialSources.mic3)
                this.LEDStatus(68, !e.muted);
        }

        private void SetupMidi(MidiIn deviceIn, MidiOut deviceOut)
        {
            deviceIn.MessageReceived += this.MidiInMessageHandler;
            deviceIn.Start();
        }

        private void SetupOBS()
        {
            this.scenes = this.obs.GetSceneList();
            string currentScene = this.obs.GetCurrentScene();
            for (int scenceIndex = 0; scenceIndex < this.scenes.Length && scenceIndex < 8; scenceIndex++)
                this.LEDStatus(Convert.ToByte(scenceIndex + 32), currentScene == this.scenes[scenceIndex]);

            this.specialSources = obs.GetSpecialSources();
            this.LEDStatus(64, !obs.GetMute(specialSources.desktop1));
            this.LEDStatus(65, !obs.GetMute(specialSources.desktop2));
            this.LEDStatus(66, !obs.GetMute(specialSources.mic1));
            this.LEDStatus(67, !obs.GetMute(specialSources.mic2));
            this.LEDStatus(68, !obs.GetMute(specialSources.mic3));

            StreamingStatus stats = this.obs.GetStreamingStatus();
            this.LEDStatus(45, stats.recording);
            this.LEDStatus(41, stats.streaming);
        }

        private void LEDStatus(byte button, bool on)
        {
            if(on)
                this.midiOut.SendBuffer(new byte[] { 0xB0, button, 0x7F });
            else
                this.midiOut.SendBuffer(new byte[] { 0xB0, button, 0x00 });
        }

        private void MidiInMessageHandler(object sender, MidiInMessageEventArgs eventArgs)
        {
            byte[] message = BitConverter.GetBytes(eventArgs.RawMessage);
            byte control = message[1];
            byte value = message[2];
            Console.ForegroundColor = ConsoleColor.White;
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
            else if(control == 16) //Dial
            {
                this.audioDevices[this.specialSources.desktop1].Volume = Convert.ToDouble(value).Map(0, 127, 0, 100);
            }
            else if (control == 17) //Dial
            {
                this.audioDevices[this.specialSources.desktop2].Volume = Convert.ToDouble(value).Map(0, 127, 0, 100);
            }
            else if (control == 18) //Dial
            {
                this.audioDevices[this.specialSources.mic1].Volume = Convert.ToDouble(value).Map(0, 127, 0, 100);
            }
            else if (control == 19) //Dial
            {
                this.audioDevices[this.specialSources.mic2].Volume = Convert.ToDouble(value).Map(0, 127, 0, 100);
            }
            else if (control == 20) //Dial
            {
                this.audioDevices[this.specialSources.mic3].Volume = Convert.ToDouble(value).Map(0, 127, 0, 100);
            }
            else if (control >= 32 && control <= 39) //Solo
            {
                int sceneIndex = Convert.ToInt32(control) - 32;
                if (sceneIndex <= this.scenes.Length)
                    this.obs.SetCurrentScene(this.scenes[sceneIndex]);
            }
            else if (control == 48) //Mute
            {
                this.audioDevices[this.specialSources.desktop1].Mute(value == 127);
                this.LEDStatus(control, value == 127);
            }
            else if (control == 49) //Mute
            {
                this.audioDevices[this.specialSources.desktop2].Mute(value == 127);
                this.LEDStatus(control, value == 127);
            }
            else if (control == 50) //Mute
            {
                this.audioDevices[this.specialSources.mic1].Mute(value == 127);
                this.LEDStatus(control, value == 127);
            }
            else if (control == 51) //Mute
            {
                this.audioDevices[this.specialSources.mic2].Mute(value == 127);
                this.LEDStatus(control, value == 127);
            }
            else if (control == 52) //Mute
            {
                this.audioDevices[this.specialSources.mic3].Mute(value == 127);
                this.LEDStatus(control, value == 127);
            }
            else if (control == 64) //R
            {
                obs.SetMute(this.specialSources.desktop1, value == 127);
            }
            else if (control == 65) //R
            {
                obs.SetMute(this.specialSources.desktop2, value == 127);
            }
            else if (control == 66) //R
            {
                obs.SetMute(this.specialSources.mic1, value == 127);
            }
            else if (control == 67) //R
            {
                obs.SetMute(this.specialSources.mic2, value == 127);
            }
            else if (control == 68) //R
            {
                obs.SetMute(this.specialSources.mic3, value == 127);
            }
            else if (control == 41 && value == 127) //Play
            {
                obs.StartStopStreaming();
            }
            else if (control == 42 && value == 127) //Stop
            {
                keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 1, IntPtr.Zero);
            }
            else if (control == 43 && value == 127) //Previus
            {
                keybd_event(VK_MEDIA_PREV_TRACK, 0, 1, IntPtr.Zero);
            }
            else if (control == 44 && value == 127) //Next
            {
                keybd_event(VK_MEDIA_NEXT_TRACK, 0, 1, IntPtr.Zero);
            }
            else if (control == 45 && value == 127) //Record
            {
                obs.SaveReplayBuffer();
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
            Console.Clear();
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
            Console.Clear();
            return deviceOut;
        }

        static void Main(string[] args)
        {
            new Midi("127.0.0.1:4444","1234");
        }
    }
}
