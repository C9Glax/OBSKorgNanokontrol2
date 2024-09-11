using AudioSwitcher.AudioApi.CoreAudio;

namespace SoundControl
{
    public class WindowsAudio : AudioDevice
    {
        private readonly CoreAudioDevice device;
        private readonly MuteObserver muteObserver;

        /*
         * Creates controls for WindowsAudioDevice with GUID
         * guid: GUID of the WindowsAudioDevice to control
         */
        public WindowsAudio(string guid) : base()
        {
            this.device = new CoreAudioController().GetDevice(Guid.Parse(guid));
            if (this.device == null)
                throw new Exception("Disconnected Audio-device in OBS-Settings. Exiting...");

            this.muteObserver = new MuteObserver(this);
            this.muteObserver.Subscribe(this.device.MuteChanged);

            this.Muted = this.device.IsMuted;
        }

        protected override void SetVolumeInternal()
        {
            this.device.Volume = Volume;
        }

        protected override void SetMuteInternal()
        {
            this.device.Mute(Muted);
        }

        public sealed override bool IsMuted() => this.device.IsMuted;

        public override void Dispose()
        {
            this.muteObserver.Unsubscribe();
        }

        public override event MuteStateChangedEventHandler? OnMuteStateChanged;

        internal void MuteChanged(bool muted)
        {
            this.OnMuteStateChanged?.Invoke(this, muted);
        }
    }
}
