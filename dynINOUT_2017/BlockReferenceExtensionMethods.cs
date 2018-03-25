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

    /// <summary>
    /// https://sites.google.com/site/bushmansnetlaboratory/moi-zametki/attsynch
    /// Методы расширений для объектов класса Autodesk.AutoCAD.DatabaseServices.BlockTableRecord
    /// </summary>
    public static class BlockReferenceExtensionMethods
    {
        public static string EffectiveName(this Db.BlockReference acBlockRef)
        {
           string  blockName = acBlockRef.Name;

            if (acBlockRef.IsDynamicBlock)
            {
                Db.ObjectId dynamicBlockTableRecordId = acBlockRef.DynamicBlockTableRecord;

                using (Db.BlockTableRecord blr_nam = dynamicBlockTableRecordId.Open(Db.OpenMode.ForRead) as Db.BlockTableRecord)
                {
                    blockName = blr_nam.Name;
                    blr_nam.Close();
                }
            }

            return blockName;
        }

        /// <summary>
        /// Синхронизация вхождений блоков с их определением
        /// </summary>
        /// <param name="btr">Запись таблицы блоков, принятая за определение блока</param>
        /// <param name="directOnly">Следует ли искать только на верхнем уровне, или же нужно 
        /// анализировать и вложенные вхождения, т.е. следует ли рекурсивно обрабатывать блок в блоке:
        /// true - только верхний; false - рекурсивно проверять вложенные блоки.</param>
        /// <param name="removeSuperfluous">
        /// Следует ли во вхождениях блока удалять лишние атрибуты (те, которых нет в определении блока).</param>
        /// <param name="setAttDefValues">
        /// Следует ли всем атрибутам, во вхождениях блока, назначить текущим значением значение по умолчанию.</param>
        public static void AttSync(this Db.BlockTableRecord btr, bool directOnly, bool removeSuperfluous, bool setAttDefValues)
        {
            Db.Database db = btr.Database;
            using (WorkingDatabaseSwitcher wdb = new WorkingDatabaseSwitcher(db))
            {
                using (Db.Transaction t = db.TransactionManager.StartTransaction())
                {
                    Db.BlockTable bt = (Db.BlockTable)t.GetObject(db.BlockTableId, Db.OpenMode.ForRead);

                    //Получаем все определения атрибутов из определения блока
                    IEnumerable<Db.AttributeDefinition> attdefs = btr.Cast<Db.ObjectId>()
                        .Where(n => n.ObjectClass.Name == "AcDbAttributeDefinition")
                        .Select(n => (Db.AttributeDefinition)t.GetObject(n, Db.OpenMode.ForRead))
                        .Where(n => !n.Constant);//Исключаем константные атрибуты, т.к. для них AttributeReference не создаются.

                    //В цикле перебираем все вхождения искомого определения блока
                    foreach (Db.ObjectId brId in btr.GetBlockReferenceIds(directOnly, false))
                    {
                        Db.BlockReference br = (Db.BlockReference)t.GetObject(brId, Db.OpenMode.ForWrite);

                        //Проверяем имена на соответствие. В том случае, если вхождение блока "A" вложено в определение блока "B", 
                        //то вхождения блока "B" тоже попадут в выборку. Нам нужно их исключить из набора обрабатываемых объектов 
                        //- именно поэтому проверяем имена.
                        if (br.Name != btr.Name)
                            continue;

                        //Получаем все атрибуты вхождения блока
                        IEnumerable<Db.AttributeReference> attrefs = br.AttributeCollection.Cast<Db.ObjectId>()
                            .Select(n => (Db.AttributeReference)t.GetObject(n, Db.OpenMode.ForWrite));

                        //Тэги существующих определений атрибутов
                        IEnumerable<string> dtags = attdefs.Select(n => n.Tag);
                        //Тэги существующих атрибутов во вхождении
                        IEnumerable<string> rtags = attrefs.Select(n => n.Tag);

                        //Если требуется - удаляем те атрибуты, для которых нет определения 
                        //в составе определения блока
                        if (removeSuperfluous)
                            foreach (Db.AttributeReference attref in attrefs.Where(n => rtags
                                .Except(dtags).Contains(n.Tag)))
                                attref.Erase(true);

                        //Свойства существующих атрибутов синхронизируем со свойствами их определений
                        foreach (Db.AttributeReference attref in attrefs.Where(n => dtags
                            .Join(rtags, a => a, b => b, (a, b) => a).Contains(n.Tag)))
                        {
                            Db.AttributeDefinition ad = attdefs.First(n => n.Tag == attref.Tag);

                            //Метод SetAttributeFromBlock, используемый нами далее в коде, сбрасывает
                            //текущее значение многострочного атрибута. Поэтому запоминаем это значение,
                            //чтобы восстановить его сразу после вызова SetAttributeFromBlock.
                            string value = attref.TextString;
                            attref.SetAttributeFromBlock(ad, br.BlockTransform);
                            //Восстанавливаем значение атрибута
                            attref.TextString = value;

                            if (attref.IsMTextAttribute)
                            {

                            }

                            //Если требуется - устанавливаем для атрибута значение по умолчанию
                            if (setAttDefValues)
                                attref.TextString = ad.TextString;

                            attref.AdjustAlignment(db);
                        }

                        //Если во вхождении блока отсутствуют нужные атрибуты - создаём их
                        IEnumerable<Db.AttributeDefinition> attdefsNew = attdefs.Where(n => dtags
                            .Except(rtags).Contains(n.Tag));

                        foreach (Db.AttributeDefinition ad in attdefsNew)
                        {
                            Db.AttributeReference attref = new Db.AttributeReference();
                            attref.SetAttributeFromBlock(ad, br.BlockTransform);
                            attref.AdjustAlignment(db);
                            br.AttributeCollection.AppendAttribute(attref);
                            t.AddNewlyCreatedDBObject(attref, true);
                        }
                    }
                    btr.UpdateAnonymousBlocks();
                    t.Commit();
                } // end Transaction


                //Если это динамический блок
                if (btr.IsDynamicBlock)
                {
                    using (Db.Transaction t = db.TransactionManager.StartTransaction())
                    {
                        foreach (Db.ObjectId id in btr.GetAnonymousBlockIds())
                        {
                            Db.BlockTableRecord _btr = (Db.BlockTableRecord)t.GetObject(id, Db.OpenMode.ForWrite);

                            //Получаем все определения атрибутов из оригинального определения блока
                            IEnumerable<Db.AttributeDefinition> attdefs = btr.Cast<Db.ObjectId>()
                                .Where(n => n.ObjectClass.Name == "AcDbAttributeDefinition")
                                .Select(n => (Db.AttributeDefinition)t.GetObject(n, Db.OpenMode.ForRead));

                            //Получаем все определения атрибутов из определения анонимного блока
                            IEnumerable<Db.AttributeDefinition> attdefs2 = _btr.Cast<Db.ObjectId>()
                                .Where(n => n.ObjectClass.Name == "AcDbAttributeDefinition")
                                .Select(n => (Db.AttributeDefinition)t.GetObject(n, Db.OpenMode.ForWrite));

                            //Определения атрибутов анонимных блоков следует синхронизировать 
                            //с определениями атрибутов основного блока

                            //Тэги существующих определений атрибутов
                            IEnumerable<string> dtags = attdefs.Select(n => n.Tag);
                            IEnumerable<string> dtags2 = attdefs2.Select(n => n.Tag);

                            //1. Удаляем лишние
                            foreach (Db.AttributeDefinition attdef in attdefs2.Where(n => !dtags.Contains(n.Tag)))
                            {
                                attdef.Erase(true);
                            }

                            //2. Синхронизируем существующие
                            foreach (Db.AttributeDefinition attdef in attdefs.Where(n => dtags
                               .Join(dtags2, a => a, b => b, (a, b) => a).Contains(n.Tag)))
                            {
                                Db.AttributeDefinition ad = attdefs2.First(n => n.Tag == attdef.Tag);
                                ad.Position = attdef.Position;
                                //ad.TextStyle = attdef.TextStyle;

                                //Если требуется - устанавливаем для атрибута значение по умолчанию
                                if (setAttDefValues)
                                    ad.TextString = attdef.TextString;

                                ad.Tag = attdef.Tag;
                                ad.Prompt = attdef.Prompt;
                                ad.LayerId = attdef.LayerId;
                                ad.Rotation = attdef.Rotation;
                                ad.LinetypeId = attdef.LinetypeId;
                                ad.LineWeight = attdef.LineWeight;
                                ad.LinetypeScale = attdef.LinetypeScale;
                                //ad.Annotative = attdef.Annotative;
                                ad.Color = attdef.Color;
                                ad.Height = attdef.Height;
                                ad.HorizontalMode = attdef.HorizontalMode;
                                ad.Invisible = attdef.Invisible;
                                ad.IsMirroredInX = attdef.IsMirroredInX;
                                ad.IsMirroredInY = attdef.IsMirroredInY;
                                ad.Justify = attdef.Justify;
                                ad.LockPositionInBlock = attdef.LockPositionInBlock;
                                ad.MaterialId = attdef.MaterialId;
                                ad.Oblique = attdef.Oblique;
                                ad.Thickness = attdef.Thickness;
                                ad.Transparency = attdef.Transparency;
                                ad.VerticalMode = attdef.VerticalMode;
                                ad.Visible = attdef.Visible;
                                ad.WidthFactor = attdef.WidthFactor;

                                ad.CastShadows = attdef.CastShadows;
                                ad.Constant = attdef.Constant;
                                ad.FieldLength = attdef.FieldLength;
                                ad.ForceAnnoAllVisible = attdef.ForceAnnoAllVisible;
                                ad.Preset = attdef.Preset;
                                ad.Prompt = attdef.Prompt;
                                ad.Verifiable = attdef.Verifiable;

                                ad.AdjustAlignment(db);
                            }

                            //3. Добавляем недостающие
                            foreach (Db.AttributeDefinition attdef in attdefs.Where(n => !dtags2.Contains(n.Tag)))
                            {
                                Db.AttributeDefinition ad = new Db.AttributeDefinition();
                                ad.SetDatabaseDefaults();
                                ad.Position = attdef.Position;
                                //ad.TextStyle = attdef.TextStyle;
                                ad.TextString = attdef.TextString;
                                ad.Tag = attdef.Tag;
                                ad.Prompt = attdef.Prompt;

                                ad.LayerId = attdef.LayerId;
                                ad.Rotation = attdef.Rotation;
                                ad.LinetypeId = attdef.LinetypeId;
                                ad.LineWeight = attdef.LineWeight;
                                ad.LinetypeScale = attdef.LinetypeScale;
                                //ad.Annotative = attdef.Annotative;
                                ad.Color = attdef.Color;
                                ad.Height = attdef.Height;
                                ad.HorizontalMode = attdef.HorizontalMode;
                                ad.Invisible = attdef.Invisible;
                                ad.IsMirroredInX = attdef.IsMirroredInX;
                                ad.IsMirroredInY = attdef.IsMirroredInY;
                                ad.Justify = attdef.Justify;
                                ad.LockPositionInBlock = attdef.LockPositionInBlock;
                                ad.MaterialId = attdef.MaterialId;
                                ad.Oblique = attdef.Oblique;
                                ad.Thickness = attdef.Thickness;
                                ad.Transparency = attdef.Transparency;
                                ad.VerticalMode = attdef.VerticalMode;
                                ad.Visible = attdef.Visible;
                                ad.WidthFactor = attdef.WidthFactor;

                                ad.CastShadows = attdef.CastShadows;
                                ad.Constant = attdef.Constant;
                                ad.FieldLength = attdef.FieldLength;
                                ad.ForceAnnoAllVisible = attdef.ForceAnnoAllVisible;
                                ad.Preset = attdef.Preset;
                                ad.Prompt = attdef.Prompt;
                                ad.Verifiable = attdef.Verifiable;

                                _btr.AppendEntity(ad);
                                t.AddNewlyCreatedDBObject(ad, true);
                                ad.AdjustAlignment(db);
                            }
                            //Синхронизируем все вхождения данного анонимного определения блока
                            _btr.AttSync(directOnly, removeSuperfluous, setAttDefValues);
                        }
                        //Обновляем геометрию определений анонимных блоков, полученных на основе 
                        //этого динамического блока
                        btr.UpdateAnonymousBlocks();
                        t.Commit();
                    }
                }
            }
        }



    }



    /// <summary>
    /// Изменяя базу данных чертежей, очень важно контролировать то, какая база данных является текущей. 
    /// Класс <c>WorkingDatabaseSwitcher</c>
    /// берёт на себя контроль над тем, чтобы текущей была именно та база данных, которая нужна.
    /// </summary>
    /// <example>
    /// Пример использования класса:
    /// <code>
    /// //db - объект Database
    /// using (WorkingDatabaseSwitcher hlp = new WorkingDatabaseSwitcher(db)) {
    ///     // тут наш код</code>
    /// }</example>
    public sealed class WorkingDatabaseSwitcher : IDisposable
    {
        private Db.Database prevDb = null;

        /// <summary>
        /// База данных, в контексте которой должна производиться работа. Эта база данных на время становится текущей.
        /// По завершению работы текущей станет та база, которая была ею до этого.
        /// </summary>
        /// <param name="db">База данных, которая должна быть установлена текущей</param>
        public WorkingDatabaseSwitcher(Db.Database db)
        {
            prevDb = Db.HostApplicationServices.WorkingDatabase;
            Db.HostApplicationServices.WorkingDatabase = db;
        }

        /// <summary>
        /// Возвращаем свойству <c>HostApplicationServices.WorkingDatabase</c> прежнее значение
        /// </summary>
        public void Dispose()
        {
            Db.HostApplicationServices.WorkingDatabase = prevDb;
        }
    }
}
