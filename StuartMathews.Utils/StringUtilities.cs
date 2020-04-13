using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace Utils
{
    public static class StringUtilities
    {
        public static string RemoveXmlNamespaces(this string xmlString)
        {
            var doc = XDocument.Parse(xmlString);

            var namespaces = from a in doc.Descendants().Attributes()
                where a.IsNamespaceDeclaration && a.Name != "xsi"
                select a;
            namespaces.Remove();
            RemoveNamespacePrefix(doc.Root);
            return doc.ToString();

            void RemoveNamespacePrefix ( XElement element )
            {
                //Remove from element
                if (element.Name.Namespace != null)
                    element.Name = element.Name.LocalName;

                //Remove from attributes
                var attributes = element.Attributes().ToArray();
                element.RemoveAttributes();
                foreach (var attr in attributes)
                {
                    var newAttr = attr;

                    if (attr.Name.Namespace != null)
                        newAttr = new XAttribute(attr.Name.LocalName, attr.Value);

                    element.Add(newAttr);                    
                };

                //Remove from children
                foreach (var child in element.Descendants())
                    RemoveNamespacePrefix(child);
            }
        }

        public static string DumpInterface<T>(this T obj, SimpleFileLogger logger = null)
        {
            var sb = new StringBuilder();
            var t = obj.GetType();
            var props = t.GetProperties();
            foreach (var prop in props)
            {
                try
                {
                    sb.AppendLine(prop.GetIndexParameters().Length == 0
                        ? $"{prop.Name}={prop.GetValue(obj)}"
                        : $" {prop.Name}, {prop.PropertyType.Name} <Indexed>");
                }
                catch (Exception /*dont care*/)
                {
                    // ignored
                }
            }
            logger?.Log(sb.ToString());
            return sb.ToString();
        }

        public static string PostMirroredXml(string serverUrl, string requestXml)
        {
            var request = (HttpWebRequest)WebRequest.Create(serverUrl);
            var bytes = System.Text.Encoding.ASCII.GetBytes(requestXml);
            request.ContentType = "text/xml; encoding='utf-8'";
            request.ContentLength = bytes.Length;
            request.Method = "POST";
            var stream = request.GetRequestStream();
            stream.Write(bytes, 0, bytes.Length);
            stream.Close();
            var response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode != HttpStatusCode.OK) return null;
            var responseStream = response.GetResponseStream();
            return new StreamReader(responseStream ?? throw new InvalidOperationException()).ReadToEnd();
        }

    }
}