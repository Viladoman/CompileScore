using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using System.Xml;

#if CS_VSIX
namespace Company.Product
{
    public class VsixManifest
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Description { get; set; }

        public VsixManifest(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);

            if (doc.DocumentElement == null || doc.DocumentElement.Name != "PackageManifest") return;

            var metaData = doc.DocumentElement.ChildNodes.Cast<XmlElement>().First(x => x.Name == "Metadata");
            var identity = metaData.ChildNodes.Cast<XmlElement>().First(x => x.Name == "Identity");
            var descNode = metaData.ChildNodes.Cast<XmlElement>().First(x => x.Name == "Description");

            Id = identity.GetAttribute("Id");
            Version = identity.GetAttribute("Version");
            Description = descNode.InnerText;
        }

        public static VsixManifest GetManifest()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyUri = new UriBuilder(assembly.CodeBase);
            var assemblyPath = Uri.UnescapeDataString(assemblyUri.Path);
            var assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyPath);
            var vsixManifestPath = System.IO.Path.Combine(assemblyDirectory, "extension.vsixmanifest");

            return new VsixManifest(vsixManifestPath);
        }
    }
}
#endif

namespace CompileScore
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

#if CS_VSIX
            var manifest = Company.Product.VsixManifest.GetManifest();

            descriptionTxt.Text = manifest.Description;
            appVersionTxt.Text = "Version: " + manifest.Version;
            dataVersionTxt.Text = "Data Version: " + CompilerData.VERSION;
#else
            var asm = Assembly.GetEntryAssembly();
            var asmName = asm.GetName();
            descriptionTxt.Text = "App for build times and compilation data visualization.";
            appVersionTxt.Text = "Application Version: "+ asmName.Version.ToString();
            dataVersionTxt.Text = "Data Version: "+CompilerData.VERSION;
#endif

        }

        private void OnReportIssue(object sender, object e)
        {
            this.Close();
            Documentation.OpenLink(Documentation.Link.ReportIssue);
        }

        private void OnGithub(object sender, object e)
        {
            this.Close();
            Documentation.OpenLink(Documentation.Link.MainPage);
        }

        private void OnDonate(object sender, object e)
        {
            this.Close();
            Documentation.OpenLink(Documentation.Link.Donate);
        }

        private void OnClose(object sender, object e)
        {
            this.Close();
        }

        private void Hyperlink_OpenURL(object sender, RequestNavigateEventArgs e)
        {
            this.Close();
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
