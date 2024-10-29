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
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
        private readonly Config bindingConfig;
        internal readonly OBSWebsocket ObsSocket;
        private Controller nanoController;
        internal Dictionary<SpecialSourceType, SpecialSourceObject> SpecialSources;
        private string[] obsSceneNames;
        private event LogEventHandler OnInfoLog, OnWarningLog, OnStatusLog, OnErrorLog;

        private EventClock eventBuffer;

        /*
         * Complete Setup
         * url: location of obs-websocket (ip+port)
         * password: password
         * statusCallback: Method to call in case of a Status-Update
         * warningCallback: Method to call in case of a Warning-Message
         * infoCallback: Method to call in case of an Info-Message
         */
        public Kontrol2OBS(string url, string password, LogEventHandler statusCallback, LogEventHandler warningCallback, LogEventHandler infoCallback, LogEventHandler errorCallback)
        {
            this.OnStatusLog = statusCallback;
            this.OnWarningLog = warningCallback;
            this.OnInfoLog = infoCallback;
            this.OnErrorLog = errorCallback;
            
            this.UpdateLogStatus("Loading Bindings...");
            this.bindingConfig = new Config(this, @"config.xml");

            this.UpdateLogStatus("Connecting to websocket...");
            this.ObsSocket = new OBSWebsocket();
            this.ObsSocket.Connected += (sender, _) =>
            {
                OnStatusLog.Invoke(sender ?? this, new LogEventArgs("Websocket Connected"));
                
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
            this.ObsSocket.Disconnected += (sender, info) =>
            {
                OnStatusLog.Invoke(sender ?? this, new LogEventArgs($"Websocket Disconnected\n{info.DisconnectReason}"));
                this.Dispose();
            };
            this.ObsSocket.ConnectAsync($"ws://{url}", password);

            
        }

        /*
         * Tries to exit gracefully...
         * And then just kills it
         */
        public void Dispose()
        {
            this.UpdateLogStatus("Disposing...");

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            // Is null on setup
            if(this.eventBuffer is not null)
            //Stop sending to OBSWebsocket and WindowsAudio
                this.eventBuffer.Dispose();

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            // Is null on setup
            //Disconnect and turn off LEDs nanoKontrol2
            if (this.nanoController is not null)
            {
                for (byte cc = 16; cc < 70; cc++)
                    this.nanoController.ToggleLED(cc, false);
                this.nanoController.Dispose();
            }

            //Disconnect from WebSocket
            if(this.ObsSocket.IsConnected)
                this.ObsSocket.Disconnect();

            // ReSharper disable twice ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            // Is null on setup
            //Disconnect AudioDevices
            if(this.SpecialSources is not null)
                foreach (SpecialSourceObject specialSource in this.SpecialSources.Values)
                    if(specialSource is { Connected: true, AudioDevice: not null })
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
            SpecialSourceType device = this.SpecialSources.FirstOrDefault(ss => ss.Value.AudioDevice.Equals(sender)).Key;
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
                        if (this.SpecialSources.ContainsKey(operation.source) && this.SpecialSources[operation.source].Connected)
                            this.eventBuffer.AddOBSEvent(() => {
                                this.ObsSocket.ToggleInputMute(this.SpecialSources[operation.source].ObsSourceName);
                            });
                        break;
                    case Config.action.windowsmute:
                        if (this.SpecialSources.ContainsKey(operation.source) && this.SpecialSources[operation.source].Connected)
                            this.SpecialSources[operation.source].AudioDevice.ToggleMute();
                        break;
                    case Config.action.setobsvolume:
                        if (this.SpecialSources.ContainsKey(operation.source) && this.SpecialSources[operation.source].Connected)
                            this.eventBuffer.SetOBSVolume(this.SpecialSources[operation.source].ObsSourceName, Convert.ToSingle(e.value).Map(0, 127, 0, 1));
                        break;
                    case Config.action.setwindowsvolume:
                        if (this.SpecialSources.ContainsKey(operation.source) && this.SpecialSources[operation.source].Connected)
                            this.SpecialSources[operation.source].AudioDevice.SetVolume(Convert.ToSingle(e.value).Map(0, 127, 0, 100));
                        break;
                    case Config.action.savereplay:
                        this.eventBuffer.AddOBSEvent(this.ObsSocket.SaveReplayBuffer);
                        break;
                    case Config.action.startstopstream:
                        this.eventBuffer.AddOBSEvent(() =>
                        {
                            this.ObsSocket.ToggleStream();
                        });
                        break;
                    case Config.action.switchscene:
                        string[] scenes = this.ObsSocket.GetSceneList().Scenes.Select(s => s.Name).ToArray();
                        Array.Reverse(scenes);
                        if (operation.index < scenes.Length)
                            this.eventBuffer.AddOBSEvent(() => {
                                this.ObsSocket.SetCurrentProgramScene(scenes[operation.index]);
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
            Dictionary<string, string> specialInputs = this.ObsSocket.GetSpecialInputs().Where(i => i.Value is not null).ToDictionary(x => x.Key, x => x.Value); //Not very special actually...

            this.SpecialSources = specialInputs.ToDictionary(si => Enum.Parse<SpecialSourceType>(si.Key), si =>
            {
                InputSettings inputSettings = this.ObsSocket.GetInputSettings(si.Value);
                AudioDevice audioDevice = AudioDevice.Default;
                Regex deviceIdRex = new Regex(@"{[0-9a-z\.\-]+}\.{([0-9a-z\-]+)}");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && inputSettings.InputKind.Contains("wasapi"))
                {
                    string? deviceId = inputSettings.Settings.Value<string>("device_id");
                    if (deviceId is null || !deviceIdRex.IsMatch(deviceId))
                    {
                        LogError("Missing or invalid device_id");
                        LogError($"{si.Key} {si.Value}\n\t{inputSettings.InputKind}\n\t{inputSettings.Settings}");
                        Environment.Exit(-1);
                    }
                    audioDevice = new WindowsAudio(deviceIdRex.Match(deviceId).Groups[1].Value);
                    audioDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    audioDevice = new AlsaAudio(inputSettings.Settings.Value<string>("device_id")!); //TODO Check if ALSA
                    audioDevice.OnMuteStateChanged += WindowsDevice_OnMuteStateChanged;
                }
                else
                {
                    LogError("Mismatch between device and Platform or unsupported.");
                    LogError($"{si.Key} {si.Value}\n\t{inputSettings.InputKind}\n\t{inputSettings.Settings}");
                    Environment.Exit(-1);
                }
                return new SpecialSourceObject(si.Value, audioDevice, true); //TODO check connected
            });
        }

        /*
         * Handles initial Setup of the nanoKontrol2
         */
        private void SetupNanoController()
        {
            /*
             * Toggle LEDs on/off if OBS-Scene is active
             */
            this.obsSceneNames = this.ObsSocket.GetSceneList().Scenes.Select(s => s.Name).ToArray();
            Array.Reverse(this.obsSceneNames);
            string currentScene = this.ObsSocket.GetCurrentProgramScene();
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
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.desktop1), this.SpecialSources.ContainsKey(SpecialSourceType.desktop1) && this.SpecialSources[SpecialSourceType.desktop1].Connected ? !this.ObsSocket.GetInputMute(this.SpecialSources[SpecialSourceType.desktop1].ObsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.desktop2), this.SpecialSources.ContainsKey(SpecialSourceType.desktop2) && this.SpecialSources[SpecialSourceType.desktop2].Connected ? !this.ObsSocket.GetInputMute(this.SpecialSources[SpecialSourceType.desktop2].ObsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic1), this.SpecialSources.ContainsKey(SpecialSourceType.mic1) && this.SpecialSources[SpecialSourceType.mic1].Connected ? !this.ObsSocket.GetInputMute(this.SpecialSources[SpecialSourceType.mic1].ObsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic2), this.SpecialSources.ContainsKey(SpecialSourceType.mic2) && this.SpecialSources[SpecialSourceType.mic2].Connected ? !this.ObsSocket.GetInputMute(this.SpecialSources[SpecialSourceType.mic2].ObsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic3), this.SpecialSources.ContainsKey(SpecialSourceType.mic3) && this.SpecialSources[SpecialSourceType.mic3].Connected ? !this.ObsSocket.GetInputMute(this.SpecialSources[SpecialSourceType.mic3].ObsSourceName) : false);
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, SpecialSourceType.mic4), this.SpecialSources.ContainsKey(SpecialSourceType.mic4) && this.SpecialSources[SpecialSourceType.mic4].Connected ? !this.ObsSocket.GetInputMute(this.SpecialSources[SpecialSourceType.mic4].ObsSourceName) : false);

            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.desktop1), this.SpecialSources.ContainsKey(SpecialSourceType.desktop1) && this.SpecialSources[SpecialSourceType.desktop1].Connected && this.SpecialSources[SpecialSourceType.desktop1].AudioDevice.IsMuted());
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.desktop2), this.SpecialSources.ContainsKey(SpecialSourceType.desktop2) && this.SpecialSources[SpecialSourceType.desktop2].Connected && this.SpecialSources[SpecialSourceType.desktop2].AudioDevice.IsMuted());
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic1), this.SpecialSources.ContainsKey(SpecialSourceType.mic1) && this.SpecialSources[SpecialSourceType.mic1].Connected && this.SpecialSources[SpecialSourceType.mic1].AudioDevice.IsMuted());
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic2), this.SpecialSources.ContainsKey(SpecialSourceType.mic2) && this.SpecialSources[SpecialSourceType.mic2].Connected && this.SpecialSources[SpecialSourceType.mic2].AudioDevice.IsMuted());
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic3), this.SpecialSources.ContainsKey(SpecialSourceType.mic3) && this.SpecialSources[SpecialSourceType.mic3].Connected && this.SpecialSources[SpecialSourceType.mic3].AudioDevice.IsMuted());
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.windowsmutechanged, SpecialSourceType.mic4), this.SpecialSources.ContainsKey(SpecialSourceType.mic4) && this.SpecialSources[SpecialSourceType.mic4].Connected && this.SpecialSources[SpecialSourceType.mic4].AudioDevice.IsMuted());
#pragma warning restore IDE0075

            /*
             * Toggle LED on/off if stream is in-/active
             */
            bool streaming = this.ObsSocket.GetStreamStatus().IsActive;
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.streamstatuschanged), streaming);
        }

        /*
         * Handles LED-toggeling in case of OBS-Event (WebSocket)
         */
        private void SetupObsEventHandlers()
        {
            this.ObsSocket.StreamStateChanged += (_, args) =>
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

            this.ObsSocket.ReplayBufferStateChanged += (_, args) =>
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

            this.ObsSocket.InputMuteStateChanged += ObsSocketOnInputMuteStateChanged;
            this.ObsSocket.CurrentProgramSceneChanged += ObsSocketOnSceneNameChanged;
        }

        private void ObsSocketOnSceneNameChanged(object? sender, ProgramSceneChangedEventArgs e)
        {
            string currentScene = e.SceneName;
            for (byte soloButtonIndex = 0; soloButtonIndex < this.obsSceneNames.Length && soloButtonIndex < 8; soloButtonIndex++)
            {
                if (currentScene.Equals(this.obsSceneNames[soloButtonIndex]))
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.sceneswitched, soloButtonIndex), true);
                else
                    this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.sceneswitched, soloButtonIndex), false);
            }
        }

        private void ObsSocketOnInputMuteStateChanged(object? sender, InputMuteStateChangedEventArgs e)
        {
            if (!this.SpecialSources.Any(ss => ss.Value.ObsSourceName.Equals(e.InputName)))
                return;

            SpecialSourceType t = this.SpecialSources.First(ss => ss.Value.ObsSourceName.Equals(e.InputName)).Key;
            this.nanoController.ToggleLED(this.bindingConfig.GetOutputForEvent(Config.outputevent.obsmutechanged, t), !e.InputMuted);
        }

        private string GetNanoKontrolInputDeviceName()
        {
            string? name = MidiInformation.GetInputDevices().FirstOrDefault(deviceName => deviceName.ToLower().Contains("nano"));
            if (name is not null)
                return name;
            this.LogError("Unable to find nanoKontrol device!");
            this.LogError("Have you installed the MIDI drivers and ran 'Install KORG USB-MIDI Device'?");
            this.LogInfo($"Found devices:\n{string.Join("\n\t", MidiInformation.GetInputDevices())}");
            Environment.Exit(-1);
            return string.Empty;
        }

        private string GetNanoKontrolOutputDeviceName()
        {
            string? name = MidiInformation.GetOutputDevices().FirstOrDefault(deviceName => deviceName.ToLower().Contains("nano"));
            if (name is not null)
                return name;
            this.LogError("Unable to find nanoKontrol device!");
            this.LogError("Have you installed the MIDI drivers and ran 'Install KORG USB-MIDI Device'?");
            this.LogInfo($"Found devices:\n{string.Join("\n\t", MidiInformation.GetOutputDevices())}");
            Environment.Exit(-1);
            return string.Empty;
        }
        
        /*
         * Custom EventHandler
         */
        public delegate void LogEventHandler(object sender, LogEventArgs e);
        public class LogEventArgs (string text) : EventArgs 
        {
            public readonly string Text = text;
        }

        /*
         * Invokes a Warning-Message
         * Also takes care of formatting
         */
        public void LogWarning(string text)
        {
            this.OnWarningLog.Invoke(this, new LogEventArgs(text));
        }
        
        /*
         * Invokes a Info-Message
         * Also takes care of formatting
         */
        public void LogError(string text)
        {
            this.OnErrorLog.Invoke(this, new LogEventArgs(text));
        }

        /*
         * Invokes a Info-Message
         * Also takes care of formatting
         */
        public void LogInfo(string text)
        {
            this.OnInfoLog.Invoke(this, new LogEventArgs(text));
        }

        /*
         * Invokes a Status-Update
         * Also takes care of formatting
         */
        public void UpdateLogStatus(string text)
        {
            this.OnStatusLog.Invoke(this, new LogEventArgs(text));
        }
    }
}
