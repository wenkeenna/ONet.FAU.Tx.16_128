using ONet.FAU.Tx16_128.Extension.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ONet.FAU.Tx16_128.Extension.Events
{
    public class InstrumentPara
    {
        public InstrumentType Type { get;  }
        public InstrumentSwitch Switch { get; }
        public InstrumentPara(InstrumentType type,InstrumentSwitch iswitch) 
        {
            Type = type;
            Switch = iswitch;
        }


    }
}
