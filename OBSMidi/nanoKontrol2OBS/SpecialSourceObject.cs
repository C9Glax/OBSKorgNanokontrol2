using SoundControl;

namespace Linker
{
    partial class Kontrol2OBS
    {
        internal class SpecialSourceObject(string obsSourceName, AudioDevice audioDevice, bool connected)
        {
            public readonly string ObsSourceName = obsSourceName;
            public readonly AudioDevice AudioDevice = audioDevice;
            public readonly bool Connected = connected;
        }
    }
}
