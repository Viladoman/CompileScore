using System.Windows;
using System.Windows.Controls;

namespace CompileScore
{
    public class MessageContent
    {
        public MessageContent(){}

        public MessageContent(string message)
        {
            Message = message;
        }

        public string Message { set; get; }
        public string Log { set; get; }
        public Documentation.Link Doc { set; get; } = Documentation.Link.None;
        public bool ShowOptions { set; get; } = false;
    }

    public partial class MessageControl : UserControl
    {
        private Window ParentWindow { set; get; }
        private MessageContent MsgContent { set; get; }

        public MessageControl(Window parentWindow, MessageContent msgContent)
        {
            InitializeComponent();

            ParentWindow = parentWindow;
            MsgContent = msgContent;
            ShowMessage(); 
            ShowLog();
            ShowOptionsButton();
            ShowDocumentationButton();
        }

        private void ShowMessage()
        {
            mainContent.Children.Clear();
            if (MsgContent.Message != null && MsgContent.Message.Length > 0)
            {
                mainContent.Children.Add(CreateBasicText(MsgContent.Message));    
            }
        }

        private void ShowLog()
        {
            if (MsgContent.Log == null || MsgContent.Log.Length == 0)
            {
                logExpander.Visibility = Visibility.Collapsed;
                onlyButtons.Visibility = Visibility.Visible;
            }
            else
            {
                logExpander.Visibility = Visibility.Visible;
                onlyButtons.Visibility = Visibility.Collapsed;

                logText.Text = TruncateLongString(MsgContent.Log, 4000);
            }
        }

        private void ShowOptionsButton()
        {
            if (MsgContent.ShowOptions)
            {
                buttonOptionsA.Visibility = Visibility.Visible; 
                buttonOptionsB.Visibility = Visibility.Visible;
                buttonAcceptB.IsDefault = false;
                buttonAcceptB.IsCancel = false;
                buttonAcceptA.IsDefault = true;
                buttonAcceptA.IsCancel = true;
            } 
            else
            {
                buttonOptionsA.Visibility = Visibility.Collapsed;
                buttonOptionsB.Visibility = Visibility.Collapsed;
                buttonAcceptA.IsDefault = false;
                buttonAcceptA.IsCancel = false;
                buttonAcceptB.IsDefault = true;
                buttonAcceptB.IsCancel = true;
            }
        }

        private void ShowDocumentationButton()
        {
            if (MsgContent.Doc == Documentation.Link.None)
            {
                buttonDocA.Visibility = Visibility.Collapsed;
                buttonDocB.Visibility = Visibility.Collapsed;
            }
            else
            {
                buttonDocA.Visibility = Visibility.Visible;
                buttonDocB.Visibility = Visibility.Visible;
            }
        }

        private string TruncateLongString(string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
            {
                return str;
            }
            return str.Substring(0, maxLength) + "...";
        }

        private TextBlock CreateBasicText(string str)
        {
            var ret = new TextBlock();
            ret.Text = str;
            return ret; 
        }

        private void CloseWindow(object a = null, object b = null)
        { 
            ParentWindow.Close();
        }

        private void OpenSettings(object a = null, object b = null)
        {
            ParentWindow.Close();
            SettingsManager.Instance.OpenSettingsWindow();
        }

        private void OpenDocumentation(object a = null, object b = null)
        {
            ParentWindow.Close();
            Documentation.OpenLink(MsgContent.Doc);
        }
    }
}
