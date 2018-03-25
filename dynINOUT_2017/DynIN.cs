﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

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
            OpenFileDialog openFileDialog = new OpenFileDialog("Выберите CSV файл",
                                          "*.csv",
                                          "csv",
                                          "Выбор файла",
                                          OpenFileDialog.OpenFileDialogFlags.NoUrls);

            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            string fileName = openFileDialog.Filename;



            List<string> fileLines = new List<string>();
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
                acEd.WriteMessage($"\nDynIN.IN-Ошибка чтения файла: {e.Message}");
                return;
            }



            List<Property> propertyList = new List<Property>();
            //Парсим первую строку
            List<string> rowHead = fileLines[0].Split(';').ToList();
            //Парсим основное тело
            for (int i = 1; i < fileLines.Count; i++)
            {
                Property prop = new Property();
                //bool t = prop.Sets(rowHead, fileLines[i]);
                if (prop.Sets(rowHead, fileLines[i]))
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

                            //TODO Масло маслянное
                            Db.Handle h = new Db.Handle(prop.Handle.Value);
                            id = acCurDb.GetObjectId(false, h, 0);

                        }
                        catch (Exception e)
                        {

                            acEd.WriteMessage($"\nDynIN.IN-Ошибка поиска объекта: {e.Message}");
                            //return;
                        }

                        //Если не нашли, идем к следующему объекту
                        if (id == null) break;

                        //Полученный объект вообще блок, если нет то переходим к следующему
                        if (!id.ObjectClass.IsDerivedFrom(Rtm.RXObject.GetClass(typeof(Db.BlockReference)))) break;


                        Db.BlockReference acBlRef = acTrans.GetObject(id, Db.OpenMode.ForWrite) as Db.BlockReference;
                        Db.BlockTableRecord blr = (Db.BlockTableRecord)acTrans.GetObject(acBlRef.DynamicBlockTableRecord,
                                                                    Db.OpenMode.ForRead);


                        PropertyInfo[] propsBlockRef = acBlRef.GetType().GetProperties();
                        PropertyInfo[] propElement = prop.GetType().GetProperties();

                        foreach (PropertyInfo propInfo in propElement)
                        {
                            try
                            {
                                //System.Reflection.PropertyInfo propBlock = propsBlockRef.Where(x => x.Name == propInfo.Name).FirstOrDefault();
                                //if (propBlock != null) propInfo.SetValue(prop, propBlock.GetValue(acBlRef, null));

                                PropertyInfo propBlock = propsBlockRef.Where(x => x.Name == propInfo.Name).FirstOrDefault();

                                object oo = propInfo.GetValue(prop, null);
                                if (propBlock != null)
                                {
                                    propBlock.SetValue(acBlRef, propInfo.GetValue(prop, null));
                                }

                            }
                            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                            {

                            }
                        }


                        if (blr.HasAttributeDefinitions)
                        {
                            Db.AttributeCollection attrCol = acBlRef.AttributeCollection;
                            if (attrCol.Count > 0)
                            {
                                foreach (Db.ObjectId AttID in attrCol)
                                {
                                    Db.AttributeReference acAttRef = acTrans.GetObject(AttID,
                                                            Db.OpenMode.ForRead) as Db.AttributeReference;

                                    foreach (var i in prop.Attribut)
                                    {
                                        //Обновляем только в том случае если были изменения
                                        if (acAttRef.Tag == i.Key && acAttRef.TextString != i.Value)
                                        {
                                            acAttRef.UpgradeOpen();
                                            acAttRef.TextString = i.Value;
                                            //acAttRef.RecordGraphicsModified(true);
                                            acAttRef.DowngradeOpen();
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
                                    //Дополнительно проверяем можно ли вообще обновить значение
                                    if (obj.PropertyName == i.Key && !obj.ReadOnly)
                                    {
                                        //Нужно проверить тип объекта
                                        if (obj.UnitsType == Db.DynamicBlockReferencePropertyUnitsType.Angular
                                            || obj.UnitsType == Db.DynamicBlockReferencePropertyUnitsType.Distance
                                            || obj.UnitsType == Db.DynamicBlockReferencePropertyUnitsType.Area)
                                        {
                                            double d = double.Parse(i.Value.ToString());
                                            //Обновляем только в том случае если были изменения
                                            //Как то коряво
                                            if (obj.Value != d as object)
                                                obj.Value = d;
                                        }

                                        //http://adn-cis.org/chtenie-tabliczyi-svojstv-bloka-dlya-dinamicheskogo-bloka.html
                                        //Тут можно посмотреть наименование свойств в таблице


                                        //http://adn-cis.org/forum/index.php?topic=603.msg2033#msg2033
                                        if (obj.UnitsType == Db.DynamicBlockReferencePropertyUnitsType.NoUnits)
                                        {

                                            object d = null;// = new object();

                                            switch (obj.PropertyTypeCode)
                                            {
                                                case (short)DwgDataType.kDwgNull: //0
                                                    break;
                                                case (short)DwgDataType.kDwgReal: //1
                                                    d = Double.Parse(i.Value.ToString()) as object;
                                                    break;                                                                                   //return true;
                                                case (short)DwgDataType.kDwgInt32: //2
                                                    d = Int32.Parse(i.Value.ToString()) as object;
                                                    break;                                                                       //return true;
                                                case (short)DwgDataType.kDwgInt16:  //3 
                                                                                    //Flip state
                                                                                    //Block Properties Table

                                                    //Отрицательное значение - вроде как выставленное по умолчанию...
                                                    // и отрицательное значение не присвоить Block Properties Table
                                                    //TODO , а как с Flip state еще не тестировал.
                                                    var j = short.Parse(i.Value.ToString());
                                                    if (j > 0) d = j as object;

                                                    break;
                                                case (short)DwgDataType.kDwgInt8: //4
                                                    //Возможно так... Int8
                                                    d = short.Parse(i.Value.ToString()) as object;
                                                    break;
                                                case (short)DwgDataType.kDwgText: //5  
                                                    //Lookup
                                                    d = i.Value.ToString() as object;
                                                    break;
                                                case (short)DwgDataType.kDwgBChunk: //6
                                                    break;
                                                case (short)DwgDataType.kDwgHandle: //7
                                                    d = long.Parse(i.Value.ToString()) as object;
                                                    break;
                                                case (short)DwgDataType.kDwgHardOwnershipId: //8
                                                    break;
                                                case (short)DwgDataType.kDwgSoftOwnershipId: //9
                                                    break;
                                                case (short)DwgDataType.kDwgHardPointerId: //12 
                                                    //Origin (double[2])
                                                    break;
                                                case (short)DwgDataType.kDwgSoftPointerId: //11
                                                    break;
                                                case (short)DwgDataType.kDwg3Real: //12
                                                    break;
                                                case (short)DwgDataType.kDwgInt64: //13
                                                    d = Int64.Parse(i.Value.ToString()) as object;
                                                    break;

                                                case (short)DwgDataType.kDwgNotRecognized: //19
                                                    break;
                                                default:
                                                    throw new InvalidCastException("You can't cast a weird value!");
                                            }

                                            if (d != null && obj.Value != d && !obj.ReadOnly)
                                            {
                                                obj.Value = d;
                                            }




                                        }

                                    }

                                }


                            }
                        }

                        //обновляем атрибуты
                        blr.AttSync(true, false, false);
                        //маркеруем блок, как блок с измененный графикой
                        acBlRef.RecordGraphicsModified(true);
                    }

                    acTrans.Commit();
                }
            }


            //5. Оповещаем пользователя о завершении работы
            //Перерисовать графику
            //http://adn-cis.org/forum/index.php?topic=8361.0
            acDoc.TransactionManager.FlushGraphics();

            acEd.WriteMessage($"\nDone.");
        }


    }
}
