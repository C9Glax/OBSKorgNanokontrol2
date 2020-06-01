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

        public Dictionary<int, Operation> inputbindings;

        public enum action { setobsvolume, setwindowsvolume, obsmute, switchscene, windowsmute, previoustrack, nexttrack, playpause, startstopstream, savereplay}
        public struct Operation
        {
            public action action;
            public SpecialSourceType source;
            public int index;
            public Operation(action action, SpecialSourceType source)
            {
                this.action = action;
                this.source = source;
                this.index = 0;
            }

            public Operation(action action, int index)
            {
                this.action = action;
                this.source = SpecialSourceType.desktop1;
                this.index = index;
            }

            public Operation(action action)
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
            this.inputbindings = new Dictionary<int, Operation>();
            XmlDocument config = this.LoadAndValidateConfig(path);
            this.ImportConfig(config);
        }

        private void ImportConfig(XmlDocument config)
        {
            XmlNode inputs = config.GetElementsByTagName("inputs")[0];
            foreach(XmlNode input in inputs.ChildNodes)
            {
                string inputName = input.Name;
                string action = input.Attributes.GetNamedItem("action").Value;
                int control = Convert.ToInt32(input.Attributes.GetNamedItem("midicontrolid").Value);
                if (inputName.Equals("slider") || inputName.Equals("dial"))
                {
                    if (action.Contains("setobsvolume"))
                    {
                        SpecialSourceType source = this.GetSpecialSourceTypeFromString(action.Split('(')[1].Split(')')[0]);
                        this.AddBinding(control, new Operation(Config.action.setobsvolume, source));
                    }else if (action.Contains("setwindowsvolume"))
                    {
                        SpecialSourceType source = this.GetSpecialSourceTypeFromString(action.Split('(')[1].Split(')')[0]);
                        this.AddBinding(control, new Operation(Config.action.setwindowsvolume, source));
                    }
                    else
                        this.parent.LogInfo("Action {0} can not be associated with slider!", action);
                }else if (inputName.Equals("button"))
                {
                    if (action.Contains("obsmute"))
                    {
                        SpecialSourceType source = this.GetSpecialSourceTypeFromString(action.Split('(')[1].Split(')')[0]);
                        this.AddBinding(control, new Operation(Config.action.obsmute, source));
                    }
                    else if(action.Contains("switchscene"))
                    {
                        int index = Convert.ToInt32(action.Split('(')[1].Split(')')[0]);
                        this.AddBinding(control, new Operation(Config.action.switchscene, index));
                    }
                    else if (action.Contains("windowsmute"))
                    {
                        this.AddBinding(control, new Operation(Config.action.windowsmute));
                    }
                    else if (action.Contains("previoustrack"))
                    {
                        this.AddBinding(control, new Operation(Config.action.previoustrack));
                    }
                    else if (action.Contains("nexttrack"))
                    {
                        this.AddBinding(control, new Operation(Config.action.nexttrack));
                    }
                    else if (action.Contains("playpause"))
                    {
                        this.AddBinding(control, new Operation(Config.action.playpause));
                    }
                    else if (action.Contains("startstopstream"))
                    {
                        this.AddBinding(control, new Operation(Config.action.startstopstream));
                    }
                    else if (action.Contains("savereplay"))
                    {
                        this.AddBinding(control, new Operation(Config.action.savereplay));
                    }
                    else
                        this.parent.LogInfo("Action {0} can not be associated with button!", action);
                }
            }
        }

        private void AddBinding(int control, Operation operation)
        {
            if (!this.inputbindings.ContainsKey(control))
                this.inputbindings.Add(control, operation);
            else
                this.parent.LogInfo("Control {0} is already bound!", control.ToString());

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
                throw new Exception("Validation failed!", e.Exception);
            };
            try
            {
                XmlReader validationReader = XmlReader.Create(path, settings);
            }
            catch (FileNotFoundException)
            {
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
