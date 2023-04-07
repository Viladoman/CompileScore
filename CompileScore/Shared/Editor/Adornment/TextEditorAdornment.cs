using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
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

        private readonly UIElement adorment;
        private readonly string fullPath;


        public TextEditorAdornment(IWpfTextView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            this.view = view;

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
            adorment = new Image
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

            

            adornmentLayer = view.GetAdornmentLayer("TextEditorAdornment");

            view.LayoutChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, EventArgs e)
        {
            adornmentLayer.RemoveAllAdornments();

            Canvas.SetLeft(adorment, view.ViewportRight - AdornmentSize);
            Canvas.SetTop(adorment, view.ViewportBottom - AdornmentSize);
            adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, adorment, null);
        }
    }
}
