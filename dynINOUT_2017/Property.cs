using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gem = Autodesk.AutoCAD.Geometry;

namespace dynIN_dynOUT
{

    enum DwgDataType
    {
        kDwgNull = 0,
        kDwgReal = 1,
        kDwgInt32 = 2,
        kDwgInt16 = 3,
        kDwgInt8 = 4,
        kDwgText = 5,
        kDwgBChunk = 6,
        kDwgHandle = 7,
        kDwgHardOwnershipId = 8,
        kDwgSoftOwnershipId = 9,
        kDwgHardPointerId = 10,
        kDwgSoftPointerId = 11,
        kDwg3Real = 12,
        kDwgInt64 = 13,
        kDwgNotRecognized = 19
    };

    class Property
    {
        public long Handle { get; set; }
        public Dictionary<string, string> Attribut { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> DynProp { get; set; } = new Dictionary<string, object>();

        public string Layer { get; set; }
        public int ColorIndex { get; set; }
        public Gem.Scale3d Scale { get; set; }
        public Gem.Point3d Position { get; set; }
        public double Rotation { get; set; }
    }

    public static class StringExtensions
    {
        public static bool ToBoolean(this string value)
        {
            switch (value.ToLower())
            {
                case "true":
                    return true;
                case "t":
                    return true;
                case "1":
                    return true;
                case "0":
                    return false;
                case "false":
                    return false;
                case "f":
                    return false;
                default:
                    throw new InvalidCastException("You can't cast a weird value to a bool!");
            }
        }
    }
}
