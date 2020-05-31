using WindowsSoundControl;
using OBSWebsocketSharp;

namespace nanoKontrol2OBS
{
    partial class Kontrol2OBS
    {
        internal class SpecialSourceObject
        {
            public SpecialSourceType specialSourceType;
            public string obsSourceName;
            public AudioDevice windowsDevice;
            public bool connected;
            public SpecialSourceObject(SpecialSourceType specialSourceType)
            {
                this.specialSourceType = specialSourceType;
            }
        }
    }
}
