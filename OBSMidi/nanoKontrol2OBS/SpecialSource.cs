using WindowsSoundControl;
using OBSWebsocketSharp;

namespace nanoKontrol2OBS
{
    partial class Kontrol2OBS
    {
        internal class SpecialSource
        {
            public _specialSourceType specialSourceType;
            public string obsSourceName;
            public AudioDevice windowsDevice;
            public SpecialSource(_specialSourceType specialSourceType)
            {
                this.specialSourceType = specialSourceType;
            }
        }
    }
}
