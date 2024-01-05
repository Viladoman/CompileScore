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

        private Grid Element { set; get; }
        private readonly string fullPath;

        private object Reference { set; get; }
        private DispatcherTimer tooltipTimer = new DispatcherTimer() { Interval = new TimeSpan(4000000) };
        private ToolTip tooltip = new ToolTip { Content = new TextEditorAdornmentTooltip(), Padding = new Thickness(0) };

        public TextEditorAdornment(IWpfTextView view)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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
            Reference = CompilerData.Instance.SeekProfilerValueFromFullPath(fullPath);
        }

        private void CreateUI()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Grid grid = new Grid();

            grid.Width = AdornmentSize;
            grid.Height = AdornmentSize;
            grid.Background = Brushes.Transparent;
            grid.Cursor = System.Windows.Input.Cursors.Arrow;

            grid.PreviewMouseLeftButtonDown += (sender, e) => { ThreadHelper.ThrowIfNotOnUIThread(); e.Handled = true; Adornment_OnClick(sender,e); };
            grid.MouseEnter += OnMouseEnter;
            grid.MouseLeave += OnMouseLeave;

            Element = grid;
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

                Element.Children.Clear();
                Element.Children.Add(icon); 
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
                System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Open Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(unit) ));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Show Requirements Graph", (a, b) => ParserData.DisplayRequirements(CompilerData.Instance.Folders.GetUnitPathSafe(unit))));
                contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition, System.Windows.Forms.ToolStripDropDownDirection.AboveLeft);
            }
            
            if ( value != null )
            {
                System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value)));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Locate Max Self Timeline", (a, b) => Timeline.CompilerTimeline.Instance.DisplayTimeline(value.SelfMaxUnit, value)));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Show Includers Graph", (a, b) => Includers.CompilerIncluders.Instance.DisplayIncluders(value)));
                contextMenuStrip.Items.Add(Common.UIHelpers.CreateContextItem("Show Requirements Graph", (a, b) => ParserData.DisplayRequirements(CompilerData.Instance.Folders.GetValuePathSafe(value))));
                contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition, System.Windows.Forms.ToolStripDropDownDirection.AboveLeft);
            }

        }

        private void OnSizeChanged(object sender, EventArgs e)
        {
            RefreshButtonPresence();
        }

        private void OnDataChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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
            ThreadHelper.ThrowIfNotOnUIThread();

            RefreshEnabled();
            RefreshButtonIcon();
            RefreshButtonPresence();
        }
        
        bool RefreshEnabled()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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
                    CreateUI();
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
