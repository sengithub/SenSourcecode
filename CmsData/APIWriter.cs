using System;
using System.Linq;
using System.Xml;
using UtilityExtensions;
using System.Text;
using System.IO;

namespace CmsData.API
{
    public class APIWriter
    {
        private XmlWriter w;
        private StringBuilder sb;
        public APIWriter()
        {
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = new System.Text.UTF8Encoding(false);
            sb = new StringBuilder();
            w = XmlWriter.Create(sb,settings);
        }
        public APIWriter Start(string element)
        {
            w.WriteStartElement(element);
            return this;
        }
        public APIWriter End()
        {
            w.WriteEndElement();
            return this;
        }
        public APIWriter Attr(string attr, object i)
        {
            var s = tostr(i);
            if (s.HasValue())
                w.WriteAttributeString(attr, s);
            return this;
        }
        private string tostr(object i)
        {
            string s = null;
            if (i is DateTime)
                s = ((DateTime)i).ToString("M/d/yy h:mm tt");
            else if (i is DateTime?)
                s = ((DateTime?)i).ToString2("M/d/yy h:mm tt");
            else if (i == null)
                s = string.Empty;
            else
                s = i.ToString();
            return s;
        }
        public APIWriter Add(string element, object i)
        {
            var s = tostr(i);
            if (s.HasValue())
                w.WriteElementString(element, s);
            return this;
        }
        public APIWriter AddText(string text)
        {
            w.WriteRaw(text);
            return this;
        }
        public override string ToString()
        {
            w.Flush();
            return sb.ToString();
        }
        ~APIWriter()
        {
            w.Close();
        }
    }
}