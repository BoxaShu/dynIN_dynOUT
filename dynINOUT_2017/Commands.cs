﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using App = Autodesk.AutoCAD.ApplicationServices;
using cad = Autodesk.AutoCAD.ApplicationServices.Application;
using Db = Autodesk.AutoCAD.DatabaseServices;
using Ed = Autodesk.AutoCAD.EditorInput;
using Gem = Autodesk.AutoCAD.Geometry;
using Rtm = Autodesk.AutoCAD.Runtime;

[assembly: Rtm.CommandClass(typeof(dynIN_dynOUT.Commands))]

namespace dynIN_dynOUT
{
    public class Commands : Rtm.IExtensionApplication
    {

        /// <summary>
        /// Загрузка библиотеки
        /// http://through-the-interface.typepad.com/through_the_interface/2007/03/getting_the_lis.html
        /// </summary>
        #region 
        public void Initialize()
        {
            String assemblyFileFullName = GetType().Assembly.Location;
            String assemblyName = System.IO.Path.GetFileName(
                                                      GetType().Assembly.Location);

            // Just get the commands for this assembly
            App.DocumentCollection dm = App.Application.DocumentManager;
            Assembly asm = Assembly.GetExecutingAssembly();
            Ed.Editor acEd = dm.MdiActiveDocument.Editor;

            // Сообщаю о том, что произведена загрузка сборки 
            //и указываю полное имя файла,
            // дабы было видно, откуда она загружена
            acEd.WriteMessage(string.Format("\n{0} {1} {2}.\n{3}: {4}\n{5}\n",
                      "Assembly", assemblyName, "Loaded",
                      "Assembly File:", assemblyFileFullName,
                       "Copyright © Владимир Шульжицкий, 2018"));


            //Вывожу список комманд определенных в библиотеке
            acEd.WriteMessage("\nStart list of commands: \n\n");

            string[] cmds = GetCommands(asm, false);
            foreach (string cmd in cmds)
                acEd.WriteMessage(cmd + "\n");

            acEd.WriteMessage("\n\nEnd list of commands.\n");
        }

        public void Terminate()
        {
            Console.WriteLine("finish!");
        }

        /// <summary>
        /// Получение списка комманд определенных в сборке
        /// </summary>
        /// <param name="asm"></param>
        /// <param name="markedOnly"></param>
        /// <returns></returns>
        private static string[] GetCommands(Assembly asm, bool markedOnly)
        {
            StringCollection sc = new StringCollection();
            object[] objs =
              asm.GetCustomAttributes(
                typeof(Rtm.CommandClassAttribute),
                true
              );
            Type[] tps;
            int numTypes = objs.Length;
            if (numTypes > 0)
            {
                tps = new Type[numTypes];
                for (int i = 0; i < numTypes; i++)
                {
                    Rtm.CommandClassAttribute cca =
                      objs[i] as Rtm.CommandClassAttribute;
                    if (cca != null)
                    {
                        tps[i] = cca.Type;
                    }
                }
            }
            else
            {
                // If we're only looking for specifically
                // marked CommandClasses, then use an
                // empty list
                if (markedOnly)
                    tps = new Type[0];
                else
                    tps = asm.GetExportedTypes();
            }
            foreach (Type tp in tps)
            {
                MethodInfo[] meths = tp.GetMethods();
                foreach (MethodInfo meth in meths)
                {
                    objs =
                      meth.GetCustomAttributes(
                        typeof(Rtm.CommandMethodAttribute),
                        true
                      );
                    foreach (object obj in objs)
                    {
                        Rtm.CommandMethodAttribute attb =
                          (Rtm.CommandMethodAttribute)obj;
                        sc.Add(attb.GlobalName);
                    }
                }
            }
            string[] ret = new string[sc.Count];
            sc.CopyTo(ret, 0);

            return ret;
        }
        #endregion


        [Rtm.CommandMethod("dynIN")]
        static public void dynIN()
        {
            //Читаем данные из txt файла
            DynIN.IN();
        }

        [Rtm.CommandMethod("dynOUT")]
        static public void dynOUT()
        {
            //Сохраняем данные в txt файл
            DynOUT.OUT();
        }

        [Rtm.CommandMethod("pp")]
        static public void dynSET()
        {
            //Сохраняем данные в txt файл
            //DynSET.OUT();

           Db.ObjectId id =  AddEntity.CreateBlockReference("Otm_pola");
        }


        [Rtm.CommandMethod("GetAllDynamicBlockParameters")]
        public void GetAllDynamicBlockParameters()
        {
            App.Document doc = App.Application.DocumentManager.MdiActiveDocument;
            Db.Database db = doc.Database;
            Ed.Editor editor = doc.Editor;
            var option = new Ed.PromptEntityOptions("\n" + "Select a block");
            Ed.PromptEntityResult result = editor.GetEntity(option);
            if (result.Status == Ed.PromptStatus.OK)
            {
                Db.ObjectId id = result.ObjectId;
                using (Db.Transaction trans = db.TransactionManager.StartTransaction())
                {
                    var blockRef = (Db.BlockReference)trans.GetObject(id, Db.OpenMode.ForWrite);


                    Db.DynamicBlockReferencePropertyCollection properties = blockRef.DynamicBlockReferencePropertyCollection;
                    for (int i = 0; i < properties.Count; i++)
                    {
                        Db.DynamicBlockReferenceProperty property = properties[i];

                        editor.WriteMessage("\n" + property.PropertyName + " | " + property.PropertyTypeCode + " | " + property.Value);
                        //property.Value = (double)25;

                    }

                    Property mtc = new Property(blockRef.Handle);

                    //http://adndevblog.typepad.com/autocad/2012/05/comparing-properties-of-two-entities.html
                    System.Reflection.PropertyInfo[] propsBlockRef = blockRef.GetType().GetProperties();
                    System.Reflection.PropertyInfo[] propElement = mtc.GetType().GetProperties();

                    Dictionary<string, object> dd = new Dictionary<string, object>();

                    foreach (System.Reflection.PropertyInfo prop in propElement)
                    {


                        try
                        {
                            System.Reflection.PropertyInfo propBlock = propsBlockRef.Where(x => x.Name == prop.Name).FirstOrDefault();
                            if (propBlock != null)
                            {
                                object val1 = propBlock.GetValue(blockRef, null);
                                prop.SetValue(mtc, propBlock.GetValue(blockRef, null));


                                dd.Add(prop.Name, val1);
                            }
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                        {

                        }
                    }






                    trans.Commit();
                }
            }
        }


        // Информация о полях и реализуемых интерфейсах 
        public static List<string> FieldInterfaceInfo<T>(T obj) where T : class
        {
            List<string> outList = new List<string>();

            Type t = typeof(T);
            outList.Add("\n*** Реализуемые интерфейсы ***\n");
            var im = t.GetInterfaces();
            foreach (Type tp in im)
                outList.Add("--> " + tp.Name);

            outList.Add("\n*** Поля и свойства ***\n");
            FieldInfo[] fieldNames = t.GetFields();
            foreach (FieldInfo fil in fieldNames)
                outList.Add("--> " + fil.ReflectedType.Name + " " + fil.Name + "\n");

            return outList;
        }

        // В данном классе определены методы использующие рефлексию
        // Данный метод выводит информацию о содержащихся в классе методах
        public static List<string> MethodReflectInfo<T>(T obj) where T : class
        {
            List<string> outList = new List<string>();

            Type t = typeof(T);
            // Получаем коллекцию методов
            MethodInfo[] MArr = t.GetMethods();
            outList.Add($"*** Список методов класса {obj.ToString()} ***\n");

            // Вывести методы
            foreach (MethodInfo m in MArr)
            {

                string s = (" --> " + m.ReturnType.Name + " \t" + m.Name + "(");
                // Вывести параметры методов
                ParameterInfo[] p = m.GetParameters();
                for (int i = 0; i < p.Length; i++)
                {
                    s += (p[i].ParameterType.Name + " " + p[i].Name);
                    if (i + 1 < p.Length) s += (", ");
                }

                outList.Add(s += (")\n"));
            }

            return outList;
        }


        // В данном классе определены методы использующие рефлексию
        // Данный метод выводит информацию о содержащихся в классе методах
        public static List<string> MethodInfo<T>(T obj) where T : class
        {
            List<string> outList = new List<string>();
            // Получаем коллекцию методов
            MethodInfo[] MArr = typeof(T).GetMethods();
            // Вывести методы
            foreach (MethodInfo m in MArr)
                outList.Add(m.Name);
            return outList;
        }

    }
}
