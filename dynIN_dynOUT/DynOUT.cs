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
    /// Сохраняем данные в txt файл
    /// </summary>
    internal static class DynOUT
    {

        internal static void OUT()
        {
            //1. Подражая Attout сначала выбираем файл в который будет сохраняться информация
            // TODO по умолчанию имя нового файла должно соответствовать имени чертежа
            SaveFileDialog openFileDialog = new SaveFileDialog("Выберите txt файл",
                                                      "*.txt",
                                                      "txt",
                                                      "Выбор файла",
                                                      SaveFileDialog.SaveFileDialogFlags.NoUrls);

            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            string fileName = openFileDialog.Filename;

            // Получение текущего документа и базы данных
            App.Document acDoc = App.Application.DocumentManager.MdiActiveDocument;
            if (acDoc == null) return;
            Db.Database acCurDb = acDoc.Database;
            Ed.Editor acEd = acDoc.Editor;


            //2. Запрашиваем у пользователя выбор блоков
            Db.TypedValue[] tv = new Db.TypedValue[4];
            tv.SetValue(new Db.TypedValue((int)Db.DxfCode.Operator, "<AND"), 0);
            //Фильтр по типу объекта
            tv.SetValue(new Db.TypedValue((int)Db.DxfCode.Start, "INSERT"), 1);
            //Фильтр по имени слоя
            tv.SetValue(new Db.TypedValue((int)Db.DxfCode.LayerName, "*"), 2);
            tv.SetValue(new Db.TypedValue((int)Db.DxfCode.Operator, "AND>"), 3);

            Ed.SelectionFilter sf = new Ed.SelectionFilter(tv);

            Ed.PromptSelectionResult psr = acEd.GetSelection(sf);
            if (psr.Status != Ed.PromptStatus.OK) return;


            //3. Проходимся по выбранным блокам и собираем информацию
            List<Property> propertyList = new List<Property>();


            using (Db.Transaction acTrans = acCurDb.TransactionManager.StartOpenCloseTransaction())
            {
                Ed.SelectionSet acSSet = psr.Value;

                foreach (Ed.SelectedObject acSSObj in acSSet)
                {
                    if (acSSObj != null)
                    {
                        Db.Entity acEnt = acTrans.GetObject(acSSObj.ObjectId,
                                                 Db.OpenMode.ForRead) as Db.Entity;
                        if (acEnt != null)
                        {
                            if (acEnt is Db.BlockReference)
                            {
                                Property prop = new Property();


                                Db.BlockReference acBlRef = (Db.BlockReference)acEnt;
                                Db.BlockTableRecord blr = (Db.BlockTableRecord)acTrans.GetObject(acBlRef.DynamicBlockTableRecord,
                                                                                                Db.OpenMode.ForRead);
                                Db.BlockTableRecord blr_nam = (Db.BlockTableRecord)acTrans.GetObject(blr.ObjectId,
                                                                                            Db.OpenMode.ForRead);

                                prop.Handle = acEnt.Handle.Value;

                                if (blr.HasAttributeDefinitions)
                                {
                                    Db.AttributeCollection attrCol = acBlRef.AttributeCollection;
                                    if (attrCol.Count > 0)
                                    {
                                        foreach (Db.ObjectId AttID in attrCol)
                                        {
                                            Db.AttributeReference acAttRef = acTrans.GetObject(AttID,
                                                                    Db.OpenMode.ForRead) as Db.AttributeReference;

                                            //TODO Необходимо проверить и учесть наличие полей
                                            if (!prop.Attribut.ContainsKey(acAttRef.Tag))
                                                prop.Attribut.Add(acAttRef.Tag, acAttRef.TextString);
                                            else
                                                acEd.WriteMessage($"\nВ блоке {blr_nam} придутствуют атрибуты с одинаковыми тегами");

                                        }
                                    }   //Проверка что кол аттрибутов больше 0
                                }  //Проверка наличия атрибутов


                                Db.DynamicBlockReferencePropertyCollection acBlockDynProp = acBlRef.DynamicBlockReferencePropertyCollection;
                                if (acBlockDynProp != null)
                                {
                                    foreach (Db.DynamicBlockReferenceProperty obj in acBlockDynProp)
                                    {
                                        if (!prop.DynProp.ContainsKey(obj.PropertyName))
                                            prop.DynProp.Add(obj.PropertyName, obj.Value);
                                        else
                                            acEd.WriteMessage($"\nВ блоке {blr_nam} придутствуют динамические свойства с одинаковыми именами");
                                    }
                                }


                                propertyList.Add(prop);

                            }   //Проверка, что объект это ссылка на блок
                        }
                    }
                }
                acTrans.Commit();

            }


            //4. Формируем одну большую таблицу с данными
            //4.1 Считаем общее количество уникальны тегов атрибутов и уникальных названй динамических свойств
            List<string> unicAttName = new List<string>();
            List<string> unicDynName = new List<string>();

            foreach (var s in propertyList)
            {
                foreach (var i in s.Attribut)
                    if(!unicAttName.Contains("a_" + i.Key)) unicAttName.Add("a_"+i.Key);

                foreach (var i in s.DynProp)
                    if (!unicDynName.Contains("d_" + i.Key)) unicDynName.Add("d_"+i.Key);
            }


            //4.2 Заполняем массив
            List<string[]> rowList = new List<string[]>();

            List <string>rowHead = new List<string>();
            rowHead.Add("Handle");
            rowHead.AddRange(unicAttName);
            rowHead.AddRange(unicDynName);


            rowList.Add(rowHead.ToArray());

            int colCount = rowHead.Count;


            foreach (var s in propertyList)
            {
                string[] row = new string[colCount];
                for(int i =0; i< row.Length; i++)
                    row [i]= "\t";


                row[0] = s.Handle.ToString();

                foreach (var i in s.Attribut)
                {
                    int indxUnicAttName = unicAttName.FindIndex(x => x == "a_" + i.Key);
                    row[1 + indxUnicAttName] = i.Value;
                }

                foreach (var i in s.DynProp)
                {
                    int indxUnicDynName = unicDynName.FindIndex(x => x == "d_" + i.Key);
                    row[1 + unicAttName.Count+ indxUnicDynName] = i.Value.ToString() ;
                }

                rowList.Add(row);
            }


            //5. Выводим собранные данные в файл
            try
            {
                using (StreamWriter sw = new StreamWriter(fileName, false, System.Text.Encoding.ASCII))
                {
                    foreach (var s in rowList)
                    {
                        sw.WriteLine(String.Join("\t",s));
                    }
                }

                //using (StreamWriter sw = new StreamWriter(fileName, true, System.Text.Encoding.Default))
                //{
                //    sw.WriteLine("Дозапись");
                //    sw.Write(4.5);
                //}
            }
            catch (Exception e)
            {
                Console.WriteLine($"\nОшибка записи в файл: {e.Message}");
            }


            //6. Оповещаем пользователя о завершении работы
            acEd.WriteMessage($"\nЭкспорт завершен.");

        }
    }
}
