using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace debug
{


    public class Property
    {
        public double Rotation { get; private set; }


        public string Layer { get; set; }
        public int ColorIndex { get; set; }

        public Property()
        {

        }

    }
        class Program
    {
        static void Main(string[] args)
        {
            var prop = new Property();
            PropertyInfo[] propElement0 = prop.GetType().GetProperties(BindingFlags.DeclaredOnly |
  
                        BindingFlags.Public |
                        BindingFlags.Instance
                        );

            PropertyInfo[] propElement1 = prop.GetType().GetProperties(BindingFlags.NonPublic);

           PropertyInfo[] propElement2 = prop.GetType().GetProperties(BindingFlags.Public);
            PropertyInfo[] propElement3 = prop.GetType().GetProperties(BindingFlags.SetProperty);
           PropertyInfo[] propElement4 = prop.GetType().GetProperties(BindingFlags.Static);


        }
    }
}
