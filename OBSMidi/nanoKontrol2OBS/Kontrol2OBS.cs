/*
 * Main Class and entry point
 * Tra and ConsoleExecutable both call here
 */

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MidiAccess;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;
using SoundControl;

namespace Linker
{
    static class ExtensionMethod
    {
        /*
         * How is this not a standard-method...
         * Maps a linear range to a smaller/larger one
         */
        public static float Map(this float value, float low1, float high1, float low2, float high2)
        {
            return (value - low1) / (high1 - low1) * (high2 - low2) + low2;
        }
    }

    public partial class Kontrol2OBS
    {
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
        private Config bindingConfig;
        private OBSWebsocket obsSocket;
        private Controller nanoController;
        private Dictionary<SpecialSourceType, SpecialSourceObject> specialSources;
        private string[] obsSceneNames;
        private event LogEventHandler OnInfoLog, OnWarningLog, OnStatusLog, onErrorLog;

        private EventClock eventBuffer;

        /*
         * Complete Setup
         * url: location of obs-websocket (ip+port)
         * password: password
         * statusCallback: Method to call in case of a Status-Update
         * warningCallback: Method to call in case of a Warning-Message
         * infoCallback: Method to call in case of a Info-Message
         */
        public Kontrol2OBS(string url, string password, LogEventHandler statusCallback, LogEventHandler warningCallback, LogEventHandler infoCallback, LogEventHandler errorCallback)
        {
            this.OnStatusLog = statusCallback;
            this.OnWarningLog = warningCallback;
            this.OnInfoLog = infoCallback;
            this.onErrorLog = errorCallback;
            
            this.UpdateLogStatus("Loading Bindings...");
            this.bindingConfig = new Config(this, @"config.xml");

            this.UpdateLogStatus("Connecting to websocket...");
            this.obsSocket = new OBSWebsocket();
            this.obsSocket.Connected += (sender, args) =>
            {
                OnStatusLog?.Invoke(sender, new LogEventArgs()
                {
                    text = "Websocket Connected"
                });
                
                this.UpdateLogStatus("Setting up audio (This might take a while)...");
                this.SetupAudio();

                this.UpdateLogStatus("Connecting nanoKontrol2...");
                try
                {
                    this.nanoController = new Controller(GetNanoKontrolInputDeviceName(), GetNanoKontrolOutputDeviceName());
                }catch (Exception e)
                {
                    this.LogWarning($"ERROR: {e.Message}");
                    Environment.Exit(-1);
                    return;
                }

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
                this.SetupObsEventHandlers();

                this.eventBuffer = new EventClock(this, 20);

                this.UpdateLogStatus("Connected and Ready!");
            };
            this.obsSocket.Disconnected += (sender, info) =>
            {
                OnStatusLog?.Invoke(sender, new LogEventArgs()
                {
                    text = $"Websocket Disconnected\n{info.DisconnectReason}"
                });
                this.Dispose();
            };
            this.obsSocket.ConnectAsync($"ws://{url}", password);

            
        }

        /*
         * Tries to exit gracefully...
         * And then just kills it
         */
        public void Dispose()
        {
            this.UpdateLogStatus("Disposing...");

            if(this.eventBuffer is not null)
            //Stop sending to OBSWebsocket and WindowsAudio
                this.eventBuffer.Dispose();

            //Disconnect and turn off LEDs nanoKontrol2
            if (this.nanoController is not null)
            {
                for (byte cc = 16; cc < 70; cc++)
                    this.nanoController.ToggleLED(cc, false);
                this.nanoController.Dispose();
            }

            //Disconnect from WebSocket
            if(this.obsSocket.IsConnected)
                this.obsSocket.Disconnect();

            //Disconnect AudioDevices
            if(this.specialSources is not null)
                foreach (SpecialSourceObject specialSource in this.specialSources.Values)
                    if(specialSource.Connected && specialSource.AudioDevice != null)
                        specialSource.AudioDevice.Dispose();
            
            this.UpdateLogStatus("Finished. Goodbye!");
            Environment.Exit(0);
        }

        /*
         * Audio Device was un-/muted
         * Turn on/off specified LED
         */
        private void WindowsDevice_OnMuteStateChanged(object sender, bool muted)
        {
            SpecialSourceType device = this.specialSources.FirstOrDefault(ss => ss.Value.AudioDevice.Equals(sender)).Key;
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, device), muted);
        }

        /*
         * MIDI-Event was received
         * Perform action as set in config.xml
         */
        private void OnNanoControllerInput(object sender, Controller.MidiMessageReceivedEventArgs e)
        {
            if (this.bindingConfig.inputbindings.ContainsKey(e.control))
            {
                Config.Action operation = this.bindingConfig.inputbindings[e.control];
                switch (operation.action)
                {
                    case Config.action.nexttrack:
                        keybd_event(0xB0, 0, 1, IntPtr.Zero); //Emulate keypress
                        break;
                    case Config.action.previoustrack:
                        keybd_event(0xB1, 0, 1, IntPtr.Zero); //Emulate keypress
                        break;
                    case Config.action.playpause:
                        keybd_event(0xB3, 0, 1, IntPtr.Zero); //Emulate keypress
                        break;
                    case Config.action.obsmute:
                        if (this.specialSources[operation.source].Connected)
                            this.eventBuffer.AddOBSEvent(() => {
                                this.obsSocket.ToggleInputMute(this.specialSources[operation.source].ObsSourceName);
                            });
                        break;
                    case Config.action.windowsmute:
                        if (this.specialSources[operation.source].Connected)
                            this.specialSources[operation.source].AudioDevice.ToggleMute();
                        break;
                    case Config.action.setobsvolume:
                        if (this.specialSources[operation.source].Connected)
                            this.eventBuffer.SetOBSVolume(this.specialSources[operation.source].ObsSourceName, Convert.ToSingle(e.value).Map(0, 127, 0, 1));
                        break;
                    case Config.action.setwindowsvolume:
                        if (this.specialSources[operation.source].Connected)
                            this.specialSources[operation.source].AudioDevice.SetVolume(Convert.ToSingle(e.value).Map(0, 127, 0, 100));
                        break;
                    case Config.action.savereplay:
                        this.eventBuffer.AddOBSEvent(this.obsSocket.SaveReplayBuffer);
                        break;
                    case Config.action.startstopstream:
                        this.eventBuffer.AddOBSEvent(() =>
                        {
                            this.obsSocket.ToggleStream();
                        });
                        break;
                    case Config.action.switchscene:
                        string[] scenes = this.obsSocket.GetSceneList().Scenes.Select(s => s.Name).ToArray();
                        if (operation.index < scenes.Length)
                            this.eventBuffer.AddOBSEvent(() => {
                                this.obsSocket.SetCurrentProgramScene(scenes[operation.index]);
                            });
                        break;
                }
            }
        }

        /*
         * Handles Initial Setup of AudioDevices
         * Connects OBS-AudioDevices with Windows-Devices (for this application)
         */
        private void SetupAudio()
        {
            Dictionary<string, string> specialInputs = this.obsSocket.GetSpecialInputs(); //Not very special actually...

            this.specialSources = specialInputs.ToDictionary(si => Enum.Parse<SpecialSourceType>(si.Key), si =>
            {
                InputSettings inputSettings = this.obsSocket.GetInputSettings(si.Value);
                LogInfo($"{si.Key} {si.Value}\n\t{inputSettings.InputKind}\n\t{inputSettings.Settings}");
                AudioDevice audioDevice = AudioDevice.Default;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    audioDevice = new WindowsAudio("");//TODO Get GUID
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    audioDevice = new AlsaAudio(inputSettings.Settings.Value<string>("device_id")!); //TODO Check if ALSA
                else
                    Environment.Exit(-1);
                return new SpecialSourceObject(si.Value, audioDevice, true); //TODO check connected
            });

            /*
            foreach (SpecialSourceObject specialSource in this.specialSources.Values)
            {
                specialSource.connected = connectedSpecialSources.Contains(specialSource.ObsSourceName);
                if (specialSource.connected)
                {
                    string pid = this.obsSocket.GetPIDOfAudioDevice(specialSource.ObsSourceName);
                    if(pid != "default") //OBS-"default"-assigned devices-PID is not transmitted by WebSocket. So we cant actually control their volume...
                    {
                        string guid = pid.Replace("}.{", "@").Split('@')[1].Substring(0, 36); //Convert the PID to GUID Format
                        try //If the device is not actually connected
                        {
                            specialSource.windowsDevice = new AudioDevice(guid);
                            specialSource.windowsDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
                        }
                        catch(Exception e) {
                            this.LogWarning(e.Message);
                            this.Dispose();
                        }
                    }
                    else
                    {
                        this.LogWarning("Audio-source \"{0}\" is assigned per default by OBS. Windows volume control will not be available, unless you set a specific Source.", specialSource.ObsSourceName);
                    }
                }
            }*/
        }

        /*
         * Handles initial Setup of the nanoKontrol2
         */
        private void SetupNanoController()
        {
            /*
             * Toggle LEDs on/off if OBS-Scene is active
             */
            this.obsSceneNames = this.obsSocket.GetSceneList().Scenes.Select(s => s.Name).ToArray();
            string currentScene = this.obsSocket.GetCurrentProgramScene();
            for (byte soloButtonIndex = 0; soloButtonIndex < this.obsSceneNames.Length && soloButtonIndex < 8; soloButtonIndex++)
            {
                if (currentScene.Equals(this.obsSceneNames[soloButtonIndex]))
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.sceneswitched, soloButtonIndex), true);
                else
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.sceneswitched, soloButtonIndex), false);
            }

            /*
             * Toggle LEDs on/off if OBS-Audio is un-/muted
             * Read at own risk.
             */
#pragma warning disable IDE0075
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.desktop1),this.specialSources[SpecialSourceType.desktop1].Connected ? !this.obsSocket.GetInputMute(this.specialSources[SpecialSourceType.desktop1].ObsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.desktop2), this.specialSources[SpecialSourceType.desktop2].Connected ? !this.obsSocket.GetInputMute(this.specialSources[SpecialSourceType.desktop2].ObsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic1), this.specialSources[SpecialSourceType.mic1].Connected ? !this.obsSocket.GetInputMute(this.specialSources[SpecialSourceType.mic1].ObsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic2), this.specialSources[SpecialSourceType.mic2].Connected ? !this.obsSocket.GetInputMute(this.specialSources[SpecialSourceType.mic2].ObsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic3), this.specialSources[SpecialSourceType.mic3].Connected ? !this.obsSocket.GetInputMute(this.specialSources[SpecialSourceType.mic3].ObsSourceName) : false);

            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.desktop1), this.specialSources[SpecialSourceType.desktop1].Connected && this.specialSources[SpecialSourceType.desktop1].AudioDevice.IsMuted());
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.desktop2), this.specialSources[SpecialSourceType.desktop2].Connected && this.specialSources[SpecialSourceType.desktop2].AudioDevice.IsMuted());
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic1), this.specialSources[SpecialSourceType.mic1].Connected && this.specialSources[SpecialSourceType.mic1].AudioDevice.IsMuted());
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic2), this.specialSources[SpecialSourceType.mic2].Connected && this.specialSources[SpecialSourceType.mic2].AudioDevice.IsMuted());
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic3), this.specialSources[SpecialSourceType.mic3].Connected && this.specialSources[SpecialSourceType.mic3].AudioDevice.IsMuted());
#pragma warning restore IDE0075

            /*
             * Toggle LED on/off if stream is in-/active
             */
            bool streaming = this.obsSocket.GetStreamStatus().IsActive;
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.streamstatuschanged), streaming);
        }

        /*
         * Handles LED-toggeling in case of OBS-Event (WebSocket)
         */
        private void SetupObsEventHandlers()
        {
            this.obsSocket.StreamStateChanged += (sender, args) =>
            {
                switch (args.OutputState.State)
                {
                    case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                        this.nanoController.ToggleLED(
                            this.bindingConfig.GetOutputForEvent(Config.outputevent.streamstatuschanged), true);
                        break;
                    case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                        this.nanoController.ToggleLED(
                            this.bindingConfig.GetOutputForEvent(Config.outputevent.streamstatuschanged), false);
                        break;
                }
            };

            this.obsSocket.ReplayBufferStateChanged += (sender, args) =>
            {
                switch (args.OutputState.State)
                {
                    case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                        this.nanoController.ToggleLED(
                            this.bindingConfig.GetOutputForEvent(Config.outputevent.replaystatuschanged), true);
                        break;
                    case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                        this.nanoController.ToggleLED(
                            this.bindingConfig.GetOutputForEvent(Config.outputevent.replaystatuschanged), false);
                        break;
                }
            };

            this.obsSocket.InputMuteStateChanged += ObsSocketOnInputMuteStateChanged;
            this.obsSocket.SceneNameChanged += ObsSocketOnSceneNameChanged;
        }

        private void ObsSocketOnSceneNameChanged(object? sender, SceneNameChangedEventArgs e)
        {
            string currentScene = e.SceneName;
            for (byte soloButtonIndex = 0; soloButtonIndex < this.obsSceneNames.Length && soloButtonIndex < 8; soloButtonIndex++)
            {
                if (currentScene.Equals(this.obsSceneNames[soloButtonIndex]))
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.sceneswitched,soloButtonIndex), true);
                else
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.sceneswitched,soloButtonIndex), false);
            }
        }

        private void ObsSocketOnInputMuteStateChanged(object? sender, InputMuteStateChangedEventArgs e)
        {
            if (!this.specialSources.Any(ss => ss.Value.ObsSourceName.Equals(e.InputName)))
                return;

            SpecialSourceType t = this.specialSources.First(ss => ss.Value.ObsSourceName.Equals(e.InputName)).Key;
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, t), e.InputMuted);
        }

        private string GetNanoKontrolInputDeviceName()
        {
            foreach(string potentialName in MidiInformation.GetInputDevices())
                if (potentialName.ToLower().Contains("nano"))
                    return potentialName;
            this.LogWarning("Unable to find nanoKontrol device!");
            foreach (string notNanoKontrol in MidiInformation.GetInputDevices())
                this.LogWarning($"Device: {notNanoKontrol}");
            return string.Empty;
        }

        private string GetNanoKontrolOutputDeviceName()
        {
            foreach (string potentialName in MidiInformation.GetOutputDevices())
                if (potentialName.ToLower().Contains("nano"))
                    return potentialName;
            this.LogWarning("Unable to find nanoKontrol device!");
            foreach (string notNanoKontrol in MidiInformation.GetInputDevices())
                this.LogWarning($"Device: {notNanoKontrol}");
            return string.Empty;
        }
        
        /*
         * Custom EventHandler
         */
        public delegate void LogEventHandler(object sender, LogEventArgs e);
        public class LogEventArgs : EventArgs
        {
            public string text;
        }

        /*
         * Invokes a Warning-Message
         * Also takes care of formatting
         */
        public void LogWarning(string format)
        {
            this.OnWarningLog?.Invoke(this, new LogEventArgs() { text = format });
        }
        
        /*
         * Invokes a Info-Message
         * Also takes care of formatting
         */
        public void LogError(string format)
        {
            this.onErrorLog?.Invoke(this, new LogEventArgs() { text = format });
        }

        /*
         * Invokes a Info-Message
         * Also takes care of formatting
         */
        public void LogInfo(string format)
        {
            this.OnInfoLog?.Invoke(this, new LogEventArgs() { text = format });
        }

        /*
         * Invokes a Status-Update
         * Also takes care of formatting
         */
        public void UpdateLogStatus(string format)
        {
            this.OnStatusLog?.Invoke(this, new LogEventArgs() { text = format });
        }
    }
}
