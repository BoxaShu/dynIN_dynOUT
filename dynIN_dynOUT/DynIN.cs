using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using App = Autodesk.AutoCAD.ApplicationServices;
using cad = Autodesk.AutoCAD.ApplicationServices.Application;
using Db = Autodesk.AutoCAD.DatabaseServices;
using Ed = Autodesk.AutoCAD.EditorInput;
using Gem = Autodesk.AutoCAD.Geometry;
using Rtm = Autodesk.AutoCAD.Runtime;

namespace dynIN_dynOUT
{
    internal static class DynIN
    {

        internal static void IN()
        {
            // Получение текущего документа и базы данных
            App.Document acDoc = App.Application.DocumentManager.MdiActiveDocument;
            Db.Database acCurDb = acDoc.Database;
            // старт транзакции
            using (Db.Transaction acTrans = acCurDb.TransactionManager.StartOpenCloseTransaction())
            {
                // Открытие таблицы Блоков для чтения
                Db.BlockTable acBlkTbl = acTrans.GetObject(acCurDb.BlockTableId, Db.OpenMode.ForRead) as Db.BlockTable;

                // Открытие записи таблицы Блоков пространства Модели для записи
                Db.BlockTableRecord acBlkTblRec = acTrans.GetObject(acBlkTbl[Db.BlockTableRecord.ModelSpace],
                                                                                Db.OpenMode.ForWrite) as Db.BlockTableRecord;

                // Создание отрезка начинающегося в 0,0 и заканчивающегося в 5,5
                Db.Line acLine = new Db.Line(new Gem.Point3d(0, 0, 0), new Gem.Point3d(5, 5, 0));
                acLine.SetDatabaseDefaults();
                // Добавление нового объекта в запись таблицы блоков и в транзакцию
                acBlkTblRec.AppendEntity(acLine);
                acTrans.AddNewlyCreatedDBObject(acLine, true);
                // Сохранение нового объекта в базе данных
                acTrans.Commit();
            }
        }


    }
}
