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
        private Scene[] obsScenes;
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
                if(specialSource.connected)
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
            this.nanoController.ToggleLED(Control.play, e.streaming);
            this.nanoController.ToggleLED(Control.record, e.replayBufferActive);
            if (e.streaming)
            {
                this.nanoController.ToggleLED(Control.back, e.totalStreamTime % 4 == 0);
                this.nanoController.ToggleLED(Control.forward, e.totalStreamTime % 4 != 0);
            }
            else
            {
                this.nanoController.ToggleLED(Control.back, false);
                this.nanoController.ToggleLED(Control.forward, false);
            }

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
                if (potentialSender.obsSourceName.Equals(e.sourceName))
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
            SpecialSources obsSpecialSources = this.obsSocket.GetSpecialSources();
            List<string> connectedSpecialSources = new List<string>();
            foreach (Source s in this.obsSocket.GetSourcesList())
                if (s.typeId.Contains("wasapi"))
                    connectedSpecialSources.Add(s.name);
            this.specialSources = new Dictionary<SpecialSourceType, SpecialSourceObject>()
            {
                {
                    SpecialSourceType.desktop1, new SpecialSourceObject(SpecialSourceType.desktop1) {
                        obsSourceName = obsSpecialSources.specialSourceNames[SpecialSourceType.desktop1]
                    }
                },{
                    SpecialSourceType.desktop2, new SpecialSourceObject(SpecialSourceType.desktop2) {
                        obsSourceName = obsSpecialSources.specialSourceNames[SpecialSourceType.desktop2]
                    }
                },{
                    SpecialSourceType.mic1, new SpecialSourceObject(SpecialSourceType.mic1) {
                        obsSourceName = obsSpecialSources.specialSourceNames[SpecialSourceType.mic1]
                    }
                },{
                    SpecialSourceType.mic2, new SpecialSourceObject(SpecialSourceType.mic2) {
                        obsSourceName = obsSpecialSources.specialSourceNames[SpecialSourceType.mic2]
                    }
                },{
                    SpecialSourceType.mic3, new SpecialSourceObject(SpecialSourceType.mic3) {
                        obsSourceName = obsSpecialSources.specialSourceNames[SpecialSourceType.mic3]
                    }
                }
            };

            foreach(SpecialSourceObject specialSource in this.specialSources.Values)
            {
                specialSource.connected = connectedSpecialSources.Contains(specialSource.obsSourceName);
                if (specialSource.connected)
                {
                    string pid = this.obsSocket.GetPIDOfAudioDevice(specialSource.obsSourceName);
                    string guid = pid.Replace("}.{", "@").Split('@')[1].Substring(0, 36);
                    specialSource.windowsDevice = new AudioDevice(guid);
                    specialSource.windowsDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
                }
            }
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
            this.obsScenes = this.obsSocket.GetSceneList().scenes;
            string currentScene = this.obsSocket.GetCurrentScene().name;
            for(byte soloButtonIndex = 0; soloButtonIndex < this.obsScenes.Length && soloButtonIndex < 8; soloButtonIndex++)
            {
                if (currentScene.Equals(this.obsScenes[soloButtonIndex]))
                    this.nanoController.ToggleLED(GroupControls.solo, soloButtonIndex, true);
                else
                    this.nanoController.ToggleLED(GroupControls.solo, soloButtonIndex, false);
            }

            this.nanoController.ToggleLED(GroupControls.r, 0, this.specialSources[SpecialSourceType.desktop1].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.desktop1].obsSourceName) : false);
            this.nanoController.ToggleLED(GroupControls.r, 1, this.specialSources[SpecialSourceType.desktop2].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.desktop2].obsSourceName) : false);
            this.nanoController.ToggleLED(GroupControls.r, 2, this.specialSources[SpecialSourceType.mic1].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.mic1].obsSourceName) : false);
            this.nanoController.ToggleLED(GroupControls.r, 3, this.specialSources[SpecialSourceType.mic2].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.mic2].obsSourceName) : false);
            this.nanoController.ToggleLED(GroupControls.r, 4, this.specialSources[SpecialSourceType.mic3].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.mic3].obsSourceName) : false);

            this.nanoController.ToggleLED(GroupControls.mute, 0, this.specialSources[SpecialSourceType.desktop2].connected ? this.specialSources[SpecialSourceType.desktop1].windowsDevice.IsMuted() : false);
            this.nanoController.ToggleLED(GroupControls.mute, 1, this.specialSources[SpecialSourceType.desktop1].connected ? this.specialSources[SpecialSourceType.desktop2].windowsDevice.IsMuted() : false);
            this.nanoController.ToggleLED(GroupControls.mute, 2, this.specialSources[SpecialSourceType.mic1].connected ? this.specialSources[SpecialSourceType.mic1].windowsDevice.IsMuted() : false);
            this.nanoController.ToggleLED(GroupControls.mute, 3, this.specialSources[SpecialSourceType.mic2].connected ? this.specialSources[SpecialSourceType.mic2].windowsDevice.IsMuted() : false);
            this.nanoController.ToggleLED(GroupControls.mute, 4, this.specialSources[SpecialSourceType.mic3].connected ? this.specialSources[SpecialSourceType.mic3].windowsDevice.IsMuted() : false);

            GetStreamingStatusObject stats = this.obsSocket.GetStreamingStatus();
            this.nanoController.ToggleLED(Control.play, stats.streaming);
        }

        private void OnNanoControllerInput(object sender, Controller.MidiMessageReceivedEventArgs e)
        {
            switch (e.control)
            {
                //Sliders
                case 0:
                    if(this.specialSources[SpecialSourceType.desktop1].connected)
                        this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.desktop1].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 1:
                    if (this.specialSources[SpecialSourceType.desktop2].connected)
                        this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.desktop2].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 2:
                    if (this.specialSources[SpecialSourceType.mic1].connected)
                        this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.mic1].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 3:
                    if (this.specialSources[SpecialSourceType.mic2].connected)
                        this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.mic2].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                case 4:
                    if (this.specialSources[SpecialSourceType.mic3].connected)
                        this.obsSocket.SetVolume(this.specialSources[SpecialSourceType.mic3].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                    break;
                //Dials
                case 16:
                    if (this.specialSources[SpecialSourceType.desktop1].connected)
                        this.specialSources[SpecialSourceType.desktop1].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 17:
                    if (this.specialSources[SpecialSourceType.desktop2].connected)
                        this.specialSources[SpecialSourceType.desktop2].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 18:
                    if (this.specialSources[SpecialSourceType.mic1].connected)
                        this.specialSources[SpecialSourceType.mic1].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 19:
                    if (this.specialSources[SpecialSourceType.mic2].connected)
                        this.specialSources[SpecialSourceType.mic2].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                case 20:
                    if (this.specialSources[SpecialSourceType.mic3].connected)
                        this.specialSources[SpecialSourceType.mic3].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                    break;
                //Mute
                case 48:
                    if (this.specialSources[SpecialSourceType.desktop1].connected)
                        this.specialSources[SpecialSourceType.desktop1].windowsDevice.ToggleMute();
                    break;
                case 49:
                    if (this.specialSources[SpecialSourceType.desktop2].connected)
                        this.specialSources[SpecialSourceType.desktop2].windowsDevice.ToggleMute();
                    break;
                case 50:
                    if (this.specialSources[SpecialSourceType.mic1].connected)
                        this.specialSources[SpecialSourceType.mic1].windowsDevice.ToggleMute();
                    break;
                case 51:
                    if (this.specialSources[SpecialSourceType.mic2].connected)
                        this.specialSources[SpecialSourceType.mic2].windowsDevice.ToggleMute();
                    break;
                case 52:
                    if (this.specialSources[SpecialSourceType.mic3].connected)
                        this.specialSources[SpecialSourceType.mic3].windowsDevice.ToggleMute();
                    break;
                //R
                case 64:
                    if (this.specialSources[SpecialSourceType.desktop1].connected)
                        this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.desktop1].obsSourceName);
                    break;
                case 65:
                    if (this.specialSources[SpecialSourceType.desktop2].connected)
                        this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.desktop2].obsSourceName);
                    break;
                case 66:
                    if (this.specialSources[SpecialSourceType.mic1].connected)
                        this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.mic1].obsSourceName);
                    break;
                case 67:
                    if (this.specialSources[SpecialSourceType.mic2].connected)
                        this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.mic2].obsSourceName);
                    break;
                case 68:
                    if (this.specialSources[SpecialSourceType.mic3].connected)
                        this.obsSocket.ToggleMute(this.specialSources[SpecialSourceType.mic3].obsSourceName);
                    break;
                //Play
                case 41:
                    this.obsSocket.StartStopStreaming();
                    break;
                //Stop
                case 42:
                    keybd_event(0xB3, 0, 1, IntPtr.Zero);
                    break;
                //Back
                case 43:
                    keybd_event(0xB1, 0, 1, IntPtr.Zero);
                    break;
                //Forward
                case 44:
                    keybd_event(0xB0, 0, 1, IntPtr.Zero);
                    break;
                case 45:
                    this.obsSocket.SaveReplayBuffer();
                    break;
            }
            //Solo
            if(e.control >= 32 && e.control <= 39)
            {
                Scene[] scenes = this.obsSocket.GetSceneList().scenes;
                if (scenes.Length > (e.control - 32))
                    this.obsSocket.SetCurrentScene(scenes[e.control - 32].name);
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
