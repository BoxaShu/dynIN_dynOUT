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
    }


}
