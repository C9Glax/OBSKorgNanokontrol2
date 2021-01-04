/*
 * Buffer to prevent overloading OBSWebsocket and WindowsAudio
 */
using System;
using System.Collections.Generic;
using System.Threading;

namespace nanoKontrol2OBS
{
    partial class Kontrol2OBS
    {
        internal class EventClock
        {
            private Dictionary<string, double> obsVolume;
            private Queue<Action> obsBuffer = new Queue<Action>();
            private List<string> volumeChanged;
            private bool stop = false;
            public EventClock(Kontrol2OBS parent, int tickRate)
            {
                this.obsVolume = new Dictionary<string, double>();
                this.volumeChanged = new List<string>();
                foreach (SpecialSourceObject source in parent.specialSources.Values)
                    if (!this.obsVolume.ContainsKey(source.obsSourceName))
                    {
                        this.obsVolume.Add(source.obsSourceName, 0);
                    }

                Thread t = new Thread(() =>
                {
                    while (!stop)
                    {
                        if (this.obsBuffer.Count > 0)
                            this.obsBuffer.Dequeue().Invoke();

                        foreach (SpecialSourceObject specialSource in parent.specialSources.Values)
                            if(specialSource.windowsDevice != null)
                                specialSource.windowsDevice.UpdateStatus();

                        foreach(string source in this.volumeChanged)
                            this.AddOBSEvent(() => { parent.obsSocket.SetVolume(source, this.obsVolume[source]); });
                        this.volumeChanged.Clear();

                        Thread.Sleep(1000 / tickRate);
                    }
                });
                t.Start();
            }

            public void AddOBSEvent(Action func)
            {
                this.obsBuffer.Enqueue(func);
            }

            public void SetOBSVolume(string source, double volume)
            {
                this.obsVolume[source] = volume;
                if(!this.volumeChanged.Contains(source))
                    this.volumeChanged.Add(source);
            }

            public void Dispose()
            {
                this.stop = true;
            }
        }
    }
}
