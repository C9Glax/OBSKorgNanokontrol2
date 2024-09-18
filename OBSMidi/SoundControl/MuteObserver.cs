using AudioSwitcher.AudioApi;

namespace SoundControl
{

    internal class MuteObserver : IObserver<DeviceMuteChangedArgs>
    {
        private IDisposable? subscribed;
        private readonly WindowsAudio parent;

        public MuteObserver(WindowsAudio parent)
        {
            this.parent = parent;
        }

        public virtual void Subscribe(IObservable<DeviceMuteChangedArgs> provider)
        {
            this.subscribed = provider.Subscribe(this);
        }

        public virtual void Unsubscribe()
        {
            this.subscribed?.Dispose();
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
            this.parent.MuteChanged(value.IsMuted);
        }
    }
}