using System;
using System.Collections.Generic;
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
                            }   //Проверка, что объект это ссылка на блок
                        }
                    }
                }
                acTrans.Commit();

            }


            //4. Выводим собранные данные в файл
            foreach(var s in propertyList)
            {

            }

            //5. Оповещаем пользователя о завершении работы
            acEd.WriteMessage($"\nЭкспорт завершен.");

        }
    }
}
