namespace SoundControl;

using System;
using System.Runtime.InteropServices;

public class AlsaAudio : AudioDevice
{

    private VolumeControl control;

    public AlsaAudio(string cardName)
    {
        this.control = new VolumeControl(cardName);
        this.Muted = false;
    }
    
    protected override void SetVolumeInternal()
    {
        if(this.Muted)
            control.SetVolumePercent(0);
        else
            control.SetVolumePercent((int)(Volume*100));
    }

    protected override void SetMuteInternal()
    {
        control.SetVolumePercent(0);
    }

    public override bool IsMuted()
    {
        return false;
    }

    public override void Dispose()
    {
        control.Dispose();
    }

    public override event MuteStateChangedEventHandler? OnMuteStateChanged;
    
    private sealed class VolumeControl : IDisposable
    {
        private readonly long _min;
        private readonly long _max;
        private readonly IntPtr _sid;
        private readonly IntPtr _selem;
        private readonly IntPtr _handle;
        private const string LibraryName = "libasound";

        public VolumeControl(string card = "default", string selemName = "PCM")
        {
            snd_mixer_open(ref _handle, 0);
            snd_mixer_attach(_handle, card);
            snd_mixer_selem_register(_handle, default, default);
            snd_mixer_load(_handle);

            snd_mixer_selem_id_malloc(ref _sid);
            snd_mixer_selem_id_set_index(_sid, 0);
            snd_mixer_selem_id_set_name(_sid, selemName);
            _selem = snd_mixer_find_selem(_handle, _sid);

            snd_mixer_selem_get_playback_volume_range(_selem, ref _min, ref _max);
        }

        public void SetVolumePercent(int volume)
        {
            snd_mixer_selem_set_playback_volume_all(_selem, (int)(volume * _max / 100));
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private void ReleaseUnmanagedResources()
        {
            snd_mixer_selem_id_free(_sid);
            snd_mixer_close(_handle);
        }

        ~VolumeControl()
        {
            ReleaseUnmanagedResources();
        }

        [DllImport(LibraryName)]
        internal static extern int snd_mixer_open(ref IntPtr mixer, int mode);

        [DllImport(LibraryName, CharSet = CharSet.Ansi)]
        internal static extern int snd_mixer_attach(IntPtr mixer, [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibraryName)]
        internal static extern int snd_mixer_selem_register(IntPtr mixer, IntPtr options, IntPtr classp);

        [DllImport(LibraryName)]
        internal static extern int snd_mixer_load(IntPtr mixer);

        [DllImport(LibraryName)]
        internal static extern int snd_mixer_selem_id_malloc(ref IntPtr selem);

        [DllImport(LibraryName)]
        internal static extern void snd_mixer_selem_id_set_index(IntPtr selem, uint val);

        [DllImport(LibraryName, CharSet = CharSet.Ansi)]
        internal static extern void snd_mixer_selem_id_set_name(IntPtr selem, [MarshalAs(UnmanagedType.LPStr)] string value);

        [DllImport(LibraryName)]
        internal static extern IntPtr snd_mixer_find_selem(IntPtr mixer, IntPtr selem);

        [DllImport(LibraryName)]
        internal static extern int snd_mixer_selem_get_playback_volume_range(IntPtr selem, ref long min, ref long max);

        [DllImport(LibraryName)]
        internal static extern int snd_mixer_selem_set_playback_volume_all(IntPtr selem, int value);

        [DllImport(LibraryName)]
        internal static extern void snd_mixer_selem_id_free(IntPtr selem);

        [DllImport(LibraryName)]
        internal static extern int snd_mixer_close(IntPtr mixer);
    }
}