using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WindowsFormsApplication1
{
    class element
    {
        double t;
        double v, i;

        public element(double t_a, double v_a, double i_a)
        {
            t = t_a;
            v = v_a;
            i = i_a;
        }

        public double get_t (){
            return t;
        }
        public double get_v()
        {
            return v;
        }
        public double get_i()
        {
            return i;
        }

    }
}
