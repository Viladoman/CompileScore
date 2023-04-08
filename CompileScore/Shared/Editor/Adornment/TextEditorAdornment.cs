using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CompileScore
{
    internal sealed class TextEditorAdornment
    {
        private const float AdornmentSize = 18;

        private readonly IAdornmentLayer adornmentLayer;
        private readonly IWpfTextView view;

        private Button Element { set; get; }
        private readonly string fullPath;


        public TextEditorAdornment(IWpfTextView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            this.view = view;
            adornmentLayer = view.GetAdornmentLayer("TextEditorAdornment");

            fullPath = null;
            ITextDocument document = null;
            if (view.TextDataModel.DocumentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document))
            {
                fullPath = document.FilePath;
            }

            //TODO ~ ramonv ~ build something that can match the best similar path ( find all instances with the same name and then tries to match as many folders as possible )
            //TODO ~ blend this together with the show timeline code

            //TODO ~ create the proper UIElement

            //TODO ~ create an array of static icons for each severity


            //adorment.Tooltip = new ToolTip { Content = new Timeline.TimelineNodeTooltip(), Padding = new Thickness(0) }; ;

            //Tooltip needs to be custom...

            CreateButton();
            RefreshButtonIcon();

            view.LayoutChanged += OnSizeChanged;
        }

        private void CreateButton()
        {
            Button baseButton = new Button();
            baseButton.Width = AdornmentSize;
            baseButton.Height = AdornmentSize;
            baseButton.BorderThickness = new Thickness(0);
            baseButton.Padding = new Thickness(0);
            baseButton.Background = Brushes.Transparent;
            baseButton.Cursor = System.Windows.Input.Cursors.Arrow;
            baseButton.Click += Adornment_OnClick;

            //TODO ~ ramonv ~ setup tooltip in a similar way as the timeline tooltip here

            Element = baseButton;
        }

        private void RefreshButtonIcon()
        {
            if (Element != null )
            {
                Image icon = new Image
                {
                    // Get the image path from within the packaged extension
                    Source = new BitmapImage(
                     new Uri(
                         Path.Combine(
                             Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                             "Resources",
                             "IconDetail.png"))),
                    Width = AdornmentSize,
                    Height = AdornmentSize,
                };
                Element.Content = icon;
            }
        }

        private void Adornment_OnClick(object sender, object evt)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //CompileValue value = GetValueFromRowItem(row);
            //if (value == null) return;

            System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Placeholder A", (a, b) => Timeline.CompilerTimeline.Instance.FocusTimelineWindow()));
            contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Placeholder B", (a, b) => Timeline.CompilerTimeline.Instance.FocusTimelineWindow()));

            //TODO ~ add more options 

            contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition, System.Windows.Forms.ToolStripDropDownDirection.AboveLeft);
        }

        private void OnSizeChanged(object sender, EventArgs e)
        {
            RefreshButtonPresence();
        }

        private void RefreshButtonPresence()
        {
            adornmentLayer.RemoveAllAdornments();

            if (Element != null )
            {
                Canvas.SetLeft(Element, view.ViewportRight - AdornmentSize);
                Canvas.SetTop(Element, view.ViewportBottom - AdornmentSize);
                adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, Element, null);
            }    
        }
    }
}
