using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPythonPatternMaker.Core.Config
{
    public class AppConfig
    {
        public double Scale { get; set; } = 1.0;
        public double Epsilon { get; set; } = 0.005;
        public double MinArea { get; set; } = 30.0;
        public int RedThreshold { get; set; } = 200;
    }
}
