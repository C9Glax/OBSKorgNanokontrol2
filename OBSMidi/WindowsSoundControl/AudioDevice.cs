/*
 * Class to control Windows-AudioDevices
 */

using System;
using System.Collections.Generic;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

namespace WindowsSoundControl
{
    public class AudioDevice
    {
        private readonly CoreAudioDevice device;
        private readonly MuteObserver muteObserver;

        private Stack<double> volumeBuffer = new Stack<double>();
        private bool mute;

        /*
         * Creates controls for WindowsAudioDevice with GUID
         * guid: GUID of the WindowsAudioDevice to control
         */
        public AudioDevice(string guid)
        {
            this.device = new CoreAudioController().GetDevice(Guid.Parse(guid));
            if (this.device == null)
                throw new Exception("Disconnected Audio-device in OBS-Settings. Exiting...");

            this.muteObserver = new MuteObserver(this);
            this.muteObserver.Subscribe(this.device.MuteChanged);

            this.mute = this.IsMuted();
        }

        public void ToggleMute()
        {
            this.mute = !this.mute;
        }

        public bool IsMuted()
        {
            return this.device.IsMuted;
        }

        public void SetVolume(double volume)
        {
            this.volumeBuffer.Push(volume);
        }

        public void ExecuteChanges()
        {
            this.device.Mute(mute);
            while (this.volumeBuffer.Count > 1)
                this.volumeBuffer.Pop();
            if(this.volumeBuffer.Count > 0)
                this.device.Volume = this.volumeBuffer.Pop();
        }

        public void Dispose()
        {
            this.muteObserver.Unsubscribe();
        }

        public event MuteStateChangedEventHandler OnMuteStateChanged;
        public class OnMuteStateChangedEventArgs : EventArgs
        {
            public bool muted;
        }
        public delegate void MuteStateChangedEventHandler(object sender, OnMuteStateChangedEventArgs e);

        internal class MuteObserver : IObserver<DeviceMuteChangedArgs>
        {
            private IDisposable subscribed;
            private readonly AudioDevice parent;

            public MuteObserver(AudioDevice parent)
            {
                this.parent = parent;
            }

            public virtual void Subscribe(IObservable<DeviceMuteChangedArgs> provider)
            {
                this.subscribed = provider.Subscribe(this);
            }

            public virtual void Unsubscribe()
            {
                this.subscribed.Dispose();
            }

            public void OnCompleted()
            {
                this.Unsubscribe();
            }

            public void OnError(Exception error)
            {
                throw error;
            }

            public void OnNext(DeviceMuteChangedArgs value)
            {
                this.parent.OnMuteStateChanged?.Invoke(this.parent, new OnMuteStateChangedEventArgs()
                {
                    muted = value.IsMuted
                });
            }
        }
    }
}
