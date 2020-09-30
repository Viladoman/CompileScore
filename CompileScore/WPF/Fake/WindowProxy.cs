using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CompileScore.Common
{
    public class WindowProxy : Window
    {
        public string Caption { get { return Title; } set { Title = value; } }

        public object GetFrame() { return this; }

        public void ProxyShow()
        {
            Show();
        }
    }
}
