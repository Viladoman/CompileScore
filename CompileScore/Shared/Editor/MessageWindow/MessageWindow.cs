using System.Windows;

namespace CompileScore
{
    public class MessageWindow : Window
    {
        public MessageWindow(MessageContent content)
        {
            Title = "Compile Score";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            this.Content = new MessageControl(this, content);
        }

        static public void Display(MessageContent content)
        {
            var messageWin = new MessageWindow(content);
            messageWin.ShowDialog();
        }
    }
}
