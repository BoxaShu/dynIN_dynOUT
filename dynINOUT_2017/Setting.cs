//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Xml.Serialization;

//namespace dynIN_dynOUT
//{
//    public static class Setting
//    {
//        /// <summary>
//        ///Создавать или нет слой, если его нет в базе чертежа
//        /// </summary>
//        public static bool CreateLayer { get; set; } = true;

//        /// <summary>
//        /// ///Создавать или нет блок, если вместо хендла написано имя блока
//        /// </summary>
//        public static bool CreateBlocReference { get; set; } = true;


//    }
//}



using System;
using System.Collections.Generic;
using App = Autodesk.AutoCAD.ApplicationServices;
using System.IO;
using System.Xml.Serialization;

namespace dynIN_dynOUT
{

    //тут нужно сделать синглтон!!!
    public class Settings
    {
        private static Sets _settings;

        public static Sets Data
        {
            //get { return _settings; }
            //set { _settings = value; }
            get
            {
                _settings = getParam();
                return _settings;
            }
            set
            {
                _settings = value;
                saveParam(_settings);
            }
        }

        private static Sets getParam()
        {
            // Set a variable to the My Documents path.
            string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string path = mydocpath + Convert.ToString($"\\dynINOUTSetting.xml");

            Sets myObject;
            if (File.Exists(path))
            {
                try
                {
                    XmlSerializer mySerializer = new XmlSerializer(typeof(Sets));
                    using (FileStream myFileStream = new FileStream(path, FileMode.Open))
                    {
                        myObject = (Sets)mySerializer.Deserialize(myFileStream);
                    }
                }
                catch (System.InvalidOperationException ex)
                {
                    File.Delete(path);//, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin);
                    myObject = new Sets(true, true, mydocpath);
                    saveParam(myObject);
                }
            }
            else
            {
                myObject = new Sets(true, true, mydocpath);
                saveParam(myObject);
            }
            return myObject;
        }

        private static void saveParam(Sets Setting)
        {
            try
            {
                string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string path = mydocpath + Convert.ToString($"\\dynINOUTSetting.xml");

                XmlSerializer ser = new XmlSerializer(typeof(Sets));
                using (TextWriter writer = new StreamWriter(path, false))
                {
                    ser.Serialize(writer, Setting);
                }
            }
            catch (System.IO.FileNotFoundException ex)
            {
                App.Application.DocumentManager.MdiActiveDocument.Editor.
WriteMessage($"\nError: Settings-saveParam:{ex.Message}");

            }
        }
    }

    public class Sets
    {
        /// <summary>
        ///Создавать или нет слой, если его нет в базе чертежа
        /// </summary>
        public  bool CreateLayer { get; set; }

        /// <summary>
        /// ///Создавать или нет блок, если вместо хендла написано имя блока
        /// </summary>
        public  bool CreateBlocReference { get; set; }

        public string Lastpath { get; set; }

        public Sets() { }

        public Sets(
            bool createLayer,
            bool createBlocReference,
            string lastPath)
        {
            this.CreateLayer = createLayer;
            this.CreateBlocReference = createBlocReference;
            this.Lastpath = lastPath;
        }
    }
}