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
                    Db.BlockTableRecord acBlkTblRec = tr.GetObject(acBlkTbl[blockName], Db.OpenMode.ForRead) as Db.BlockTableRecord;
                    Db.ObjectId BRId= acBlkTblRec.GetBlockReferenceIds(true, true)[acBlkTblRec.GetBlockReferenceIds(true, true).Count-1];

                    // копирование выбранных объектов в блок
                    Db.ObjectIdCollection ids = new Db.ObjectIdCollection();
                    ids.Add(BRId);

                    Db.IdMapping mapping = new Db.IdMapping();
                    db.DeepCloneObjects(ids, acBlkTbl[Db.BlockTableRecord.ModelSpace], mapping, false);

                    foreach (Db.IdPair pair in mapping)
                    {
                        if (pair.Key == BRId) newBtrId = pair.Value;
                        //if (pair.IsCloned)
                        //{
                        //    var cloned = tran.GetObject(
                        //        pair.Value, OpenMode.ForRead) as Entity;
                        //    if (cloned != null)
                        //    {
                        //        cloned.UpgradeOpen();
                        //        cloned.TransformBy(transform);
                        //    }
                        //}
                    }
                }
                else
                {
                    return newBtrId;
                }
                tr.Commit();
            }

            return newBtrId;
        }

    }
}
