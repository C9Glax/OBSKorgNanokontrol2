/*
 * Buffer to prevent overloading OBSWebsocket and WindowsAudio
 */

using System.Collections.Concurrent;

namespace Linker
{
    partial class Kontrol2OBS
    {
        internal class EventClock
        {
            private ConcurrentDictionary<string, float> obsVolume;
            private Queue<Action> obsBuffer = new Queue<Action>();
            private bool stop = false;
            public EventClock(Kontrol2OBS parent, int tickRate)
            {
                this.obsVolume = new ConcurrentDictionary<string, float>();

                Thread t = new Thread(() =>
                {
                    while (!stop)
                    {
                        if (this.obsBuffer.Count > 0)
                            this.obsBuffer.Dequeue().Invoke();

                        foreach (SpecialSourceObject specialSource in parent.specialSources.Values)
                            specialSource.AudioDevice.ExecuteChanges();

                        foreach (KeyValuePair<string, float> volumePair in this.obsVolume)
                            this.AddOBSEvent(() => { parent.obsSocket.SetInputVolume(volumePair.Key, volumePair.Value); });
                        this.obsVolume.Clear();

                        Thread.Sleep(1000 / tickRate);
                    }
                });
                t.Start();
            }

            public void AddOBSEvent(Action func)
            {
                this.obsBuffer.Enqueue(func);
            }

            public void SetOBSVolume(string source, float volume)
            {
                if (!this.obsVolume.ContainsKey(source))
                    while (!this.obsVolume.TryAdd(source, volume)) ;
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
