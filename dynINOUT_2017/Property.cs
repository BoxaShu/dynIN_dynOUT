using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dynIN_dynOUT
{
    class Property
    {
        public long Handle { get; set; }
        public Dictionary<string, string> Attribut { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> DynProp { get; set; } = new Dictionary<string, object>();
    }
}
