using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

using dynINOUT_UI;

namespace dynIN_dynOUT
{
    /// <summary>
    /// Сохраняем данные в txt файл
    /// </summary>
    internal static class DynOUT
    {
        //Публичная переменная в которой храниться список имен блоков
        internal static ObservableCollection<string> _blockNameList = new ObservableCollection<string>();

        internal static void OUT()
        {


            //1. Подражая Attout сначала выбираем файл в который будет сохраняться информация
            // TODO по умолчанию имя нового файла должно соответствовать имени чертежа
            SaveFileDialog openFileDialog = new SaveFileDialog("Выберите CSV файл",
                                                      "*.csv",
                                                      "csv",
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




            //Сюда нужно встроить фильтрацию по имени блока
            _blockNameList.Clear(); // чистим хранилище имен блоков
            using (App.DocumentLock docloc = acDoc.LockDocument())
            {
                foreach (Ed.SelectedObject acSSObj in psr.Value)
                {
                    if (!acSSObj.ObjectId.IsNull && acSSObj.ObjectId.IsResident && acSSObj.ObjectId.IsValid && !acSSObj.ObjectId.IsErased)
                        if (acSSObj.ObjectId.ObjectClass.IsDerivedFrom(Rtm.RXObject.GetClass(typeof(Db.BlockReference))))
                        {
                            string blockName = "";
                            using (Db.BlockReference acBlRef = acSSObj.ObjectId.Open(Db.OpenMode.ForRead) as Db.BlockReference)
                            {
                                //TODO По хорошему этот кусок кода надо бы вынести в экстеншен метод класса BlockReference
                                blockName = acBlRef.EffectiveName();
                                acBlRef.Close();
                            }
                            if (!_blockNameList.Contains(blockName)) _blockNameList.Add(blockName);
                        }
                }
            }

            //Тут показываем пользователю окошко с выбором блоков по именам
            if (_blockNameList.Count > 1)
            {
                var dlg = new MainWindow();
                dlg.AddBlockNameList(_blockNameList);
                cad.ShowModalWindow(dlg);

                _blockNameList.Clear();

                foreach (var i in dlg.BindingList)
                    if (i.Value) _blockNameList.Add(i.Key);
            }

            //3. Проходимся по выбранным блокам и собираем информацию
            List<Property> propertyList = new List<Property>();


            //Блокируем документ
            using (App.DocumentLock docloc = acDoc.LockDocument())
            {
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
                                    Db.BlockReference acBlRef = (Db.BlockReference)acEnt;
                                    Db.BlockTableRecord blr = (Db.BlockTableRecord)acTrans.GetObject(acBlRef.DynamicBlockTableRecord,
                                                                                                    Db.OpenMode.ForRead);

                                    //Фильтр по именам блоков
                                    if (_blockNameList.Contains(acBlRef.EffectiveName()))
                                    {
                                        Property prop = new Property();
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
                                                        acEd.WriteMessage($"\nВ блоке {blr.Name}->{acBlRef.Name} придутствуют атрибуты с одинаковыми тегами");

                                                }
                                            }   //Проверка что кол аттрибутов больше 0
                                        }  //Проверка наличия атрибутов


                                        Db.DynamicBlockReferencePropertyCollection acBlockDynProp = acBlRef.DynamicBlockReferencePropertyCollection;
                                        if (acBlockDynProp != null)
                                        {
                                            foreach (Db.DynamicBlockReferenceProperty obj in acBlockDynProp)
                                            {
                                                //TODO а вот тут вопрос, нужно ли выводить значения, которые только ReadOnly
                                                if (obj.PropertyName != "Origin")
                                                {
                                                    if (!prop.DynProp.ContainsKey(obj.PropertyName))

                                                        prop.DynProp.Add(obj.PropertyName, obj.Value);
                                                    else
                                                        acEd.WriteMessage($"\nВ блоке {blr.Name}->{acBlRef.Name} придутствуют динамические свойства с одинаковыми именами");
                                                }

                                            }
                                        }
                                        propertyList.Add(prop);
                                    }
                                }   //Проверка, что объект это ссылка на блок
                            }
                        }
                    }
                    acTrans.Commit();

                }
            }

            //4. Формируем одну большую таблицу с данными
            //4.1 Считаем общее количество уникальны тегов атрибутов и уникальных названй динамических свойств
            List<string> unicAttName = new List<string>();
            List<string> unicDynName = new List<string>();

            foreach (var s in propertyList)
            {
                foreach (var i in s.Attribut)
                    if (!unicAttName.Contains("a_" + i.Key)) unicAttName.Add("a_" + i.Key);

                foreach (var i in s.DynProp)
                    if (!unicDynName.Contains("d_" + i.Key)) unicDynName.Add("d_" + i.Key);

            }


            //4.2 Заполняем массив
            List<string[]> rowList = new List<string[]>();

            List<string> rowHead = new List<string>();
            rowHead.Add("Handle");
            rowHead.AddRange(unicAttName);
            rowHead.AddRange(unicDynName);
            rowList.Add(rowHead.ToArray());

            int colCount = rowHead.Count;




            foreach (var s in propertyList)
            {
                //Создаем массив длинной , равной длинне заголовка
                string[] row = new string[colCount];
                //Все ячейки массива заполняем по умолчанию табуляциями
                for (int i = 0; i < row.Length; i++)
                    row[i] = "";

                //В первую ячейку массива пишу хендл объекта
                row[0] = $"\'{s.Handle.ToString()}";


                foreach (var i in s.Attribut)
                {
                    int indxUnicAttName = unicAttName.FindIndex(x => x == "a_" + i.Key);
                    row[1 + indxUnicAttName] = i.Value;
                }


                foreach (var i in s.DynProp)
                {
                    int indxUnicDynName = unicDynName.FindIndex(x => x == "d_" + i.Key);
                    row[1 + unicAttName.Count + indxUnicDynName] = i.Value.ToString();
                }


                //Добавляю 
                rowList.Add(row);
            }


            //5. Выводим собранные данные в файл
            try
            {
                using (StreamWriter sw = new StreamWriter(fileName, false, System.Text.Encoding.GetEncoding(1251)))
                {
                    foreach (var s in rowList)
                    {
                        sw.WriteLine(String.Join(";", s));
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
                Console.WriteLine($"\nDynOUT.Out-Ошибка записи в файл: {e.Message}");
            }


            //6. Оповещаем пользователя о завершении работы
            acEd.WriteMessage($"\nЭкспорт завершен.");

        }
    }
}
