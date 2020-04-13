using System;
using System.Collections.Generic;
using System.Linq;

namespace Second.Utils
{
    public static class Extensions
    {
        public static double? ObjectToDouble(this object theObject)
        {
            var asDouble = theObject as double?;
            var asString = theObject as string;

            if(asDouble != null)
                return asDouble;
            if(string.IsNullOrEmpty(asString))
                return null;
            else 
            {
                if(double.TryParse(asString, out var found))
                    return found;
                return null;
            }
        }

        public static int? ObjectToNullableInt(this object theObject)
        {
            var asString = theObject as string;
            var asNullableInt = theObject as int?;

            if(asNullableInt != null)
                return asNullableInt;
            if(string.IsNullOrEmpty(asString))
                return null;
            else
                return int.Parse(asString);     
        }

        public static string ObjectToString(this object theObject)
        {
            if(theObject is string asString && !string.IsNullOrEmpty(asString))
                return asString;
            return string.Empty;
        }

        public static int? ObjectToInteger(this object theObject)
        {
            var asInteger = theObject as int?;
            var asString = theObject as string;

            if(asInteger != null)
                return asInteger;
            if(string.IsNullOrEmpty(asString))
                return null;
            
            if(int.TryParse(asString, out var found)) return found;            
            return null;
        }

        public static bool? ObjectToBool(this object theObject)
        {
            var asBool = theObject as bool?;
            var asString = theObject as string;
            if(asBool != null)
                return asBool;

            if(!string.IsNullOrEmpty(asString))
            {
                if(bool.TryParse(asString, out var found)) return found;
                return null;
            }
            return null;
        }

        public static DateTime? ObjectToDateTime(this object theObject)
        {
            var asDateTime = theObject as DateTime?;
            var asString = theObject as string;
            if(asDateTime != null)
                return asDateTime;

            if (string.IsNullOrEmpty(asString)) return null;
            if(DateTime.TryParse(asString, out var parsed)) 
                return parsed;                
            return null;

        }
    }
}