using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace CompileScore
{
    public class ParserSettingsPageGrid : DialogPage
    {
        [Category("Parser")]
        [DisplayName("Print Detailed Output")]
        [Description("Print the parser results from the command line in the Tool output pane")]
        public bool OptionParserShowDetailedCommandLine { set; get; } = false;
    }

}
