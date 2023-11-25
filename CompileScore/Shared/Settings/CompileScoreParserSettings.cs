using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace CompileScore
{
    public class ParserSettingsPageGrid : DialogPage
    {
        [Category("Parser")]
        [DisplayName("Print Command Line")]
        [Description("Print the command line argument passed to the parser in the Tool output pane")]
        public bool OptionParserShowCommandLine { set; get; } = true;
    }

}
