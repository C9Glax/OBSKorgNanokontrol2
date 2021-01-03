using System;
using System.Collections.Generic;
using System.Threading;

namespace nanoKontrol2OBS
{
    partial class Kontrol2OBS
    {
        internal class EventClock
        {
            private Stack<Delegate> OBSBuffer = new Stack<Delegate>();
            private Stack<object[]> OBSBufferArguments = new Stack<object[]>();
            public EventClock(Kontrol2OBS parent, int tickRate)
            {
                Thread t = new Thread(() =>
                {
                    while (true)
                    {
                        if (this.OBSBuffer.Count > 0)
                            if (this.OBSBufferArguments.Peek().Length == 0)
                            {
                                this.OBSBuffer.Pop().DynamicInvoke();
                                this.OBSBufferArguments.Pop();
                            } else if (this.OBSBufferArguments.Peek().Length == 1)
                                    this.OBSBuffer.Pop().DynamicInvoke(OBSBufferArguments.Pop()[0]);
                            else if(this.OBSBufferArguments.Peek().Length == 2)
                                    this.OBSBuffer.Pop().DynamicInvoke(OBSBufferArguments.Peek()[0], OBSBufferArguments.Pop()[1]);

                        foreach (SpecialSourceObject specialSource in parent.specialSources.Values)
                            if(specialSource.windowsDevice != null)
                                specialSource.windowsDevice.UpdateStatus();
                        Thread.Sleep(tickRate);
                    }
                });
                t.Start();
            }

            public void AddOBSEvent(Action func)
            {
                this.OBSBuffer.Push(func);
                this.OBSBufferArguments.Push(new object[0]);
            }

            public void AddOBSEvent(Action<string> func, string parameter)
            {
                this.OBSBuffer.Push(func);
                this.OBSBufferArguments.Push(new object[]{ parameter });
            }

            public void AddOBSEvent(Action<string, double> func, string parameter, double parameter2)
            {
                this.OBSBuffer.Push(func);
                this.OBSBufferArguments.Push(new object[] { parameter, parameter2 });
            }
        }
    }
}
