using ONet.FAU.Tx._16_128.Extension.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ONet.FAU.Tx._16_128.Extension.Model
{
    public class Result1D
    {
        public bool Success { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double BestPos { get; set; }

        public List<CouplingData> CouplingData;
    }
}
