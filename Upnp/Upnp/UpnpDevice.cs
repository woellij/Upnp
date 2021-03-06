﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using Upnp.Collections;
using Upnp.Xml;

namespace Upnp.Upnp
{
    public class UpnpDevice : IXmlSerializable
    {
        private UpnpDevice _parent;
        private UpnpRoot _root;

        public UpnpDevice()
        {
            this.Properties = new Dictionary<string, string>();
            this.IsEnabled = true;

            this.Devices = new CustomActionCollection<UpnpDevice>(
                device =>
                {
                    device.Parent = this;
                    device.Root = this.Root;
                },
                device =>
                {
                    device.Parent = null;
                    device.Root = null;
                });
            this.Services = new CustomActionCollection<UpnpService>(service => service.Device = this, service => service.Device = null);
            this.Icons = new CustomActionCollection<UpnpIcon>(icon => icon.Device = this, icon => icon.Device = null);
        }

        #region Object Overrides 

        public override string ToString()
        {
            return string.Format("{0}/{1}", this.Type, this.UDN);
        }
  
        #endregion

        #region IXmlSerializable Implementation

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }
  
        public void ReadXml(XmlReader reader)
        {
            if (reader.LocalName != "device" && !reader.ReadToDescendant("device"))
                throw new InvalidDataException();
            
            // TODO: Serialize/Deserialize required Upnp properties separate from the default property
            var dict = new Dictionary<string, Action>()
            {
                {XmlHelper.DefaultParseElementName, () => 
                    {
                        if (!this.Properties.ContainsKey(reader.LocalName))
                            this.Properties.Add(reader.LocalName, reader.ReadString());
                    }
                },
                {"deviceList", () => XmlHelper.ParseXmlCollection(reader, this.Devices, "device", () => new UpnpDevice())},
                {"serviceList", () => XmlHelper.ParseXmlCollection(reader, this.Services, "service", () => new UpnpService())},
                {"iconList", () => XmlHelper.ParseXmlCollection(reader, this.Icons, "icon", () => new UpnpIcon())}
            };

            XmlHelper.ParseXml(reader, dict);
        }
  
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("device");

            if (!this.IsEnabled)
                writer.WriteAttributeString("enabled", "false");

            foreach (var pair in this.Properties)
                writer.WriteElementString(pair.Key, pair.Value);

            if (this.Devices.Count > 0)
            {
                writer.WriteStartElement("deviceList");
                foreach (var device in this.Devices)
                    device.WriteXml(writer);
                writer.WriteEndElement();
            }

            if (this.Services.Count > 0)
            {
                writer.WriteStartElement("serviceList");
                foreach (var service in this.Services)
                    service.WriteXml(writer);
                writer.WriteEndElement();
            }

            if (this.Icons.Count > 0)
            {
                writer.WriteStartElement("iconList");
                foreach (var icon in this.Icons)
                    icon.WriteXml(writer);
                writer.WriteEndElement();
            }

            writer.WriteEndElement(); //device
        }
  
        #endregion

        #region Events

        public event EventHandler<EventArgs<UpnpDevice>> Removed;
        public event EventHandler<EventArgs<UpnpDevice>> Added;

        protected void OnAdded()
        {
            var handler = Added;
            if (handler != null) 
                handler(this, new EventArgs<UpnpDevice>(this));
        }

        protected void OnRemoved()
        {
            var handler = this.Removed;
            if (handler != null)
                handler(this, new EventArgs<UpnpDevice>(this));
        }

        #endregion

        #region Properties

        public UpnpRoot Root
        {
            get { return _root; }
            protected internal set
            {
                if (_root == value)
                    return; 
                
                if(_root != null)
                    _root.OnChildDeviceRemoved(this);

                _root = value;

                if(_root != null)
                    _root.OnChildDeviceAdded(this);
            }
        }

        public UpnpDevice Parent
        {
            get { return _parent; }
            protected set
            {
                if(_parent == value)
                    return;

                _parent = value;

                if(_parent == null)
                {
                    OnRemoved();
                }
                else
                {
                    OnAdded();
                }
            }
        }

        public UpnpDevice RootDevice
        {
            get
            {
                return this.Root.RootDevice;
            }
        }
  
        public bool IsEnabled { get;set; }
  
        public ICollection<UpnpDevice> Devices { get;private set; }
  
        public ICollection<UpnpService> Services { get;private set; }
  
        public ICollection<UpnpIcon> Icons { get;private set; }
  
        public UpnpType Type
        {
            get { return UpnpType.Parse(this.Properties["deviceType"]); }
            set { this.Properties["deviceType"] = value.ToString(); }
        }
  
        public UniqueDeviceName UDN
        {
            get { return UniqueDeviceName.Parse(this.Properties["UDN"]); }
            set
            {
                this.Properties["UDN"] = value.ToString();
            }
        }
  
        public Dictionary<string, string> Properties { get;private set; }
  
        public string FriendlyName
        {
            get { return this.GetProperty("friendlyName"); }
            set { this.Properties["friendlyName"] = value; }
        }
  
        public string Manufacturer
        {
            get { return this.GetProperty("manufacturer"); }
            set { this.Properties["manufacturer"] = value; }
        }
  
        public string ManufacturerUrl
        {
            get { return this.GetProperty("manufacturerURL"); }
            set { this.Properties["manufacturerURL"] = value; }
        }
  
        public string ModelDescription
        {
            get { return this.GetProperty("modelDescription"); }
            set { this.Properties["modelDescription"] = value; }
        }
  
        public string ModelName
        {
            get { return this.GetProperty("modelName"); }
            set { this.Properties["modelName"] = value; }
        }
  
        public string ModelNumber
        {
            get { return this.GetProperty("modelNumber"); }
            set { this.Properties["modelNumber"] = value; }
        }
  
        public string ModelUrl
        {
            get { return this.GetProperty("modelURL"); }
            set { this.Properties["modelURL"] = value; }
        }
  
        public string SerialNumber
        {
            get { return this.GetProperty("serialNumber"); }
            set { this.Properties["serialNumber"] = value; }
        }
  
        public string UPC
        {
            get { return this.GetProperty("UPC"); }
            set { this.Properties["UPC"] = value; }
        }
  
        private string GetProperty(string property)
        {
            if (this.Properties.ContainsKey(property))
                return this.Properties[property];

            return string.Empty;
        }
  
        #endregion

        public IEnumerable<UpnpDevice> FindByDeviceType(UpnpType type)
        {
            return this.EnumerateDevices().Where(d => d.Type.Equals(type));
        }

        public IEnumerable<UpnpDevice> EnumerateDevices()
        {
            return new[] { this }.Concat(this.Devices.SelectMany(d => d.EnumerateDevices()));
        }

        public IEnumerable<UpnpService> EnumerateServices()
        {
            return this.EnumerateDevices().SelectMany(device => device.Services);
        }
    }
}
