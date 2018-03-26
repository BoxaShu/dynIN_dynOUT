using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Db = Autodesk.AutoCAD.DatabaseServices;
using Gem = Autodesk.AutoCAD.Geometry;

namespace dynIN_dynOUT
{
    /// <summary>
    /// Вспомогательный класс для создания объектов 
    /// в базе чертежа
    /// </summary>
    static public class AddEntity
    {
        /// <summary>
        /// Проверяем и создаем заданный слой
        /// </summary>
        /// <param name="name">Имя слоя</param>
        /// <param name="create">true - если слоя с таким именем нет, то он будет создан</param>
        /// <returns></returns>
        static public bool CreateLayer(string name, bool create)
        {
            bool outBool = false;

            Db.Database db = Db.HostApplicationServices.WorkingDatabase;
            using (Db.Transaction acTrans = db.TransactionManager.StartTransaction())
            {
                Db.LayerTable layerTable = (Db.LayerTable)acTrans.GetObject(db.LayerTableId, Db.OpenMode.ForRead);

                if (!layerTable.Has(name))
                {
                    if (create)
                    {
                        using (Db.LayerTableRecord layerRecord = new Db.LayerTableRecord())
                        {
                            layerRecord.Name = name;
                            layerTable.UpgradeOpen();
                            layerTable.Add(layerRecord);
                            acTrans.AddNewlyCreatedDBObject(layerRecord, true);
                        }
                        outBool = true;
                    }
                }
                else
                {
                    outBool = true;
                }

                acTrans.Commit();
            }
            return outBool;
        }



        /// <summary>
        /// Проверяем и создаем заданный блок
        /// </summary>
        /// <param name="blockName">Имя блока</param>
        /// <returns></returns>
        static public Db.ObjectId CreateBlockReference(string blockName)
        {

            Db.ObjectId newBtrId = Db.ObjectId.Null;

            Db.Database db = Db.HostApplicationServices.WorkingDatabase;


            using (Db.Transaction tr = db.TransactionManager.StartTransaction())
            {
                Db.BlockTable acBlkTbl = tr.GetObject(db.BlockTableId, Db.OpenMode.ForRead) as Db.BlockTable;
                if (acBlkTbl.Has(blockName))
                {
                    Db.BlockTableRecord acBlkTblRec = tr.GetObject(acBlkTbl[blockName], Db.OpenMode.ForWrite) as Db.BlockTableRecord;

                    Db.ObjectId BRId = Db.ObjectId.Null;

                    using (Db.BlockTableRecord ms = (Db.BlockTableRecord)tr.GetObject(acBlkTbl[Db.BlockTableRecord.ModelSpace], Db.OpenMode.ForWrite))
                    using (Db.BlockReference br = new Db.BlockReference(Gem.Point3d.Origin, acBlkTblRec.ObjectId))
                    {

                        ms.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);

                        //Получаем все определения атрибутов из определения блока
                        IEnumerable<Db.AttributeDefinition> attdefs = acBlkTblRec.Cast<Db.ObjectId>()
                            .Where(n => n.ObjectClass.Name == "AcDbAttributeDefinition")
                            .Select(n => (Db.AttributeDefinition)tr.GetObject(n, Db.OpenMode.ForRead))
                            .Where(n => !n.Constant);//Исключаем константные атрибуты, т.к. для них AttributeReference не создаются.

                        foreach (Db.AttributeDefinition attref in attdefs)
                        {
                            Db.AttributeReference ar = new Db.AttributeReference();
                            ar.SetAttributeFromBlock(attref, br.BlockTransform);
                            br.AttributeCollection.AppendAttribute(ar);
                            tr.AddNewlyCreatedDBObject(ar, true);

                        }


                        newBtrId = br.ObjectId;
                    }
                }
                tr.Commit();
            }
            return newBtrId;
        }

    }
}
