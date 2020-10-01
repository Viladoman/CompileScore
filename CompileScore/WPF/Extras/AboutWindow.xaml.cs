using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CompileScore.Extras
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            var asm = Assembly.GetEntryAssembly();
            var asmName = asm.GetName();
            appVersionTxt.Text = "Application Version: "+ asmName.Version.ToString();
            dataVersionTxt.Text = "Data Version: "+CompilerData.VERSION;
        }

        private void Hyperlink_ReportIssue(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        void OnOk(object sender, object e)
        {
            this.Close();
        }
    }
}
