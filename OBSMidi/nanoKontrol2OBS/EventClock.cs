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
            private bool dictLocked = false;
            private Queue<Action> obsBuffer = new Queue<Action>();
            private bool stop = false;
            public EventClock(Kontrol2OBS parent, int tickRate)
            {
                this.obsVolume = new Dictionary<string, double>();

                Thread t = new Thread(() =>
                {
                    while (!stop)
                    {
                        if (this.obsBuffer.Count > 0)
                            this.obsBuffer.Dequeue().Invoke();

                        foreach (SpecialSourceObject specialSource in parent.specialSources.Values)
                            if(specialSource.windowsDevice != null)
                                specialSource.windowsDevice.UpdateStatus();

                        this.dictLocked = true;
                        foreach (KeyValuePair<string, double> volumePair in this.obsVolume)
                            this.AddOBSEvent(() => { parent.obsSocket.SetVolume(volumePair.Key, volumePair.Value); });
                        this.obsVolume.Clear();
                        this.dictLocked = false;

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
                while (this.dictLocked)
                    Thread.Sleep(5);

                if (!this.obsVolume.ContainsKey(source))
                    this.obsVolume.Add(source, volume);
                else 
                    this.obsVolume[source] = volume;
            }

            public void Dispose()
            {
                this.stop = true;
            }
        }
    }
}
