using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dynIN_dynOUT
{
    public static class Setting
    {
        /// <summary>
        ///Создавать или нет слой, если его нет в базе чертежа
        /// </summary>
        public static bool CreateLayer { get; set; } = true;

        /// <summary>
        /// ///Создавать или нет блок, если вместо хендла написано имя блока
        /// </summary>
        public static bool CreateBlocReference { get; set; } = true;


    }
}
