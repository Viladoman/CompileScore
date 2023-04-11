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
using System.Windows.Threading;

namespace CompileScore
{
    internal sealed class TextEditorAdornment
    {
        //private const float AdornmentSize = 18;
        private const float AdornmentSize = 15;

        private readonly IAdornmentLayer adornmentLayer;
        private readonly IWpfTextView view;

        private Button Element { set; get; }
        private readonly string fullPath;

        private object Reference { set; get; }
        private DispatcherTimer tooltipTimer = new DispatcherTimer() { Interval = new TimeSpan(4000000) };
        private ToolTip tooltip = new ToolTip { Content = new TextEditorAdornmentTooltip(), Padding = new Thickness(0) };

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

            RefreshReference();
            RefreshEnabled();
            RefreshButtonIcon();

            view.LayoutChanged += OnSizeChanged;
            CompilerData.Instance.ScoreDataChanged += OnDataChanged;
            CompilerData.Instance.AdornmentModeChanged += OnEnabledChanged;

            tooltipTimer.Tick += ShowTooltip;
        }

        private void RefreshReference()
        {
            Reference = EditorUtils.SeekObjectFromFullPath(fullPath);
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
            baseButton.MouseEnter += OnMouseEnter;
            baseButton.MouseLeave += OnMouseLeave;

            Element = baseButton;
        }

        private void OnMouseEnter(object sender, object evt)
        {
            if(Element != null)
            {
                tooltipTimer.Start();
            }
        }

        private void OnMouseLeave(object sender, object evt)
        {
            HideTooltip();
        }

        private void ShowTooltip(Object a, object b)
        {
            tooltipTimer.Stop();
            (tooltip.Content as TextEditorAdornmentTooltip).Reference = Reference;
            tooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
            tooltip.PlacementTarget = Element;
            tooltip.IsOpen = true;
        }

        private void HideTooltip()
        {
            tooltipTimer.Stop();
            tooltip.IsOpen = false;
        }

        private void RefreshButtonIcon()
        {
            if (Element != null )
            {
                //TODO ~ ramonv ~ create an array of static icons for each severity

                Image icon = new Image
                {
                    Source = new BitmapImage(new Uri(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"Resources","IconDetail.png"))),
                    Width = AdornmentSize,
                    Height = AdornmentSize,
                };
                Element.Content = icon;
            }
        }

        private void Adornment_OnClick(object sender, object evt)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            UnitValue unit = Reference as UnitValue;
            CompileValue value = Reference as CompileValue;

            if (unit == null && value == null)
                return;

            HideTooltip();

            if ( unit != null )
            {
                Timeline.CompilerTimeline.Instance.DisplayTimeline(unit);
            }
            
            if ( value != null )
            {
                System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value)));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Self Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.SelfMaxUnit, value)));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Show Includers Graph", (a, b) => Includers.CompilerIncluders.Instance.DisplayIncluders(value)));
                contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition, System.Windows.Forms.ToolStripDropDownDirection.AboveLeft);
            }

        }

        private void OnSizeChanged(object sender, EventArgs e)
        {
            RefreshButtonPresence();
        }

        private void OnDataChanged()
        {
            RefreshReference();
            RefreshEnabled();
            RefreshButtonIcon();
            RefreshButtonPresence();
            
            if (tooltip.IsOpen)
            {
                (tooltip.Content as TextEditorAdornmentTooltip).Reference = Reference;
            }
        }

        private void OnEnabledChanged()
        {
            RefreshEnabled();
            RefreshButtonIcon();
            RefreshButtonPresence();
        }
        
        bool RefreshEnabled()
        {
            bool wasVisible = Element != null;
            GeneralSettingsPageGrid settings = CompilerData.Instance.GetGeneralSettings();
            bool isEnabled = settings != null && settings.OptionTextEditorAdornment != GeneralSettingsPageGrid.AdornmentMode.Disabled;

            if (isEnabled)
            {
                CompilerData.Instance.Hydrate(CompilerData.HydrateFlag.Main);
            }

            bool shouldBeVisible = isEnabled && Reference != null;
            if (wasVisible != shouldBeVisible)
            {
                if ( shouldBeVisible )
                {
                    CreateButton();
                }
                else
                {
                    Element = null;
                    HideTooltip();
                }
                return true;
            }

            return false;
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
