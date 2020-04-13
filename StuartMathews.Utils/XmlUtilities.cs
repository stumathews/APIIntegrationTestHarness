using System.Xml.Serialization;

namespace Utils
{
    public static class XmlUtilities <T>
    {
        public static string ObjectToXml(T data, SimpleFileLogger logger = null)
        {
            var result = string.Empty;
            try
            {
                using(var stringWriter = new System.IO.StringWriter())
                { 
                    var serializer = new XmlSerializer(data.GetType());
                    serializer.Serialize(stringWriter, data);
                    result = stringWriter.ToString();
                }
            } 
            catch(System.InvalidOperationException)
            {
                result = string.Empty;
            }
            logger?.Log(result);
            return result;
        }

        public static T XmlToObject(string xmlString)
        {
            using(var stringReader = new System.IO.StringReader(xmlString))
            {
                var serializer = new XmlSerializer(typeof(T));
                return (T) serializer.Deserialize(stringReader);
            }
            
        }
    }
}