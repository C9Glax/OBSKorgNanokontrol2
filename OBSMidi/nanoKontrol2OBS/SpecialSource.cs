using WindowsSoundControl;
using OBSWebsocketSharp;

namespace nanoKontrol2OBS
{
    partial class Kontrol2OBS
    {
        internal class SpecialSource
        {
            public SpecialSourceType specialSourceType;
            public string obsSourceName;
            public AudioDevice windowsDevice;
            public SpecialSource(SpecialSourceType specialSourceType)
            {
                this.specialSourceType = specialSourceType;
            }
        }
    }
}
