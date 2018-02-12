using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Windows;
using App = Autodesk.AutoCAD.ApplicationServices;
using cad = Autodesk.AutoCAD.ApplicationServices.Application;
using Db = Autodesk.AutoCAD.DatabaseServices;
using Ed = Autodesk.AutoCAD.EditorInput;
using Gem = Autodesk.AutoCAD.Geometry;
using Rtm = Autodesk.AutoCAD.Runtime;

namespace dynIN_dynOUT
{
    /// <summary>
    /// Читаем данные из txt файла
    /// </summary>
    internal static class DynIN
    {

        internal static void IN()
        {

            // Получение текущего документа и базы данных
            App.Document acDoc = App.Application.DocumentManager.MdiActiveDocument;
            if (acDoc == null) return;
            Db.Database acCurDb = acDoc.Database;
            Ed.Editor acEd = acDoc.Editor;

            //1. Читаем и парсим файл
            OpenFileDialog openFileDialog = new OpenFileDialog("Выберите txt файл",
                                          "*.txt",
                                          "txt",
                                          "Выбор файла",
                                          OpenFileDialog.OpenFileDialogFlags.NoUrls);

            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            string fileName = openFileDialog.Filename;



            List<string> fileLines =  new List<string>();
            try
            {
                //using (StreamReader sr = new StreamReader(fileName, System.Text.Encoding.Default))
                //{
                //   fileLines = sr.ReadToEnd().Split('\n').ToList();
                //}

                //using (StreamWriter sw = new StreamWriter(fileName, false, System.Text.Encoding.GetEncoding(1251)))
                //{
                //    foreach (var s in rowList)
                //    {
                //        sw.WriteLine(String.Join("\t", s));
                //    }
                //}


                fileLines = System.IO.File.ReadAllLines(fileName, System.Text.Encoding.GetEncoding(1251)).ToList();
            }
            catch (Exception e)
            {
                acEd.WriteMessage($"\nОшибка чтения файла: {e.Message}");
                return;
            }



            List<Property> propertyList = new List<Property>();

            //Парсим первую строку
            List<string> unicAttName = new List<string>();
            List<string> unicDynName = new List<string>();

            List<string> l = fileLines[0].Split('\t').ToList();
            foreach(string s in l)
            {
                if (s.Substring(0, 2) == "a_")
                {
                    unicAttName.Add(s.Substring(2, s.Length-2));
                }
                if (s.Substring(0, 2) == "d_")
                {
                    unicDynName.Add(s.Substring(2, s.Length - 2));
                }
            }






            //Парсим основное тело
            for (int i = 1; i < fileLines.Count;  i++)
            {
                Property prop = new Property();

                l = fileLines[i].Split('\t').ToList();

                prop.Handle = long.Parse(l[0]);

                //Нужно соотнести значение с названием параметра
                for(int j =1; j < l.Count; j++)
                {
                    if (l[j] != "")
                    {
                        //Если индекс ячейки со значением лежит в обрасти занчений атрибутов
                        // TODO заменить постоянный расчет диапазонов на простые переменные
                        if (1 + j > 1 && 1+j < 1+ unicAttName.Count)
                        {
                            prop.Attribut.Add(unicAttName[j-1], l[j]);
                        }

                        if (1 + j > 1+ unicAttName.Count )
                        {
                            int p = j - 1 - unicAttName.Count;
                            prop.DynProp.Add(unicDynName[p], l[j]);
                        }

                    }
                }

                propertyList.Add(prop);
            }






            //Блокируем документ
            using (App.DocumentLock docloc = acDoc.LockDocument())
            {


                // старт транзакции
                using (Db.Transaction acTrans = acCurDb.TransactionManager.StartOpenCloseTransaction())
                {

                    foreach (var prop in propertyList)
                    {
                        Db.ObjectId id = Db.ObjectId.Null;
                        try
                        {
                            Db.Handle h = new Db.Handle(prop.Handle);
                            id = acCurDb.GetObjectId(false, h, 0);

                        }
                        catch (Exception e)
                        {

                            acEd.WriteMessage($"\nОшибка поиска объекта: {e.Message}");
                            //return;
                        }

                        //Если не нашли, идем к следующему объекту
                        if (id == null) break;

                        //Полученный объект вообще блок, если нет то переходим к следующему
                        if (!id.ObjectClass.IsDerivedFrom(Rtm.RXObject.GetClass(typeof(Db.BlockReference)))) break;


                        Db.BlockReference acBlRef = acTrans.GetObject(id, Db.OpenMode.ForWrite) as Db.BlockReference;
                        Db.BlockTableRecord blr = (Db.BlockTableRecord)acTrans.GetObject(acBlRef.DynamicBlockTableRecord,
                                                                    Db.OpenMode.ForRead);
                        Db.BlockTableRecord blr_nam = (Db.BlockTableRecord)acTrans.GetObject(blr.ObjectId,
                                                                                    Db.OpenMode.ForRead);


                        if (blr.HasAttributeDefinitions)
                        {
                            Db.AttributeCollection attrCol = acBlRef.AttributeCollection;
                            if (attrCol.Count > 0)
                            {
                                foreach (Db.ObjectId AttID in attrCol)
                                {
                                    Db.AttributeReference acAttRef = acTrans.GetObject(AttID,
                                                            Db.OpenMode.ForWrite) as Db.AttributeReference;

                                    foreach (var i in prop.Attribut)
                                    {
                                        if (acAttRef.Tag == i.Key)
                                        {
                                            acAttRef.TextString = i.Value;
                                            break;
                                        }
                                        
                                    }

                                }
                            }   //Проверка что кол аттрибутов больше 0
                        }  //Проверка наличия атрибутов


                        Db.DynamicBlockReferencePropertyCollection acBlockDynProp = acBlRef.DynamicBlockReferencePropertyCollection;
                        if (acBlockDynProp != null)
                        {
                            foreach (Db.DynamicBlockReferenceProperty obj in acBlockDynProp)
                            {

                                foreach (var i in prop.DynProp)
                                {
                                    if (obj.PropertyName == i.Key)
                                    {
                                        //Нужно проверить тип объекта
                                        if(obj.UnitsType == Db.DynamicBlockReferencePropertyUnitsType.Angular 
                                            || obj.UnitsType == Db.DynamicBlockReferencePropertyUnitsType.Distance 
                                            || obj.UnitsType == Db.DynamicBlockReferencePropertyUnitsType.Distance)
                                        {
                                            obj.Value = double.Parse(i.Value.ToString());
                                        }

                                        //http://adn-cis.org/forum/index.php?topic=603.msg2033#msg2033
                                        if (obj.UnitsType == Db.DynamicBlockReferencePropertyUnitsType.NoUnits)
                                        {

                                        }

                                    }

                                }


                            }
                        }
                    }

                    acTrans.Commit();
                }
            }


            //5. Оповещаем пользователя о завершении работы
            acEd.WriteMessage($"\nЭкспорт завершен.");
        }


    }
}
