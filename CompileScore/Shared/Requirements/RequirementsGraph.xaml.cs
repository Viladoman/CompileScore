using CompileScore.Common;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CompileScore.Requirements
{
    public delegate void NotifyGraphNodeSelected(object graphNode);  // delegate ( root or node )

    public class RequirementGraphNode
    {
        public ParserFileRequirements Value { set; get; }

        public RequirementGraphNode ChildNode { set; get; }

        public CompileValue ProfilerValue { set; get; }

        public object IncluderValue { set; get; }

        public string Label { set; get; }

        public int Row { set; get; } = -1;
        public int Column { set; get; } = -1;

        

        //precomputed requirement icons
    }

    public class RequirementGraphRoot
    {
        public ParserUnit Value { get; set; }

        public List<RequirementGraphNode> Nodes { set; get; }

        public object ProfilerValue { set; get; }

        public string Label {  get; set; }

        public int MaxColumn { set; get; } = 0;
    }

    public static class RequirementGraphGenerator
    {
        public static RequirementGraphNode BuildGraphNode(ParserFileRequirements file)
        {
            RequirementGraphNode node = new RequirementGraphNode();
            node.Value = file;
            node.Label = GetFileNameSafe(file.Name);

            object profilerObject = CompilerData.Instance.SeekProfilerValueFromFullPath(file.Name);
            node.ProfilerValue = profilerObject as CompileValue;

            return node;
        }
        
        private static object GetIncluderData(object includer, CompileValue includee)
        {
            if (includer == null || includee == null)
                return null;

            int IncludeeIndex = CompilerData.Instance.GetIndexOf(CompilerData.CompileCategory.Include, includee);
            if (IncludeeIndex < 0)
                return null;

            if ( includer is UnitValue )
            {
                int includerIndex = CompilerData.Instance.GetIndexOf(includer as UnitValue);
                return Includers.CompilerIncluders.Instance.GetIncludeUnitValue(includerIndex, IncludeeIndex);
            }
            else if ( includer is CompileValue )
            {
                int includerIndex = CompilerData.Instance.GetIndexOf(CompilerData.CompileCategory.Include, includer as CompileValue);
                return Includers.CompilerIncluders.Instance.GetIncludeInclValue(includerIndex, IncludeeIndex);
            }

            return null;
        }

        public static RequirementGraphRoot BuildGraph(ParserUnit parserUnit)
        {
            if (parserUnit == null) return null;

            RequirementGraphRoot root = new RequirementGraphRoot();
            root.Value = parserUnit;
            root.Label = GetFileNameSafe(parserUnit.Filename);
            root.Nodes = new List<RequirementGraphNode>(parserUnit.DirectIncludes.Count);
            root.ProfilerValue = CompilerData.Instance.SeekProfilerValueFromFullPath(parserUnit.Filename);

            foreach (ParserFileRequirements file in parserUnit.DirectIncludes ?? Enumerable.Empty<ParserFileRequirements>())
            {
                int column = 0;

                RequirementGraphNode newNode = BuildGraphNode(file);
                newNode.Row = root.Nodes.Count;
                newNode.Column = column++;
                newNode.IncluderValue = GetIncluderData(root.ProfilerValue, newNode.ProfilerValue);

                RequirementGraphNode lastInclude = newNode;
                foreach (ParserFileRequirements indirectFile in file.Includes ?? Enumerable.Empty<ParserFileRequirements>())
                {
                    RequirementGraphNode indirectNode = BuildGraphNode(indirectFile);
                    indirectNode.Row = newNode.Row;
                    indirectNode.Column = column++;

                    lastInclude.ChildNode = indirectNode;
                    lastInclude = indirectNode;
                }

                root.MaxColumn = Math.Max(root.MaxColumn, column);

                root.Nodes.Add(newNode);
            }

            return root;
        }

        private static string GetFileNameSafe(string path)
        {
            string ret = EditorUtils.GetFileNameSafe(path);
            return ret == null ? "<Unknown>" : ret;
        }
    }

    public partial class RequirementsGraph : UserControl
    {
        private ToolTip tooltip = new ToolTip { Content = new RequirementsGraphTooltip(), Padding = new Thickness(0) };
        private DispatcherTimer tooltipTimer = new DispatcherTimer() { Interval = new TimeSpan(4000000) };

        const double CanvasPaddingY = 10.0;
        const double CanvasPaddingX = 5.0;
        const double RootWidth = 20.0;
        const double RootWidthSeparation = 10.0;
        const double NodeWidth = 200.0;
        const double NodeBaseHeight = 40.0;
        const double NodeProfilerHeight = 20.0;
        const double NodeWidthSeparation = 10.0;
        const double NodeHeightSeparation = 10.0;
        const double IndirectExtraSeparation = 20.0;

        private double NodeHeight = NodeBaseHeight;

        private double restoreScrollX = -1.0;
        private double restoreScrollY = -1.0;
        private Timeline.VisualHost baseVisual = new Timeline.VisualHost();
        private Timeline.VisualHost overlayVisual = new Timeline.VisualHost();
        private Brush overlayBrush = Brushes.White.Clone();
        private Brush activeBrush  = Brushes.White.Clone();
        private Pen borderPen = new Pen(Brushes.Black, 1);
        private Pen dashedPen = new Pen(Brushes.Black, 1);
        private Pen transparentPen = new Pen(Brushes.Transparent, 1);
        private Typeface Font = new Typeface("Verdana");

        private ParserUnit Unit { set; get; }
        private RequirementGraphRoot Root { set; get; }
        private object Hover { set; get; }
        private object Active { set; get; }

        public event NotifyGraphNodeSelected OnGraphNodeSelected;

        public RequirementsGraph()
        {
            InitializeComponent();

            this.DataContext = this;

            CompilerData compilerData = CompilerData.Instance;
            compilerData.Hydrate(CompilerData.HydrateFlag.Main);

            compilerData.ScoreDataChanged += OnScoreDataChanged;
            //TODO ~ ramonv ~ add parser data changed 

            dashedPen.DashStyle = DashStyles.Dash;
            overlayBrush.Opacity = 0.3;
            activeBrush.Opacity = 0.2;

            tooltipTimer.Tick += ShowTooltip;

            scrollViewer.Loaded += OnScrollViewerLoaded;
            scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
            scrollViewer.On2DMouseScroll += OnScrollView2DMouseScroll;
            scrollViewer.MouseMove += OnScrollViewerMouseMove;
            scrollViewer.MouseLeave += OnScrollViewerMouseLeave;
            scrollViewer.OnMouseLeftClick += OnScrollViewerMouseLeftClick;
            scrollViewer.MouseDoubleClick += OnScrollViewerDoubleClick;
            //scrollViewer.MouseRightButtonDown += OnScrollViewerContextMenu;
        }

        private void OnScoreDataChanged()
        {
            //Rebuild the graph with the new info
            ThreadHelper.ThrowIfNotOnUIThread();
            SetRoot(RequirementGraphGenerator.BuildGraph(Unit));
        }

        public void SetUnit(ParserUnit unit)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Unit = unit;
            SetRoot(RequirementGraphGenerator.BuildGraph(Unit));
        }

        private void SetRoot(RequirementGraphRoot root)
        {
            Root = root;

            SetActiveNode(null);

            restoreScrollX = -1.0;
            restoreScrollY = -1.0;
            SetupCanvas();
            RefreshAll();
        }

        private void SetHoverNode(object node)
        {
            if (node != Hover)
            {
                //Close Tooltip 
                tooltip.IsOpen = false;
                tooltipTimer.Stop();

                Hover = node;

                //Start Tooltip if applicable
                if (Hover != null)
                {
                    tooltipTimer.Start();
                }

                RenderOverlay();
            }
        }

        private void SetActiveNode(object node)
        {
            if (node != Active)
            {
                Active = node;

                OnGraphNodeSelected?.Invoke(Active);

                RenderOverlay();
            }
        }

        private void ShowTooltip(Object a, object b)
        {
            tooltipTimer.Stop();
            (tooltip.Content as RequirementsGraphTooltip).RootNode      = Root;
            (tooltip.Content as RequirementsGraphTooltip).ReferenceNode = Hover;
            tooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
            tooltip.IsOpen = true;
            tooltip.PlacementTarget = this;
        }

        private void SetupCanvas()
        {
            if (Root != null)
            {
                int numVisalCells = Math.Max(Root.Nodes.Count, 1); //At least one cell to show the root node

                NodeHeight = NodeBaseHeight + ( Root.ProfilerValue != null ? NodeProfilerHeight : 0 ); 

                double extraWidth = Root.MaxColumn > 0 ? IndirectExtraSeparation : 0; 
                canvas.Width = RootWidth + RootWidthSeparation + extraWidth + (Root.MaxColumn * (NodeWidth + NodeWidthSeparation) ) + 2 * CanvasPaddingX;
                canvas.Height = (numVisalCells * ( NodeHeight + NodeHeightSeparation ) ) + ( 2 * CanvasPaddingY ) - NodeHeightSeparation;

                if (restoreScrollX >= 0)
                {
                    scrollViewer.ScrollToHorizontalOffset(restoreScrollX);
                }

                if (restoreScrollY >= 0)
                {
                    scrollViewer.ScrollToVerticalOffset(restoreScrollY);
                }
            }
        }

        private void OnScrollViewerLoaded(object sender, RoutedEventArgs e)
        {
            //Fix the issue with the colored corner square
            ((Rectangle)scrollViewer.Template.FindName("Corner", scrollViewer)).Fill = scrollViewer.Background;

            SetupCanvas();
            RefreshAll();
        }

        private void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            restoreScrollX = scrollViewer.HorizontalOffset;
            restoreScrollY = scrollViewer.VerticalOffset;
            RefreshAll();
        }

        private void OnScrollView2DMouseScroll(object sender, Timeline.Mouse2DScrollEventArgs e)
        {
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta.X);
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta.Y);
        }

        private void OnScrollViewerMouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(canvas);
            SetHoverNode(Root == null ? null : GetElementAtPosition(p.X, p.Y));
        }

        private void OnScrollViewerMouseLeave(object sender, MouseEventArgs e)
        {
            SetHoverNode(null);
        }

        private void OnScrollViewerMouseLeftClick(object sender, MouseButtonEventArgs e)
        {
            if ( Root == null )
                return;

            Point p = e.GetPosition(canvas);
            SetActiveNode(Root == null ? null : GetElementAtPosition(p.X, p.Y));
        }

        private void OnScrollViewerDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Root == null)
                return;

            Point p = e.GetPosition(canvas);
            SetActiveNode(Root == null ? null : GetElementAtPosition(p.X, p.Y));
            OpenFileByNode(Active);
        }

        private void OpenFileByNode(object node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (node == null)
                return;
            
            if (node is RequirementGraphNode)
            {
                EditorUtils.OpenFileByName((node as RequirementGraphNode).Value.Name);
            }
            else if (node is RequirementGraphRoot)
            {
                EditorUtils.OpenFileByName((node as RequirementGraphRoot).Value.Filename);
            }
        }

        private void RefreshCanvasVisual(Timeline.VisualHost visual)
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
            if (Root == null)
            {
                //Clear the canvas
                using (DrawingContext drawingContext = baseVisual.Visual.RenderOpen()) { }
                RefreshCanvasVisual(baseVisual);
            }
            else
            {
                borderPen.Brush = Foreground;
                dashedPen.Brush = Foreground;

                using (DrawingContext drawingContext = baseVisual.Visual.RenderOpen())
                {
                    RenderRootNode(drawingContext, Root.ProfilerValue is UnitValue ? Common.Colors.ExecuteCompilerBrush : Common.Colors.IncludeBrush);

                    if ( Root.Nodes.Count > 0 )
                    {
                        //Get the Row and Columns we need to draw
                        int firstRow = GetRow(scrollViewer.VerticalOffset);
                        int lastRow = GetRow(scrollViewer.VerticalOffset + scrollViewer.ViewportHeight);

                        int firstColumn = GetColumn(scrollViewer.HorizontalOffset);
                        int lastColumn = GetColumn(scrollViewer.HorizontalOffset + scrollViewer.ViewportWidth);

                        for ( int row = firstRow; row <= lastRow && row < Root.Nodes.Count; ++row)
                        {
                            RenderNodeRow(drawingContext, Root.Nodes[row], firstColumn, lastColumn);
                        }
                    }
                }

                //force a canvas redraw
                RefreshCanvasVisual(baseVisual);
            }
        }

        private void RenderOverlayedNode(DrawingContext drawingContext, object node, Brush brush)
        {
            if (node != null)
            {
                if (node is RequirementGraphNode)
                {
                    RequirementGraphNode graphNode = node as RequirementGraphNode;
                    RenderNodeSingle(drawingContext, graphNode, GetColumnLocation(graphNode.Column), GetRowLocation(graphNode.Row), brush);
                }
                else if (Hover is RequirementGraphRoot)
                {
                    RenderRootNode(drawingContext, brush);
                }
            }
        }

        private void RenderOverlay()
        {
            using (DrawingContext drawingContext = overlayVisual.Visual.RenderOpen())
            {
                RenderOverlayedNode(drawingContext, Active, activeBrush);
                RenderOverlayedNode(drawingContext, Hover,  overlayBrush);
            }

            RefreshCanvasVisual(overlayVisual);
        }

        private double GetRowLocation(int row)
        {
            double initialOffset = CanvasPaddingY;
            double cellSize = NodeHeight + NodeHeightSeparation;
            return initialOffset + row * cellSize; 
        }

        private double GetColumnLocation(int column)
        {
            double initialOffset = CanvasPaddingX + RootWidth + RootWidthSeparation + (column > 0 ? IndirectExtraSeparation : 0);
            double cellSize = NodeWidth + NodeWidthSeparation;
            return initialOffset + column * cellSize;
        }

        private int GetColumn(double x)
        {
            double initialOffset = CanvasPaddingX + RootWidth + RootWidthSeparation;
            if (x < initialOffset)
                return -1;
            
            double initialIndirectOffset = initialOffset + NodeWidth + NodeWidthSeparation + IndirectExtraSeparation;
            if (x < initialIndirectOffset)
                return 0;

            return (int)((x - ( initialOffset + IndirectExtraSeparation ) ) / (NodeWidth + NodeWidthSeparation) );
        }

        private int GetRow(double y)
        {
            double initialOffset = CanvasPaddingY;
            double cellSize = NodeHeight + NodeHeightSeparation;
            return (int)((y - initialOffset) / cellSize); 
        }

        private object GetElementAtPosition(double x, double y)
        {
            RequirementGraphRoot foundRoot = GetRootNodeAtPosition(x, y); 
            if (foundRoot != null)
            {
                return foundRoot;
            }
            return GetGraphNodeAtPosition(x, y);
        }

        private RequirementGraphRoot GetRootNodeAtPosition(double x, double y)
        {
            double localX = x - CanvasPaddingX; 
            double localY = y - CanvasPaddingY;
            double rootHeight = canvas.Height - ((2.0 * CanvasPaddingY));
            return localX < 0 || localY < 0 || localX > RootWidth || localY > rootHeight ? null : Root;
        }

        private RequirementGraphNode GetGraphNodeAtPosition(double x, double y)
        {
            if (Root == null) 
                return null;

            int column = GetColumn(x);
            int row    = GetRow(y);
            if (row < 0 || column < 0 || row >= Root.Nodes.Count) 
                return null;

            double localX = x - GetColumnLocation(column);
            double localY = y - GetRowLocation(row);
            if ( localX > NodeWidth || localY > NodeHeight) 
                return null;

            RequirementGraphNode node = Root.Nodes[row];
            for( int col = 0; node != null && col < column; ++col )
            {
                node = node.ChildNode;
            }

            return node;
        }

        private void RenderNodeRow(DrawingContext drawingContext, RequirementGraphNode node, int firstColumn, int lastColumn)
        {
            double nodePositionY = GetRowLocation(node.Row);

            for (int column = 0; column <= lastColumn && node != null; ++column)
            {
                if ( column >= firstColumn )
                {
                    double nodePositionX = GetColumnLocation(node.Column);
                    RenderConnectingLine(drawingContext, node, nodePositionX, nodePositionY);
                    RenderNodeSingle(drawingContext, node, nodePositionX, nodePositionY, Common.Colors.InstantiateFuncBrush);
                }

                node = node.ChildNode;
            }
        }

        private void RenderConnectingLine(DrawingContext drawingContext, RequirementGraphNode node, double posX, double posY)
        {
            double lineHeight = NodeBaseHeight * 0.5;

            if (node.Column == 0)
            {
                drawingContext.DrawLine(borderPen, new Point(posX, posY + lineHeight), new Point(posX - RootWidthSeparation, posY + lineHeight));
            }
            else if (node.Column == 1)
            {
                double separation = RootWidthSeparation + IndirectExtraSeparation;
                drawingContext.DrawLine(dashedPen, new Point(posX, posY + lineHeight), new Point(posX - separation, posY + lineHeight));
            }
            else
            {
                drawingContext.DrawLine(dashedPen, new Point(posX, posY + lineHeight), new Point(posX - NodeWidthSeparation, posY + lineHeight));
            }
        }

        private void RenderNodeProfilerChunk(DrawingContext drawingContext, CompileValue value, double posX, double posY)
        {
            if ( value != null)
            {
                Brush severityColor = Common.Colors.GetSeverityBrush((uint)value.Severity);
                drawingContext.DrawRoundedRectangle(severityColor, transparentPen, new Rect(posX, posY+NodeBaseHeight, NodeWidth, NodeProfilerHeight), 5, 5);

                //MonikerProxy.DrawTo(drawingContext);

                //drawingContext.DrawImage(,);
            }
        }

        private void RenderNodeSingle(DrawingContext drawingContext, RequirementGraphNode node, double posX, double posY, Brush brush)
        {
            drawingContext.DrawRoundedRectangle(brush, borderPen, new Rect(posX, posY, NodeWidth, NodeHeight), 5, 5);

            //Render text
            var UIText = new FormattedText(node.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 12, Common.Colors.GetCategoryForeground(), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            UIText.MaxTextWidth = Math.Min(NodeWidth, UIText.Width);
            UIText.MaxTextHeight = NodeBaseHeight;

            double textPosX = posX + (NodeWidth - UIText.Width) * 0.5;
            double textPosY = posY + (NodeBaseHeight - UIText.Height) * 0.5;

            drawingContext.DrawText(UIText, new Point(textPosX, textPosY));

            RenderNodeProfilerChunk(drawingContext, node.ProfilerValue, posX, posY);
        }

        private void RenderRootNode(DrawingContext drawingContext, Brush brush)
        {
            double rootHeight = canvas.Height - ( ( 2.0 * CanvasPaddingY) );
            drawingContext.DrawRectangle(brush, borderPen, new Rect(CanvasPaddingX, CanvasPaddingY, RootWidth, rootHeight));
        }

    }
}
