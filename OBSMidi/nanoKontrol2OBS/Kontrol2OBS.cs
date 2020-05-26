using System;
using System.Collections.Generic;
using MidiAccess;
using WindowsSoundControl;
using OBSWebsocketSharp;
using System.Runtime.InteropServices;
using System.Threading;

namespace nanoKontrol2OBS
{
    static class ExtensionMethod
    {
        public static double Map(this double value, double low1, double high1, double low2, double high2)
        {
            return (value - low1) / (high1 - low1) * (high2 - low2) + low2;
        }
    }

    partial class Kontrol2OBS
    {
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
        private readonly OBSConnector obsSocket;
        private readonly Controller nanoController;
        private Dictionary<_specialSourceType, SpecialSource> specialSources;

        public Kontrol2OBS(string url, string password)
        {
            Console.WriteLine("Connecting to websocket...");
            this.obsSocket = new OBSConnector(url, password);
            Console.WriteLine("Connected!");
            Console.WriteLine("Setting up audio (This might take a while).");
            this.SetupAudio();
            Console.WriteLine("Audio connected!");
            Console.WriteLine("Connecting nanoKontrol2 (Fancy Animation)");
            this.nanoController = new Controller(GetNanoKontrolInputDeviceName(), GetNanoKontrolOutputDeviceName());
            for (byte cc = 16; cc < 70; cc++)
                this.nanoController.ToggleLED(cc, false);
            for (byte cc = 16; cc < 70; cc++)//Fancy Animation
            {
                this.nanoController.ToggleLED(cc, true);
                Thread.Sleep(25);
            }
            for (byte cc = 16; cc < 70; cc++)
                this.nanoController.ToggleLED(cc, false);
            this.nanoController.OnMidiMessageReceived += OnNanoControllerInput;
            this.SetupNanoController();
            Console.WriteLine("Connected!");
            Console.WriteLine("Setup Event Handlers...");
            this.SetupOBSEventHandlers();
            Console.WriteLine("Done!");

            while (Console.ReadKey().Key != ConsoleKey.Escape) ;
            for (byte cc = 16; cc < 70; cc++)
                this.nanoController.ToggleLED(cc, false);
        }

        private void SetupOBSEventHandlers()
        {
            this.obsSocket.OnOBSStreamStarted += (s, e) => { this.nanoController.ToggleLED(Control.play, true); };
            this.obsSocket.OnOBSStreamStopped += (s, e) => { this.nanoController.ToggleLED(Control.play, false); };
            this.obsSocket.OnOBSReplayStarted += (s, e) => { this.nanoController.ToggleLED(Control.record, true); };
            this.obsSocket.OnOBSReplayStopped += (s, e) => { this.nanoController.ToggleLED(Control.record, false); };
            this.obsSocket.OnOBSSourceMuteStateChanged += OnOBSSourceMuteStateChanged;
            this.obsSocket.OnOBSSceneSwitched += OnOBSSceneSwitched;
        }

        private void OnOBSSceneSwitched(object sender, OBSConnector.OBSSceneSwitchedEventArgs e)
        {
            string[] scenes = this.obsSocket.GetSceneList();
            string currentScene = e.newSceneName;
            for (byte soloButtonIndex = 0; soloButtonIndex < scenes.Length && soloButtonIndex < 8;)
            {
                if (currentScene.Equals(scenes[soloButtonIndex]))
                    this.nanoController.ToggleLED(GroupControls.solo, ++soloButtonIndex, true);
                else
                    this.nanoController.ToggleLED(GroupControls.solo, ++soloButtonIndex, false);
            }
        }

        private void OnOBSSourceMuteStateChanged(object sender, OBSConnector.OBSSourceMuteStateChangedEventArgs e)
        {
            foreach (SpecialSource potentialSender in this.specialSources.Values)
                if (potentialSender.obsSourceName.Equals(e.sourcename))
                {
                    if (potentialSender.specialSourceType.Equals(_specialSourceType.desktop1))
                        this.nanoController.ToggleLED(GroupControls.r, 0, !e.muted);
                    else if (potentialSender.specialSourceType.Equals(_specialSourceType.desktop2))
                        this.nanoController.ToggleLED(GroupControls.r, 1, !e.muted);
                    else if (potentialSender.specialSourceType.Equals(_specialSourceType.mic1))
                        this.nanoController.ToggleLED(GroupControls.r, 2, !e.muted);
                    else if (potentialSender.specialSourceType.Equals(_specialSourceType.mic2))
                        this.nanoController.ToggleLED(GroupControls.r, 3, !e.muted);
                    else if (potentialSender.specialSourceType.Equals(_specialSourceType.mic3))
                        this.nanoController.ToggleLED(GroupControls.r, 4, !e.muted);
                    break;
                }
        }

        private void SetupAudio()
        {
            SpecialSources obsSpecialSources = obsSocket.GetSpecialSources();
            this.specialSources = new Dictionary<_specialSourceType, SpecialSource>()
            {
                {
                    _specialSourceType.desktop1, new SpecialSource(_specialSourceType.desktop1) {
                        obsSourceName = obsSpecialSources.sources[_specialSourceType.desktop1],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[_specialSourceType.desktop1]))
                    }
                },{
                    _specialSourceType.desktop2, new SpecialSource(_specialSourceType.desktop2) {
                        obsSourceName = obsSpecialSources.sources[_specialSourceType.desktop2],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[_specialSourceType.desktop2]))
                    }
                },{
                    _specialSourceType.mic1, new SpecialSource(_specialSourceType.mic1) {
                        obsSourceName = obsSpecialSources.sources[_specialSourceType.mic1],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[_specialSourceType.mic1]))
                    }
                },{
                    _specialSourceType.mic2, new SpecialSource(_specialSourceType.mic2) {
                        obsSourceName = obsSpecialSources.sources[_specialSourceType.mic2],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[_specialSourceType.mic2]))
                    }
                },{
                    _specialSourceType.mic3, new SpecialSource(_specialSourceType.mic3) {
                        obsSourceName = obsSpecialSources.sources[_specialSourceType.mic3],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[_specialSourceType.mic3]))
                    }
                }
            };
            this.specialSources[_specialSourceType.desktop1].windowsDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
        }

        private void WindowsDevice_OnMuteStateChanged(object sender, AudioDevice.OnMuteStateChangedEventArgs e)
        {
            foreach(SpecialSource potentialSender in this.specialSources.Values)
                if (potentialSender.windowsDevice.Equals(sender))
                {
                    if (potentialSender.specialSourceType.Equals(_specialSourceType.desktop1))
                        this.nanoController.ToggleLED(GroupControls.mute, 0, e.muted);
                    else if (potentialSender.specialSourceType.Equals(_specialSourceType.desktop2))
                        this.nanoController.ToggleLED(GroupControls.mute, 1, e.muted);
                    else if (potentialSender.specialSourceType.Equals(_specialSourceType.mic1))
                        this.nanoController.ToggleLED(GroupControls.mute, 2, e.muted);
                    else if (potentialSender.specialSourceType.Equals(_specialSourceType.mic2))
                        this.nanoController.ToggleLED(GroupControls.mute, 3, e.muted);
                    else if (potentialSender.specialSourceType.Equals(_specialSourceType.mic3))
                        this.nanoController.ToggleLED(GroupControls.mute, 4, e.muted);
                    break;
                }

        }

        private void SetupNanoController()
        {
            string[] scenes = this.obsSocket.GetSceneList();
            string currentScene = this.obsSocket.GetCurrentScene();
            for(byte soloButtonIndex = 0; soloButtonIndex < scenes.Length && soloButtonIndex < 8; soloButtonIndex++)
            {
                if (currentScene.Equals(scenes[soloButtonIndex]))
                    this.nanoController.ToggleLED(GroupControls.solo, soloButtonIndex, true);
                else
                    this.nanoController.ToggleLED(GroupControls.solo, soloButtonIndex, false);
            }

            this.nanoController.ToggleLED(GroupControls.r, 0, !this.obsSocket.GetMute(this.specialSources[_specialSourceType.desktop1].obsSourceName));
            this.nanoController.ToggleLED(GroupControls.r, 1, !this.obsSocket.GetMute(this.specialSources[_specialSourceType.desktop2].obsSourceName));
            this.nanoController.ToggleLED(GroupControls.r, 2, !this.obsSocket.GetMute(this.specialSources[_specialSourceType.mic1].obsSourceName));
            this.nanoController.ToggleLED(GroupControls.r, 3, !this.obsSocket.GetMute(this.specialSources[_specialSourceType.mic2].obsSourceName));
            this.nanoController.ToggleLED(GroupControls.r, 4, !this.obsSocket.GetMute(this.specialSources[_specialSourceType.mic3].obsSourceName));

            this.nanoController.ToggleLED(GroupControls.mute, 0, this.specialSources[_specialSourceType.desktop1].windowsDevice.IsMuted());
            this.nanoController.ToggleLED(GroupControls.mute, 1, this.specialSources[_specialSourceType.desktop2].windowsDevice.IsMuted());
            this.nanoController.ToggleLED(GroupControls.mute, 2, this.specialSources[_specialSourceType.mic1].windowsDevice.IsMuted());
            this.nanoController.ToggleLED(GroupControls.mute, 3, this.specialSources[_specialSourceType.mic2].windowsDevice.IsMuted());
            this.nanoController.ToggleLED(GroupControls.mute, 4, this.specialSources[_specialSourceType.mic3].windowsDevice.IsMuted());

            StreamingStatus stats = this.obsSocket.GetStreamingStatus();
            this.nanoController.ToggleLED(Control.play, stats.streaming);
        }
        private void OnNanoControllerInput(object sender, Controller.MidiMessageReceivedEventArgs e)
        {
            switch (e.control)
            {
                //Sliders
                case 0:
                    this.obsSocket.SetVolume(this.specialSources[_specialSourceType.desktop1].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 1:
                    this.obsSocket.SetVolume(this.specialSources[_specialSourceType.desktop2].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 2:
                    this.obsSocket.SetVolume(this.specialSources[_specialSourceType.mic1].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 3:
                    this.obsSocket.SetVolume(this.specialSources[_specialSourceType.mic2].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 4:
                    this.obsSocket.SetVolume(this.specialSources[_specialSourceType.mic3].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                //Dials
                case 16:
                    this.specialSources[_specialSourceType.desktop1].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 17:
                    this.specialSources[_specialSourceType.desktop2].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 18:
                    this.specialSources[_specialSourceType.mic1].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 19:
                    this.specialSources[_specialSourceType.mic2].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 20:
                    this.specialSources[_specialSourceType.mic3].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                //Mute
                case 48:
                    this.specialSources[_specialSourceType.desktop1].windowsDevice.ToggleMute();
                    break;
                case 49:
                    this.specialSources[_specialSourceType.desktop2].windowsDevice.ToggleMute();
                    break;
                case 50:
                    this.specialSources[_specialSourceType.mic1].windowsDevice.ToggleMute();
                    break;
                case 51:
                    this.specialSources[_specialSourceType.mic2].windowsDevice.ToggleMute();
                    break;
                case 52:
                    this.specialSources[_specialSourceType.mic3].windowsDevice.ToggleMute();
                    break;
                //R
                case 64:
                    this.obsSocket.ToggleMute(this.specialSources[_specialSourceType.desktop1].obsSourceName);
                    break;
                case 65:
                    this.obsSocket.ToggleMute(this.specialSources[_specialSourceType.desktop2].obsSourceName);
                    break;
                case 66:
                    this.obsSocket.ToggleMute(this.specialSources[_specialSourceType.mic1].obsSourceName);
                    break;
                case 67:
                    this.obsSocket.ToggleMute(this.specialSources[_specialSourceType.mic2].obsSourceName);
                    break;
                case 68:
                    this.obsSocket.ToggleMute(this.specialSources[_specialSourceType.mic3].obsSourceName);
                    break;
                //Play
                case 41:
                    if(e.value == 127) this.obsSocket.StartStopStreaming();
                    break;
                //Stop
                case 42:
                    if (e.value == 127) keybd_event(0xB3, 0, 1, IntPtr.Zero);
                    break;
                //Back
                case 43:
                    if (e.value == 127) keybd_event(0xB1, 0, 1, IntPtr.Zero);
                    break;
                //Forward
                case 44:
                    if (e.value == 127) keybd_event(0xB0, 0, 1, IntPtr.Zero);
                    break;
                case 45:
                    if (e.value == 127) this.obsSocket.SaveReplayBuffer();
                    break;
                default:
                    Console.WriteLine("Control {0} not bound.", e.control);
                    break;
            }
            //Solo
            if(e.control >= 32 && e.control <= 39)
            {
                string[] scenes = this.obsSocket.GetSceneList();
                if (scenes.Length > (e.control - 32))
                    this.obsSocket.SetCurrentScene(scenes[e.control - 32]);
            }
        }

        private string GetNanoKontrolInputDeviceName()
        {
            foreach(string potentialName in MidiInformation.GetInputDevices())
                if (potentialName.Contains("nano"))
                    return potentialName;
            return "";
        }

        private string GetNanoKontrolOutputDeviceName()
        {
            foreach (string potentialName in MidiInformation.GetOutputDevices())
                if (potentialName.Contains("nano"))
                    return potentialName;
            return "";
        }


        static void Main(string[] args)
        {
            new Kontrol2OBS("127.0.0.1:4444","1234");
        }
    }
}
