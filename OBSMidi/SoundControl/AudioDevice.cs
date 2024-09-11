namespace SoundControl;

public abstract class AudioDevice : IDisposable
{
    protected bool Muted;
    protected double Volume;
    public void ExecuteChanges()
    {
        this.SetMuteInternal();
        this.SetVolumeInternal();
    }
    
    public void SetVolume(double newVolume)
    {
        this.Volume = newVolume;
    }

    public void Mute(bool mute)
    {
        this.Muted = mute;
    }

    public void ToggleMute()
    {
        this.Muted = !this.Muted;
    }

    protected abstract void SetVolumeInternal();
    protected abstract void SetMuteInternal();
    public abstract bool IsMuted();
    public abstract void Dispose();
    public abstract event MuteStateChangedEventHandler? OnMuteStateChanged;
    public delegate void MuteStateChangedEventHandler(object sender, bool muted);
    public static readonly AudioDevice Default = new DefaultAudioDevice();
}

public class DefaultAudioDevice : AudioDevice
{
    protected override void SetVolumeInternal()
    {
        throw new NotImplementedException();
    }

    protected override void SetMuteInternal()
    {
        throw new NotImplementedException();
    }

    public override bool IsMuted()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public override event MuteStateChangedEventHandler? OnMuteStateChanged;
}