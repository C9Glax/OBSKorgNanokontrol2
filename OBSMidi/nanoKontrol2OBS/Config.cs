using OBSWebsocketSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;

namespace nanoKontrol2OBS
{
    public class Config
    {

        public Dictionary<int, Action> inputbindings;
        public List<Reaction> outputbindings;

        public enum outputevent { obsmutechanged, windowsmutechanged, streamstatuschanged, replaystatuschanged, sceneswitched }

        public struct Reaction
        {
            public byte control;
            public SpecialSourceType source;
            public outputevent cause;
            public int sceneindex;

            public Reaction(outputevent cause, byte control, SpecialSourceType source)
            {
                this.control = control;
                this.cause = cause;
                this.source = source;
                this.sceneindex = 0;
            }

            public Reaction(outputevent cause, byte control)
            {
                this.control = control;
                this.cause = cause;
                this.source = SpecialSourceType.desktop1;
                this.sceneindex = 0;
            }

            public Reaction(outputevent cause, byte control, int sceneindex)
            {
                this.control = control;
                this.cause = cause;
                this.source = SpecialSourceType.desktop1;
                this.sceneindex = sceneindex;
            }
        }

        public enum action { setobsvolume, setwindowsvolume, obsmute, switchscene, windowsmute, previoustrack, nexttrack, playpause, startstopstream, savereplay}
        public struct Action
        {
            public action action;
            public SpecialSourceType source;
            public int index;
            public Action(action action, SpecialSourceType source)
            {
                this.action = action;
                this.source = source;
                this.index = 0;
            }

            public Action(action action, int index)
            {
                this.action = action;
                this.source = SpecialSourceType.desktop1;
                this.index = index;
            }

            public Action(action action)
            {
                this.action = action;
                this.source = SpecialSourceType.desktop1;
                this.index = 0;
            }
        }

        private readonly Kontrol2OBS parent;
        public Config(Kontrol2OBS parent, string path)
        {
            this.parent = parent;
            this.inputbindings = new Dictionary<int, Action>();
            this.outputbindings = new List<Reaction>();
            XmlDocument config = this.LoadAndValidateConfig(path);
            this.ImportConfig(config);
        }

        private void ImportConfig(XmlDocument config)
        {
            XmlNode inputs = config.GetElementsByTagName("inputs")[0];
            foreach(XmlNode input in inputs.ChildNodes)
            {
                string action = input.Attributes.GetNamedItem("action").Value.Split('(')[0];
                string value = input.Attributes.GetNamedItem("action").Value.Split('(')[1].Split(')')[0];
                byte control = Convert.ToByte(input.Attributes.GetNamedItem("midicontrolid").Value);
                if (input.Name.Equals("slider") || input.Name.Equals("dial"))
                {
                    if (action == "setobsvolume")
                    {
                        SpecialSourceType source = this.GetSpecialSourceTypeFromString(value);
                        this.AddBinding(control, new Action(Config.action.setobsvolume, source));
                    }else if (action == "setwindowsvolume")
                    {
                        SpecialSourceType source = this.GetSpecialSourceTypeFromString(value);
                        this.AddBinding(control, new Action(Config.action.setwindowsvolume, source));
                    }
                    else
                        this.parent.LogInfo("Action {0} can not be associated with slider!", action);
                }else if (input.Name.Equals("button"))
                {
                    if (action == "obsmute")
                    {
                        SpecialSourceType source = this.GetSpecialSourceTypeFromString(value);
                        this.AddBinding(control, new Action(Config.action.obsmute, source));
                    }
                    else if(action == "switchscene")
                    {
                        int index = Convert.ToInt32(value);
                        this.AddBinding(control, new Action(Config.action.switchscene, index));
                    }
                    else if (action == "windowsmute")
                    {
                        this.AddBinding(control, new Action(Config.action.windowsmute, this.GetSpecialSourceTypeFromString(value)));
                    }
                    else if (action == "previoustrack")
                    {
                        this.AddBinding(control, new Action(Config.action.previoustrack));
                    }
                    else if (action == "nexttrack")
                    {
                        this.AddBinding(control, new Action(Config.action.nexttrack));
                    }
                    else if (action == "playpause")
                    {
                        this.AddBinding(control, new Action(Config.action.playpause));
                    }
                    else if (action == "startstopstream")
                    {
                        this.AddBinding(control, new Action(Config.action.startstopstream));
                    }
                    else if (action == "savereplay")
                    {
                        this.AddBinding(control, new Action(Config.action.savereplay));
                    }
                    else
                        this.parent.LogInfo("Action {0} can not be associated with button!", action);
                }
            }

            XmlNode outputs = config.GetElementsByTagName("outputs")[0];
            foreach(XmlNode output in outputs.ChildNodes)
            {
                string cause = output.Attributes.GetNamedItem("event").Value;
                byte control = Convert.ToByte(output.Attributes.GetNamedItem("midicontrolid").Value);
                if (cause.Equals("obsmutechanged"))
                {
                    XmlNode source = output.Attributes.GetNamedItem("source");
                    if (source != null)
                        this.outputbindings.Add(new Reaction(outputevent.obsmutechanged, control, this.GetSpecialSourceTypeFromString(source.Value)));
                    else
                        this.parent.LogInfo("Attribute 'source' has to be set for event {0}!", cause);
                }
                else if (cause.Equals("windowsmutechanged"))
                {
                    XmlNode source = output.Attributes.GetNamedItem("source");
                    if (source != null)
                        this.outputbindings.Add(new Reaction(outputevent.windowsmutechanged, control, this.GetSpecialSourceTypeFromString(source.Value)));
                    else
                        this.parent.LogInfo("Attribute 'source' has to be set for event {0}!", cause);
                }
                else if (cause.Equals("streamstatuschanged"))
                {
                    this.outputbindings.Add(new Reaction(outputevent.streamstatuschanged, control));
                }
                else if (cause.Equals("replaystatuschanged"))
                {
                    this.outputbindings.Add(new Reaction(outputevent.replaystatuschanged, control));
                }
                else if (cause.Equals("sceneswitched"))
                {
                    XmlNode sceneindex = output.Attributes.GetNamedItem("sceneindex");
                    if (sceneindex != null)
                        this.outputbindings.Add(new Reaction(outputevent.sceneswitched, control, Convert.ToInt32(sceneindex.Value)));
                    else
                        this.parent.LogInfo("Attribute 'sceneindex' has to be set for event {0}!", cause);
                }
                else
                    this.parent.LogInfo("Event {0} does not exist!", cause);
            }
        }

        private void AddBinding(byte control, Action operation)
        {
            if (!this.inputbindings.ContainsKey(control))
                this.inputbindings.Add(control, operation);
            else
                this.parent.LogInfo("Control {0} is already bound!", control.ToString());

        }

        public byte GetOutputToEvent(outputevent cause, SpecialSourceType source)
        {
            foreach(Reaction output in this.outputbindings)
                if (cause == output.cause && source == output.source)
                    return output.control;
            return 0;
        }
        public byte GetOutputToEvent(outputevent cause)
        {
            foreach (Reaction output in this.outputbindings)
                if (cause == output.cause)
                    return output.control;
            return 0;
        }

        public byte GetOutputToEvent(outputevent cause, int sceneindex)
        {
            foreach (Reaction output in this.outputbindings)
                if (cause == output.cause && sceneindex == output.sceneindex)
                    return output.control;
            return 0;

        }

        private SpecialSourceType GetSpecialSourceTypeFromString(string source)
        {
            switch (source)
            {
                case "desktop1":
                    return SpecialSourceType.desktop1;
                case "desktop2":
                    return SpecialSourceType.desktop2;
                case "mic1":
                    return SpecialSourceType.mic1;
                case "mic2":
                    return SpecialSourceType.mic2;
                case "mic3":
                    return SpecialSourceType.mic3;
                default:
                    this.parent.LogInfo("Source {0} is not a valid source.", source);
                    return SpecialSourceType.desktop1;
            }
        }

        private XmlDocument LoadAndValidateConfig(string path)
        {
            XmlSchemaSet schemaset = new XmlSchemaSet();
            XmlReader xmlreader = XmlReader.Create(@".\config.xsd");
            schemaset.Add("http://www.w3.org/2001/XMLSchema", xmlreader);

            XmlReaderSettings settings = new XmlReaderSettings()
            {
                ValidationType = ValidationType.Schema,
                ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
            };
            settings.ValidationEventHandler += (s, e) =>
            {
                this.parent.LogWarning("Config has wrong format!");
                throw new Exception("Validation failed!", e.Exception);
            };
            try
            {
                XmlReader validationReader = XmlReader.Create(path, settings);
            }
            catch (FileNotFoundException)
            {
                this.parent.LogWarning("Configfile not found at {0}!", path);
                this.parent.LogWarning("Configfile not found. ({0})", path);
            }

            XmlDocument xmlconfig = new XmlDocument
            {
                PreserveWhitespace = false
            };
            xmlconfig.Load(path);
            return xmlconfig;
        }
    }
}
