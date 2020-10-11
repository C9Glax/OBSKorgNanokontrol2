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
        private Config bindingConfig;
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
            this.UpdateLogStatus("Loading Bindings...");
            this.bindingConfig = new Config(this, @".\config.xml");
            this.UpdateLogStatus("Connecting to websocket...");
            this.obsSocket = new OBSConnector(url, password);
            this.obsSocket.OnOBSWebsocketInfo += (s, e) => { this.OnInfoLog?.Invoke(s, new LogEventArgs() { text = e.text }); };
            this.obsSocket.OnOBSWebsocketWarning += (s, e) => { this.OnWarningLog?.Invoke(s, new LogEventArgs() { text = e.text }); };
            this.UpdateLogStatus("Setting up audio (This might take a while)...");
            this.SetupAudio();
            this.UpdateLogStatus("Connecting nanoKontrol2...");
            this.nanoController = new Controller(GetNanoKontrolInputDeviceName(), GetNanoKontrolOutputDeviceName());

            for (byte cc = 16; cc < 70; cc++)//Fancy Animation (Seems like it also helps debugging stuff lol)
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

        private void OnStreamStatusUpdate(object sender, OBSConnector.StreamStatusUpdateEventArgs e)
        {
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.streamstatuschanged), e.streaming);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.replaystatuschanged), e.replayBufferActive);
        }

        private void OnSceneSwitched(object sender, OBSConnector.SceneSwitchedEventArgs e)
        {
            string currentScene = e.newSceneName;
            for (byte soloButtonIndex = 0; soloButtonIndex < this.obsScenes.Length && soloButtonIndex < 8; soloButtonIndex++)
            {
                if (currentScene.Equals(this.obsScenes[soloButtonIndex]))
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.sceneswitched), true);
                else
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.sceneswitched), false);
            }
        }

        private void OnOBSSourceMuteChanged(object sender, OBSConnector.SourceMuteChangedEventArgs e)
        {
            foreach (SpecialSourceObject potentialSender in this.specialSources.Values)
                if (potentialSender.obsSourceName.Equals(e.sourceName))
                {
                    if (potentialSender.specialSourceType.Equals(SpecialSourceType.desktop1))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.desktop1), !e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.desktop2))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.desktop2), !e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic1))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic1), !e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic2))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic2), !e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic3))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic3), !e.muted);
                    break;
                }
        }

        private void WindowsDevice_OnMuteStateChanged(object sender, AudioDevice.OnMuteStateChangedEventArgs e)
        {
            foreach (SpecialSourceObject potentialSender in this.specialSources.Values)
                if (potentialSender.windowsDevice.Equals(sender))
                {
                    if (potentialSender.specialSourceType.Equals(SpecialSourceType.desktop1))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.desktop1), e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.desktop2))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.desktop2), e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic1))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic1), e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic2))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic2), e.muted);
                    else if (potentialSender.specialSourceType.Equals(SpecialSourceType.mic3))
                        this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic3), e.muted);
                    break;
                }
        }

        private void OnNanoControllerInput(object sender, Controller.MidiMessageReceivedEventArgs e)
        {
            if (this.bindingConfig.inputbindings.ContainsKey(e.control))
            {
                Config.Action operation = this.bindingConfig.inputbindings[e.control];
                switch (operation.action)
                {
                    case Config.action.nexttrack:
                        keybd_event(0xB0, 0, 1, IntPtr.Zero);
                        break;
                    case Config.action.previoustrack:
                        keybd_event(0xB1, 0, 1, IntPtr.Zero);
                        break;
                    case Config.action.playpause:
                        keybd_event(0xB3, 0, 1, IntPtr.Zero);
                        break;
                    case Config.action.obsmute:
                        if (this.specialSources[operation.source].connected)
                            this.obsSocket.ToggleMute(this.specialSources[operation.source].obsSourceName);
                        break;
                    case Config.action.windowsmute:
                        if (this.specialSources[operation.source].connected)
                            this.specialSources[operation.source].windowsDevice.ToggleMute();
                        break;
                    case Config.action.setobsvolume:
                        if (this.specialSources[operation.source].connected)
                            this.obsSocket.SetVolume(this.specialSources[operation.source].obsSourceName, Convert.ToDouble(e.value).Map(0, 127, 0, 1));
                        break;
                    case Config.action.setwindowsvolume:
                        if (this.specialSources[operation.source].connected)
                            this.specialSources[operation.source].windowsDevice.SetVolume(Convert.ToDouble(e.value).Map(0, 127, 0, 100));
                        break;
                    case Config.action.savereplay:
                        this.obsSocket.SaveReplayBuffer();
                        break;
                    case Config.action.startstopstream:
                        this.obsSocket.StartStopStreaming();
                        break;
                    case Config.action.switchscene:
                        Scene[] scenes = this.obsSocket.GetSceneList().scenes;
                        if(operation.index <= scenes.Length)
                            this.obsSocket.SetCurrentScene(scenes[operation.index].name);
                        break;
                }
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

            foreach (SpecialSourceObject specialSource in this.specialSources.Values)
            {
                specialSource.connected = connectedSpecialSources.Contains(specialSource.obsSourceName);
                if (specialSource.connected)
                {
                    string pid = this.obsSocket.GetPIDOfAudioDevice(specialSource.obsSourceName);
                    if(pid != "default")
                    {
                        string guid = pid.Replace("}.{", "@").Split('@')[1].Substring(0, 36);
                        specialSource.windowsDevice = new AudioDevice(guid);
                        specialSource.windowsDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
                    }
                    else
                    {
                        this.LogWarning("Audio-source \"{0}\" is assigned per default. Windows volume control will not be available, unless you set a specific Source.");
                    }
                }
            }
        }

        private void SetupNanoController()
        {
            this.obsScenes = this.obsSocket.GetSceneList().scenes;
            string currentScene = this.obsSocket.GetCurrentScene().name;
            for (byte soloButtonIndex = 0; soloButtonIndex < this.obsScenes.Length && soloButtonIndex < 8; soloButtonIndex++)
            {
                if (currentScene.Equals(this.obsScenes[soloButtonIndex]))
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.sceneswitched), true);
                else
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.sceneswitched), false);
            }

#pragma warning disable IDE0075
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.desktop1),this.specialSources[SpecialSourceType.desktop1].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.desktop1].obsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.desktop2), this.specialSources[SpecialSourceType.desktop2].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.desktop2].obsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic1), this.specialSources[SpecialSourceType.mic1].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.mic1].obsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic2), this.specialSources[SpecialSourceType.mic2].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.mic2].obsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic3), this.specialSources[SpecialSourceType.mic3].connected ? !this.obsSocket.GetMute(this.specialSources[SpecialSourceType.mic3].obsSourceName) : false);

            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.desktop1), this.specialSources[SpecialSourceType.desktop2].connected ? this.specialSources[SpecialSourceType.desktop1].windowsDevice.IsMuted() : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.desktop2), this.specialSources[SpecialSourceType.desktop1].connected ? this.specialSources[SpecialSourceType.desktop2].windowsDevice.IsMuted() : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic1), this.specialSources[SpecialSourceType.mic1].connected ? this.specialSources[SpecialSourceType.mic1].windowsDevice.IsMuted() : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic2), this.specialSources[SpecialSourceType.mic2].connected ? this.specialSources[SpecialSourceType.mic2].windowsDevice.IsMuted() : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic3), this.specialSources[SpecialSourceType.mic3].connected ? this.specialSources[SpecialSourceType.mic3].windowsDevice.IsMuted() : false);
#pragma warning restore IDE0075

            GetStreamingStatusObject stats = this.obsSocket.GetStreamingStatus();
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.streamstatuschanged), stats.streaming);
        }

        private void SetupOBSEventHandlers()
        {
            this.obsSocket.OnStreamStarted += (s, e) => { this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.streamstatuschanged), true); };
            this.obsSocket.OnStreamStopped += (s, e) => { this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.streamstatuschanged), false); };
            this.obsSocket.OnReplayStarted += (s, e) => { this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.replaystatuschanged), true); };
            this.obsSocket.OnReplayStopped += (s, e) => { this.nanoController.ToggleLED(this.bindingConfig.GetOutputToEvent(Config.outputevent.replaystatuschanged), false); };
            this.obsSocket.OnSourceMuteChanged += OnOBSSourceMuteChanged;
            this.obsSocket.OnSceneSwitched += OnSceneSwitched;
            this.obsSocket.OnStreamStatusUpdate += OnStreamStatusUpdate;
        }

        private string GetNanoKontrolInputDeviceName()
        {
            foreach(string potentialName in MidiInformation.GetInputDevices())
                if (potentialName.ToLower().Contains("nano"))
                    return potentialName;
            this.LogWarning("Unable to find nanoKontrol device!");
            foreach (string notNanoKontrol in MidiInformation.GetInputDevices())
                this.LogWarning("Device: {0}", notNanoKontrol);
           return string.Empty;
        }

        private string GetNanoKontrolOutputDeviceName()
        {
            foreach (string potentialName in MidiInformation.GetOutputDevices())
                if (potentialName.ToLower().Contains("nano"))
                    return potentialName;
            this.LogWarning("Unable to find nanoKontrol device!");
            foreach (string notNanoKontrol in MidiInformation.GetInputDevices())
                this.LogWarning("Device: {0}", notNanoKontrol);
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

        public void LogWarning(string format, params string[] replace)
        {
            this.OnWarningLog?.Invoke(this, new LogEventArgs() { text = string.Format(format, replace) });
        }

        public void LogInfo(string format, params string[] replace)
        {
            this.OnInfoLog?.Invoke(this, new LogEventArgs() { text = string.Format(format, replace) });
        }

        public void UpdateLogStatus(string format, params string[] replace)
        {
            this.OnStatusLog?.Invoke(this, new LogEventArgs() { text = string.Format(format, replace) });
        }
    }
}
