using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CompileScore.Timeline
{
    public class Mouse2DScrollEventArgs
    {
        public Mouse2DScrollEventArgs(Vector delta) { Delta = delta; }

        public Vector Delta { get; }
    };

    public delegate void Mouse2DScrollEventHandler(object sender, Mouse2DScrollEventArgs e);

    public class CustomScrollViewer : ScrollViewer
    {
        private bool Is2DScolling { set; get; }
        private Point lastScrollingPosition { set; get; }

        public event MouseWheelEventHandler OnControlMouseWheel;
        public event Mouse2DScrollEventHandler On2DMouseScroll;

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                OnControlMouseWheel.Invoke(this,e);
            }
            else 
            { 
                //Default behavior
                base.OnMouseWheel(e); 
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            if (Mouse.MiddleButton == MouseButtonState.Pressed)
            {
                Is2DScolling = true;
                lastScrollingPosition = e.GetPosition(this);
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Is2DScolling = false;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        { 
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                Is2DScolling = true;
                lastScrollingPosition = e.GetPosition(this);
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released)
            {
                Is2DScolling = false;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (Is2DScolling)
            {
                Point nextPosition = e.GetPosition(this);
                On2DMouseScroll.Invoke(this, new Mouse2DScrollEventArgs(nextPosition - lastScrollingPosition));
                lastScrollingPosition = nextPosition;
            }
            base.OnMouseMove(e);
        }
    }

    public class VisualHost : UIElement
    {
        public DrawingVisual Visual = new DrawingVisual();

        public VisualHost()
        {
            IsHitTestVisible = false;
        }

        protected override int VisualChildrenCount
        {
            get { return Visual != null ? 1 : 0; }
        }

        protected override Visual GetVisualChild(int index)
        {
            return Visual;
        }
    }

    /// <summary>
    /// Interaction logic for Timeline.xaml
    /// </summary>
    public partial class Timeline : UserControl
    {
        const double NodeHeight = 20.0;
        const double zoomIncreaseRatio = 1.1;
        const double FakeWidth = 3;

        private double pixelToTimeRatio = 1.0;
        private bool zoomSliderLock = false; //used to avoid slider event feedback on itself 
        private VisualHost baseVisual = new VisualHost();
        private VisualHost overlayVisual = new VisualHost();
        private Brush overlayBrush = Brushes.White.Clone();
        private Pen borderPen = new Pen(Brushes.Black, 1);

        private ToolTip tooltip = new ToolTip { Content = new TimelineNodeTooltip() };

        private FullUnitValue Unit { set; get; }
        private TimelineNode Root { set; get; }
        private TimelineNode Hover { set; get; }

        public Timeline()
        {
            InitializeComponent();

            this.DataContext = this;

            CompilerData.Instance.ScoreDataChanged += OnDataChanged;

            overlayBrush.Opacity = 0.5;

            scrollViewer.Loaded += OnScrollViewerLoaded;
            scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
            scrollViewer.On2DMouseScroll += OnScrollView2DMouseScroll;
            scrollViewer.OnControlMouseWheel += OnScrollViewerControlMouseWheel;
            scrollViewer.MouseMove += OnScrollViewerMouseMove;
            scrollViewer.MouseLeave += OnScrollViewerMouseLeave;
            scrollViewer.MouseDoubleClick += OnScrollViewerDoubleClick;
            scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
            sliderZoom.ValueChanged += OnSliderZoomChanged;

        }

        public void SetUnit(FullUnitValue unit)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Unit = unit;
            SetRoot(CompilerTimeline.Instance.LoadTimeline(Unit));
        }             

        private void OnDataChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetUnit(Unit != null? CompilerData.Instance.GetUnitByName(Unit.Name) : null);
        }

        private void SetRoot(TimelineNode root)
        {
            Root = root;
            if (Root != null)
            {
                InitializeNodeRecursive(Root);
            }

            searchBox.SetData(Root);

            SetupCanvas(); //this should set up the zoom and scroll to the element we want by default full screen
            RefreshAll();
        }

        private void OnScrollViewerLoaded(object sender, RoutedEventArgs e)
        {
            SetupCanvas();
            RefreshAll();
        }

        private void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            RefreshAll();
        }

        private void OnScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Root != null)
            {
                double prevRatio = pixelToTimeRatio;
                pixelToTimeRatio = Math.Min(Math.Max(GetMinZoom(),pixelToTimeRatio),GetMaxZoom());

                if (prevRatio != pixelToTimeRatio)
                { 
                    canvas.Width = Root.Duration * pixelToTimeRatio;
                }

                RefreshZoomSlider();
            }
        }

        private void ApplyHorizontalZoom(double targetRatio, double anchorPosX)
        {
            double canvasPosX = anchorPosX + scrollViewer.HorizontalOffset;
            double realTimeOffset = canvasPosX / pixelToTimeRatio;
            double prevRatio = pixelToTimeRatio;

            pixelToTimeRatio = Math.Max(Math.Min(targetRatio, GetMaxZoom()), GetMinZoom());

            if (Root != null && prevRatio != pixelToTimeRatio)
            {
                double nextOffset = realTimeOffset * pixelToTimeRatio;
                double scrollOffset = nextOffset - anchorPosX;

                canvas.Width = Root.Duration * pixelToTimeRatio;
                scrollViewer.ScrollToHorizontalOffset(scrollOffset);
            }
        }           

        private void OnScrollViewerControlMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double targetRatio = e.Delta > 0 ? pixelToTimeRatio * zoomIncreaseRatio : pixelToTimeRatio / zoomIncreaseRatio;           
            double anchorPosX = e.GetPosition(scrollViewer).X;

            ApplyHorizontalZoom(targetRatio, anchorPosX);

            RefreshZoomSlider();
            e.Handled = true;
        }

        private void OnScrollView2DMouseScroll(object sender, Mouse2DScrollEventArgs e)
        {
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta.X);
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta.Y);
        }

        private void OnScrollViewerMouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(canvas);
            SetHoverNode(Root == null? null : GetNodeAtPosition(Root,PixelToTime(p.X),PixelToDepth(p.Y)));
        }

        private void OnScrollViewerMouseLeave(object sender, MouseEventArgs e)
        {
            SetHoverNode(null);
        }

        private void OnScrollViewerDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Root != null)
            {
                Point p = e.GetPosition(canvas);
                FocusNode(GetNodeAtPosition(Root, PixelToTime(p.X), PixelToDepth(p.Y)));
            }
        }

        private void SetHoverNode(TimelineNode node)
        {
            if (node != Hover)
            {
                Hover = node;

                //Tooltip control 
                (tooltip.Content as TimelineNodeTooltip).ReferenceNode = Hover;
                tooltip.IsOpen = false;
                if (Hover != null)
                {
                    tooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
                    tooltip.IsOpen = true;
                    tooltip.PlacementTarget = this;
                }
                RenderOverlay();
            }
        }

        private void FocusNode(TimelineNode node)
        { 
            if (node != null && Root != null)
            {
                const double margin = 10;

                double viewPortWidth = scrollViewer.ViewportWidth - 2 * margin;
                double zoom = node.Duration > 0 && viewPortWidth > 0? viewPortWidth / node.Duration : 0;
                pixelToTimeRatio = Math.Max(Math.Min(zoom, GetMaxZoom()), GetMinZoom());
                double scrollOffset = (node.Start * pixelToTimeRatio)-margin;

                canvas.Width = Root.Duration * pixelToTimeRatio;
                scrollViewer.ScrollToHorizontalOffset(scrollOffset);

                RefreshZoomSlider();
            }
        }

        private double GetMinZoom()
        {
            return Root != null && Root.Duration > 0 && scrollViewer.ViewportWidth > 0 ? scrollViewer.ViewportWidth/ Root.Duration : 1;
        }
        
        private double GetMaxZoom()
        {
            return 5.0;
        }

        private double PixelToTime(double pixels) { return pixels / pixelToTimeRatio; }

        private double TimeToPixel(double time) { return time * pixelToTimeRatio; }

        private uint PixelToDepth(double pixels) { return (uint)(pixels/NodeHeight); }
        private double DepthToPixel(uint depth) { return depth * NodeHeight;  }

        private void SetupCanvas()
        {
            if (Root != null)
            {
                pixelToTimeRatio = GetMinZoom();
                canvas.Width = Root.Duration*pixelToTimeRatio;
                canvas.Height = (Root.MaxDepthLevel+1) * NodeHeight;

                RefreshZoomSlider();
            }
        }

        private void InitializeNodeRecursive(TimelineNode node)
        {
            node.DepthLevel = node.Parent == null ? 0 : node.Parent.DepthLevel+1;
            node.MaxDepthLevel = node.DepthLevel;

            node.UIColor = Brushes.Aquamarine; //TODO ~ ramonv define color based on category

            foreach (TimelineNode child in node.Children)
            {
                InitializeNodeRecursive(child);
                node.MaxDepthLevel = Math.Max(node.MaxDepthLevel, child.MaxDepthLevel);
            }
        }

        private void RenderNodeRecursive(DrawingContext drawingContext, TimelineNode node, double clipTimeStart, double clipTimeEnd, double clipDepth, double fakeDurationThreshold)
        {
            //Clipping and LODs
            if (node.Start > clipTimeEnd || (node.Start + node.Duration) < clipTimeStart || node.DepthLevel > clipDepth)
            {
                return;
            }
            else if (node.Duration < fakeDurationThreshold)
            {
                RenderFake(drawingContext, node);
            }
            else
            {
                RenderNodeSingle(drawingContext, node, node.UIColor, clipTimeStart, clipTimeEnd);

                foreach (TimelineNode child in node.Children)
                {
                    RenderNodeRecursive(drawingContext, child, clipTimeStart, clipTimeEnd, clipDepth, fakeDurationThreshold);
                }
            }

        }

        private void RenderFake(DrawingContext drawingContext, TimelineNode node)
        {
            double posX = TimeToPixel(node.Start);
            double width = TimeToPixel(node.Duration);
            double posY = DepthToPixel(node.DepthLevel);

            var brush = Brushes.Black;
            drawingContext.DrawRectangle(brush, null, new Rect(posX, posY, width, NodeHeight * (1 + node.MaxDepthLevel - node.DepthLevel)));
        }

        private void RenderNodeSingle(DrawingContext drawingContext, TimelineNode node, Brush brush, double clipTimeStart, double clipTimeEnd)
        {
            double posY        = DepthToPixel(node.DepthLevel);
            double pixelStart  = TimeToPixel(Math.Max(clipTimeStart, node.Start));
            double pixelEnd    = TimeToPixel(Math.Min(clipTimeEnd, node.Start + node.Duration));
            double screenWidth = pixelEnd - pixelStart;

            drawingContext.DrawRectangle(brush, borderPen, new Rect(pixelStart, posY, screenWidth, NodeHeight));

            //Render text
            if (screenWidth > 30)
            {
                var UIText = new FormattedText(node.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                UIText.MaxTextWidth = Math.Min(screenWidth, UIText.Width);
                UIText.MaxTextHeight = NodeHeight;

                double textPosX = (pixelEnd + pixelStart - UIText.Width) * 0.5;
                double textPosY = posY + (NodeHeight - UIText.Height) * 0.5;

                drawingContext.DrawText(UIText, new Point(textPosX, textPosY));

            }
        }

        private void RefreshCanvasVisual(VisualHost visual)
        {
            canvas.Children.Remove(visual);
            canvas.Children.Add(visual);
        }

        private void RefreshAll()
        {
            RenderBase();
            RenderOverlay();
        }

        private void RenderBase()
        {
            if (Root != null)
            {
                double clipTimeStart = PixelToTime(scrollViewer.HorizontalOffset);
                double clipTimeEnd   = PixelToTime(scrollViewer.HorizontalOffset + scrollViewer.ViewportWidth);
                uint   clipDepth     = PixelToDepth(scrollViewer.VerticalOffset + scrollViewer.ViewportHeight);
                double fakeDurationThreshold = PixelToTime(FakeWidth);

                //Setup 
                using (DrawingContext drawingContext = baseVisual.Visual.RenderOpen())
                {
                    RenderNodeRecursive(drawingContext, Root, clipTimeStart, clipTimeEnd, clipDepth, fakeDurationThreshold);
                }

                //force a canvas redraw
                RefreshCanvasVisual(baseVisual);
            }
        }

        private void RenderOverlay()
        {
            using (DrawingContext drawingContext = overlayVisual.Visual.RenderOpen())
            {
                double clipTimeStart = PixelToTime(scrollViewer.HorizontalOffset);
                double clipTimeEnd = PixelToTime(scrollViewer.HorizontalOffset + scrollViewer.ViewportWidth);

                //perform clipping here
                if (Hover != null && Hover.Start < clipTimeEnd && (Hover.Start + Hover.Duration) > clipTimeStart)
                {
                    RenderNodeSingle(drawingContext, Hover, overlayBrush, clipTimeStart, clipTimeEnd);
                }
            }

            RefreshCanvasVisual(overlayVisual);
        }

        private TimelineNode GetNodeAtPosition(TimelineNode node, double time, uint depth)
        {
            if (time >= node.Start && time <= (node.Start+node.Duration))
            {
                if (depth == node.DepthLevel)
                {
                    return node;
                }
                else
                {
                    foreach (TimelineNode child in node.Children)
                    {
                        TimelineNode found = GetNodeAtPosition(child, time, depth);
                        if (found != null) return found;                        
                    }
                }
            }

            return null;
        }

        private void RefreshZoomSlider()
        {
            sliderZoom.Minimum = 0;
            sliderZoom.Maximum = 1;

            double minZoom = Math.Log(GetMinZoom());
            double maxZoom = Math.Log(GetMaxZoom());
            double range = maxZoom - minZoom;
            double current = Math.Log(pixelToTimeRatio);

            zoomSliderLock = true;
            sliderZoom.Value = range != 0? (current-minZoom)/ range : 0;
            zoomSliderLock = false;
        }

        private void OnSliderZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!zoomSliderLock)
            {               
                double minZoom = Math.Log(GetMinZoom());
                double maxZoom = Math.Log(GetMaxZoom());
                double targetRatio = Math.Exp(e.NewValue * (maxZoom - minZoom) + minZoom);
                double anchorPosX = scrollViewer.ViewportWidth * 0.5;
                ApplyHorizontalZoom(targetRatio, anchorPosX);
            }
            
        }
    }
}
