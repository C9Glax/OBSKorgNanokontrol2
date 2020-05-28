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

    public partial class Kontrol2OBS
    {
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
        private OBSConnector obsSocket;
        private Controller nanoController;
        private Dictionary<SpecialSourceType, SpecialSourceObject> specialSources;
        private string[] obsScenes;
        private readonly string url, password;

        public Kontrol2OBS(string url, string password)
        {
            this.url = url;
            this.password = password;
        }

        public void Create()
        {
            this.UpdateLogStatus("Connecting to websocket...");
            this.obsSocket = new OBSConnector(url, password);
            this.obsSocket.OnOBSWebsocketInfo += (s, e) => { this.OnInfoLog?.Invoke(s, new LogEventArgs() { text = e.text }); };
            this.obsSocket.OnOBSWebsocketWarning += (s, e) => { this.OnWarningLog?.Invoke(s, new LogEventArgs() { text = e.text }); };
            this.UpdateLogStatus("Setting up audio (This might take a while)...");
            this.SetupAudio();
            this.UpdateLogStatus("Connecting nanoKontrol2...");
            this.nanoController = new Controller(GetNanoKontrolInputDeviceName(), GetNanoKontrolOutputDeviceName());

            for (byte cc = 16; cc < 70; cc++)//Fancy Animation
                this.nanoController.ToggleLED(cc, false);
            for (byte cc = 16; cc < 70; cc++)
            {
                this.nanoController.ToggleLED(cc, true);
                Thread.Sleep(25);
            }
            for (byte cc = 16; cc < 70; cc++)
                this.nanoController.ToggleLED(cc, false);

            this.nanoController.OnMidiMessageReceived += OnNanoControllerInput;
            this.SetupNanoController();
            this.UpdateLogStatus("Setup Event Handlers...");
            this.SetupOBSEventHandlers();
            this.UpdateLogStatus("Connected and Ready!");
        }

        public void Dispose()
        {
            this.UpdateLogStatus("Disposing...");
            for (byte cc = 16; cc < 70; cc++)
                this.nanoController.ToggleLED(cc, false);
            this.obsSocket.Close();
            foreach (SpecialSourceObject specialSource in this.specialSources.Values)
                specialSource.windowsDevice.Dispose();
            this.nanoController.Dispose();
            this.UpdateLogStatus("Finished. Goodbye!");
        }

        private void SetupOBSEventHandlers()
        {
            this.obsSocket.OnStreamStarted += (s, e) => { this.nanoController.ToggleLED(Control.play, true); };
            this.obsSocket.OnStreamStopped += (s, e) => { this.nanoController.ToggleLED(Control.play, false); };
            this.obsSocket.OnReplayStarted += (s, e) => { this.nanoController.ToggleLED(Control.record, true); };
            this.obsSocket.OnReplayStopped += (s, e) => { this.nanoController.ToggleLED(Control.record, false); };
            this.obsSocket.OnSourceMuteChanged += OnOBSSourceMuteChanged;
            this.obsSocket.OnSceneSwitched += OnSceneSwitched;
            this.obsSocket.OnStreamStatusUpdate += OnStreamStatusUpdate;
        }

        private void OnStreamStatusUpdate(object sender, OBSConnector.StreamStatusUpdateEventArgs e)
        {
            this.nanoController.ToggleLED(Control.play, e.status.streaming);
            this.nanoController.ToggleLED(Control.record, e.status.replayBufferActive);
            this.nanoController.ToggleLED(Control.back, e.status.totalStreamTime % 4 == 0);
            this.nanoController.ToggleLED(Control.forward, e.status.totalStreamTime % 4 != 0);
        }

        private void OnSceneSwitched(object sender, OBSConnector.SceneSwitchedEventArgs e)
        {
            string currentScene = e.newSceneName;
            for (byte soloButtonIndex = 0; soloButtonIndex < this.obsScenes.Length && soloButtonIndex < 8; soloButtonIndex++)
            {
                if (currentScene.Equals(this.obsScenes[soloButtonIndex]))
                    this.nanoController.ToggleLED(GroupControls.solo, soloButtonIndex, true);
                else
                    this.nanoController.ToggleLED(GroupControls.solo, soloButtonIndex, false);
            }
        }

        private void OnOBSSourceMuteChanged(object sender, OBSConnector.SourceMuteChangedEventArgs e)
        {
            foreach (SpecialSourceObject potentialSender in this.specialSources.Values)
                if (potentialSender.obsSourceName.Equals(e.sourcename))
                {
                    if (potentialSender.specialSourceType.Equals(SpecialSourceType.desktop1))
                        this.nanoController.ToggleLED(GroupControls.r, 0, !e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.desktop2))
                        this.nanoController.ToggleLED(GroupControls.r, 1, !e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic1))
                        this.nanoController.ToggleLED(GroupControls.r, 2, !e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic2))
                        this.nanoController.ToggleLED(GroupControls.r, 3, !e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic3))
                        this.nanoController.ToggleLED(GroupControls.r, 4, !e.muted);
                    break;
                }
        }

        private void SetupAudio()
        {
            SpecialSources obsSpecialSources = obsSocket.GetSpecialSources();
            this.specialSources = new Dictionary<SpecialSourceType, SpecialSourceObject>()
            {
                {
                    SpecialSourceType.desktop1, new SpecialSourceObject(SpecialSourceType.desktop1) {
                        obsSourceName = obsSpecialSources.sources[SpecialSourceType.desktop1],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[SpecialSourceType.desktop1]).Split('}')[1].Substring(2))
                    }
                },{
                    SpecialSourceType.desktop2, new SpecialSourceObject(SpecialSourceType.desktop2) {
                        obsSourceName = obsSpecialSources.sources[SpecialSourceType.desktop2],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[SpecialSourceType.desktop2]).Split('}')[1].Substring(2))
                    }
                },{
                    SpecialSourceType.mic1, new SpecialSourceObject(SpecialSourceType.mic1) {
                        obsSourceName = obsSpecialSources.sources[SpecialSourceType.mic1],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[SpecialSourceType.mic1]).Split('}')[1].Substring(2))
                    }
                },{
                    SpecialSourceType.mic2, new SpecialSourceObject(SpecialSourceType.mic2) {
                        obsSourceName = obsSpecialSources.sources[SpecialSourceType.mic2],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[SpecialSourceType.mic2]).Split('}')[1].Substring(2))
                    }
                },{
                    SpecialSourceType.mic3, new SpecialSourceObject(SpecialSourceType.mic3) {
                        obsSourceName = obsSpecialSources.sources[SpecialSourceType.mic3],
                        windowsDevice = new AudioDevice(this.obsSocket.GetPIDOfAudioDevice(obsSpecialSources.sources[SpecialSourceType.mic3]).Split('}')[1].Substring(2))
                    }
                }
            };
            this.specialSources[SpecialSourceType.desktop1].windowsDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
            this.specialSources[SpecialSourceType.desktop2].windowsDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
            this.specialSources[SpecialSourceType.mic1].windowsDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
            this.specialSources[SpecialSourceType.mic2].windowsDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
            this.specialSources[SpecialSourceType.mic3].windowsDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
        }

        private void WindowsDevice_OnMuteStateChanged(object sender, AudioDevice.OnMuteStateChangedEventArgs e)
        {
            foreach(SpecialSourceObject potentialSender in this.specialSources.Values)
                if (potentialSender.windowsDevice.Equals(sender))
                {
                    if (potentialSender.specialSourceType.Equals(SpecialSourceType.desktop1))
                        this.nanoController.ToggleLED(GroupControls.mute, 0, e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.desktop2))
                        this.nanoController.ToggleLED(GroupControls.mute, 1, e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic1))
                        this.nanoController.ToggleLED(GroupControls.mute, 2, e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic2))
                        this.nanoController.ToggleLED(GroupControls.mute, 3, e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic3))
                        this.nanoController.ToggleLED(GroupControls.mute, 4, e.muted);
                    break;
                }

        }

        private void SetupNanoController()
        {
            this.obsScenes = this.obsSocket.GetSceneList();
            string currentScene = this.obsSocket.GetCurrentScene();
            for(byte soloButtonIndex = 0; soloButtonIndex < this.obsScenes.Length && soloButtonIndex < 8; soloButtonIndex++)
            {
                if (currentScene.Equals(this.obsScenes[soloButtonIndex]))
                    this.nanoController.ToggleLED(GroupControls.solo, soloButtonIndex, true);
                else
                    this.nanoController.ToggleLED(GroupControls.solo, soloButtonIndex, false);
            }

            this.nanoController.ToggleLED(GroupControls.r, 0, !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.desktop1].obsSourceName));
            this.nanoController.ToggleLED(GroupControls.r, 1, !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.desktop2].obsSourceName));
            this.nanoController.ToggleLED(GroupControls.r, 2, !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.mic1].obsSourceName));
            this.nanoController.ToggleLED(GroupControls.r, 3, !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.mic2].obsSourceName));
            this.nanoController.ToggleLED(GroupControls.r, 4, !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.mic3].obsSourceName));

            this.nanoController.ToggleLED(GroupControls.mute, 0, this.specialSources[SpecialSourceType.desktop1].windowsDevice.IsMuted());
            this.nanoController.ToggleLED(GroupControls.mute, 1, this.specialSources[SpecialSourceType.desktop2].windowsDevice.IsMuted());
            this.nanoController.ToggleLED(GroupControls.mute, 2, this.specialSources[SpecialSourceType.mic1].windowsDevice.IsMuted());
            this.nanoController.ToggleLED(GroupControls.mute, 3, this.specialSources[SpecialSourceType.mic2].windowsDevice.IsMuted());
            this.nanoController.ToggleLED(GroupControls.mute, 4, this.specialSources[SpecialSourceType.mic3].windowsDevice.IsMuted());

            StreamStatus stats = this.obsSocket.GetStreamingStatus();
            this.nanoController.ToggleLED(Control.play, stats.streaming);
        }
        private void OnNanoControllerInput(object sender, Controller.MidiMessageReceivedEventArgs e)
        {
            this.LogInfo("Button {0} pressed! ({1})", e.control.ToString(), e.value.ToString());
            switch (e.control)
            {
                //Sliders
                case 0:
                    this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.desktop1].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 1:
                    this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.desktop2].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 2:
                    this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.mic1].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 3:
                    this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.mic2].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 4:
                    this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.mic3].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                //Dials
                case 16:
                    this.specialSources[SpecialSourceType.desktop1].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 17:
                    this.specialSources[SpecialSourceType.desktop2].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 18:
                    this.specialSources[SpecialSourceType.mic1].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 19:
                    this.specialSources[SpecialSourceType.mic2].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 20:
                    this.specialSources[SpecialSourceType.mic3].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                //Mute
                case 48:
                    this.specialSources[SpecialSourceType.desktop1].windowsDevice.ToggleMute();
                    break;
                case 49:
                    this.specialSources[SpecialSourceType.desktop2].windowsDevice.ToggleMute();
                    break;
                case 50:
                    this.specialSources[SpecialSourceType.mic1].windowsDevice.ToggleMute();
                    break;
                case 51:
                    this.specialSources[SpecialSourceType.mic2].windowsDevice.ToggleMute();
                    break;
                case 52:
                    this.specialSources[SpecialSourceType.mic3].windowsDevice.ToggleMute();
                    break;
                //R
                case 64:
                    this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.desktop1].obsSourceName);
                    break;
                case 65:
                    this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.desktop2].obsSourceName);
                    break;
                case 66:
                    this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.mic1].obsSourceName);
                    break;
                case 67:
                    this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.mic2].obsSourceName);
                    break;
                case 68:
                    this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.mic3].obsSourceName);
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
            return string.Empty;
        }

        private string GetNanoKontrolOutputDeviceName()
        {
            foreach (string potentialName in MidiInformation.GetOutputDevices())
                if (potentialName.Contains("nano"))
                    return potentialName;
            return string.Empty;
        }

        public event LogEventHandler OnInfoLog;
        public event LogEventHandler OnWarningLog;
        public event LogEventHandler OnStatusLog;
        
        public delegate void LogEventHandler(object sender, LogEventArgs e);
        public class LogEventArgs : EventArgs
        {
            public string text;
        }

        private void LogWarning(string format, params string[] replace)
        {
            this.OnWarningLog?.Invoke(this, new LogEventArgs() { text = string.Format(format, replace) });
        }

        private void LogInfo(string format, params string[] replace)
        {
            this.OnInfoLog?.Invoke(this, new LogEventArgs() { text = string.Format(format, replace) });
        }

        private void UpdateLogStatus(string format, params string[] replace)
        {
            this.OnStatusLog?.Invoke(this, new LogEventArgs() { text = string.Format(format, replace) });
        }
    }
}
