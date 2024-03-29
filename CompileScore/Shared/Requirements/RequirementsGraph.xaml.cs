﻿using CompileScore.Common;
using CompileScore.Timeline;
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

        public List<RequirementGraphNode> PreNodes { set; get; }

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

            root.PreNodes = new List<RequirementGraphNode>(parserUnit.PreIncludes.Count);
            int preColumn = 0;
            foreach (ParserFileRequirements file in parserUnit.PreIncludes ?? Enumerable.Empty<ParserFileRequirements>())
            {
                RequirementGraphNode newNode = BuildGraphNode(file);
                newNode.Column = preColumn++;
                root.PreNodes.Add(newNode);
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

        const double PreAreaPaddingX = 10.0;
        const double PreAreaPaddingY = 10.0;
        const double PreAreaSpacingY = 10.0;

        const double RootWidth = 20.0;
        const double RootWidthSeparation = 10.0;
        const double NodeWidth = 200.0;
        const double NodeBaseHeight = 25.0;
        const double NodeProfilerHeight = 10.0;
        const double NodeWidthSeparation = 10.0;
        const double NodeHeightSeparation = 10.0;
        const double IndirectExtraSeparation = 20.0;
        const byte   SeverityBrushOpacity = 125;

        private double NodeHeight = NodeBaseHeight;
        private double PreAreaHeight = 0;

        private double restoreScrollX = -1.0;
        private double restoreScrollY = -1.0;
        private Timeline.VisualHost baseVisual = new Timeline.VisualHost();
        private Timeline.VisualHost overlayVisual = new Timeline.VisualHost();
        private Brush overlayBrush = Brushes.White.Clone();
        private Brush activeBrush  = Brushes.White.Clone();
        private Pen borderPen = new Pen(Brushes.Black, 1);
        private Pen transparentPen = new Pen(Brushes.Transparent, 1);
        private Pen dashedPen = new Pen(Brushes.Black, 1);
        private Pen selectedPen = new Pen(Brushes.Black, 3);
        private Typeface Font = new Typeface("Verdana");

        private static Brush[] SeverityBrushes { set; get; } = new Brush[6];

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

            MonikerProxy.Dpi = (int)VisualTreeHelper.GetDpi(this).PixelsPerInchX;

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
            scrollViewer.MouseRightButtonDown += OnScrollViewerContextMenu;

            ParserData.Instance.ThemeChanged += RefreshAll;
            CompilerData.Instance.ThemeChanged += RefreshAll;
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
            ThreadHelper.ThrowIfNotOnUIThread();

            Root = root;

            SetActiveNode(null);

            restoreScrollX = -1.0;
            restoreScrollY = -1.0;
            SetupCanvas();
            RefreshAll();
        }

        private void SetHoverNode(object node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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
            ThreadHelper.ThrowIfNotOnUIThread();

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

                PreAreaHeight = Root.PreNodes.Count > 0? (NodeHeight + (2 * PreAreaPaddingY) + PreAreaSpacingY) : 0;

                double extraWidth = Root.MaxColumn > 0 ? IndirectExtraSeparation : 0; 
                double mainWidth = RootWidth + RootWidthSeparation + extraWidth + (Root.MaxColumn * (NodeWidth + NodeWidthSeparation) ) + 2 * CanvasPaddingX;
                double mainHeight = (numVisalCells * ( NodeHeight + NodeHeightSeparation ) ) + ( 2 * CanvasPaddingY ) - NodeHeightSeparation;

                double preWidth = Root.PreNodes.Count * (NodeWidth + NodeWidthSeparation) + 2 * (CanvasPaddingX + PreAreaPaddingX);

                canvas.Width = Math.Max(mainWidth, preWidth);
                canvas.Height = mainHeight + PreAreaHeight;

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
            ThreadHelper.ThrowIfNotOnUIThread();

            //Fix the issue with the colored corner square
            ((Rectangle)scrollViewer.Template.FindName("Corner", scrollViewer)).Fill = scrollViewer.Background;

            SetupCanvas();
            RefreshAll();
        }

        private void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            restoreScrollX = scrollViewer.HorizontalOffset;
            restoreScrollY = scrollViewer.VerticalOffset;
        }

        private void OnScrollView2DMouseScroll(object sender, Timeline.Mouse2DScrollEventArgs e)
        {
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta.X);
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta.Y);
        }

        private void OnScrollViewerMouseMove(object sender, MouseEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Point p = e.GetPosition(canvas);
            SetHoverNode(Root == null ? null : GetElementAtPosition(p.X, p.Y));
        }

        private void OnScrollViewerMouseLeave(object sender, MouseEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetHoverNode(null);
        }

        private void OnScrollViewerMouseLeftClick(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if ( Root == null )
                return;

            Point p = e.GetPosition(canvas);
            SetActiveNode(Root == null ? null : GetElementAtPosition(p.X, p.Y));
        }

        private void OnScrollViewerDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Root == null || e.ChangedButton != MouseButton.Left)
                return;

            Point p = e.GetPosition(canvas);
            SetActiveNode(Root == null ? null : GetElementAtPosition(p.X, p.Y));
            OpenFileByNode(Active);
        }

        private void OnScrollViewerContextMenu(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Hover != null)
            {
                CreateContextualMenu(Hover);
            }
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
            ThreadHelper.ThrowIfNotOnUIThread();

            Array.Clear(SeverityBrushes, 0, SeverityBrushes.Length);

            RenderBase();
            RenderOverlay();
        }

        private void RenderBase()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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

                    if ( Root.PreNodes.Count > 0 )
                    {
                        RenderPeeBackground(drawingContext);

                        foreach (RequirementGraphNode node in Root.PreNodes)
                        {
                            RenderPreNode(drawingContext, node);
                        }
                    }

                    RenderRootNode(drawingContext, Root.ProfilerValue is UnitValue ? Common.Colors.ExecuteCompilerBrush : Common.Colors.IncludeBrush, borderPen);

                    for ( int row = 0; row < Root.Nodes.Count; ++row)
                    {
                        RenderNodeRow(drawingContext, Root.Nodes[row]);
                    }
                }

                //force a canvas redraw
                RefreshCanvasVisual(baseVisual);
            }
        }

        private void RenderPeeBackground(DrawingContext drawingContext)
        {
            double bgWidth  = (PreAreaPaddingX * 2) + (Root.PreNodes.Count * ( NodeWidth + NodeWidthSeparation ) );
            double bgHeight = (PreAreaPaddingY * 2) + NodeHeight;
            drawingContext.DrawRoundedRectangle(Brushes.Transparent, dashedPen, new Rect(CanvasPaddingX, CanvasPaddingY, bgWidth, bgHeight), 10,10);
        }

        private void RenderOverlayedNode(DrawingContext drawingContext, object node, Brush brush, Pen pen)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (node != null)
            {
                if (node is RequirementGraphNode)
                {
                    RequirementGraphNode graphNode = node as RequirementGraphNode;
                    
                    double x,y;
                    GetLocation(out x,out y,graphNode.Row, graphNode.Column);  
                    RenderOverlayedNodeGraph(drawingContext, graphNode, x, y, brush, pen);
                }
                else if (Hover is RequirementGraphRoot)
                {
                    RenderRootNode(drawingContext, brush, pen);
                }
            }
        }

        private void RenderOverlay()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            using (DrawingContext drawingContext = overlayVisual.Visual.RenderOpen())
            {
                selectedPen.Brush = this.Foreground;

                RenderOverlayedNode(drawingContext, Active, activeBrush, selectedPen);
                RenderOverlayedNode(drawingContext, Hover,  overlayBrush, borderPen);
            }

            RefreshCanvasVisual(overlayVisual);
        }

        private void GetLocation(out double x, out double y, int row, int column)
        {
            double cellSizeX = NodeWidth  + NodeWidthSeparation;
            double cellSizeY = NodeHeight + NodeHeightSeparation;

            if ( row < 0 )
            {
                double initialOffsetX = CanvasPaddingX + PreAreaPaddingX;
                double initialOffsetY = CanvasPaddingY + PreAreaPaddingY;

                x = initialOffsetX + column * cellSizeX;
                y = initialOffsetY;
            }
            else
            {
                double initialOffsetX = CanvasPaddingX + RootWidth + RootWidthSeparation + (column > 0 ? IndirectExtraSeparation : 0);
                double initialOffsetY = CanvasPaddingY + PreAreaHeight;

                x = initialOffsetX + column * cellSizeX;
                y = initialOffsetY + row    * cellSizeY;
            }
        }

        private bool GetGridLocation(out int row, out int column, double x, double y)
        {
            row = -1;
            column = -1; 

            if ( y <= CanvasPaddingY)
            {
                //in top padding
                return false;
            }

            double cellSizeX = NodeWidth + NodeWidthSeparation;
            double cellSizeY = NodeHeight + NodeHeightSeparation;

            if ( y < PreAreaHeight )
            {
                double initialOffsetX = CanvasPaddingX + PreAreaPaddingX;
                column = (int)((x - (initialOffsetX + IndirectExtraSeparation)) / cellSizeX);
                row = -1;
            }
            else
            {
                //Column
                double initialOffsetX = CanvasPaddingX + RootWidth + RootWidthSeparation;
                if (x < initialOffsetX)
                {
                    return false;
                }          

                double initialIndirectOffsetX = initialOffsetX + cellSizeX + IndirectExtraSeparation;
                if (x < initialIndirectOffsetX)
                {
                    column = 0;
                }
                else
                {
                    column = (int)((x - (initialOffsetX + IndirectExtraSeparation)) / cellSizeX); 
                }

                //Row
                double initialOffsetY = CanvasPaddingY + PreAreaHeight;
                row = (int)((y - initialOffsetY) / cellSizeY);
            }

            return true;
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
            double localY = y - (CanvasPaddingY + PreAreaHeight);
            double rootHeight = canvas.Height - ((2.0 * CanvasPaddingY));
            return localX < 0 || localY < 0 || localX > RootWidth || localY > rootHeight ? null : Root;
        }

        private RequirementGraphNode GetGraphNodeAtPosition(double x, double y)
        {
            if (Root == null) 
                return null;

            int row,column;
            bool valid = GetGridLocation(out row, out column, x, y);

            if (!valid || column < 0 || (row >= 0 && row >= Root.Nodes.Count) || (row < 0 && Root.PreNodes.Count == 0) )
                return null;

            double gridX, gridY;
            GetLocation(out gridX, out gridY, row, column);

            double localX = x - gridX;
            double localY = y - gridY;
            if ( localX > NodeWidth || localY > NodeHeight) 
                return null;

            RequirementGraphNode node = null;
            if ( row < 0 )
            {
                return column < Root.PreNodes.Count? Root.PreNodes[column] : null;
            }
            else
            {
                node = Root.Nodes[row];
                for( int col = 0; node != null && col < column; ++col )
                {
                    node = node.ChildNode;
                }
            }

            return node;
        }

        private void RenderPreNode(DrawingContext drawingContext, RequirementGraphNode node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            double nodePositionX;
            double nodePositionY;
            GetLocation(out nodePositionX, out nodePositionY, node.Row, node.Column);
            RenderNodeSingle(drawingContext, node, nodePositionX, nodePositionY);
        }

        private void RenderNodeRow(DrawingContext drawingContext, RequirementGraphNode node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            while (node != null)
            {
                double nodePositionX;
                double nodePositionY;
                GetLocation(out nodePositionX, out nodePositionY, node.Row, node.Column);

                RenderConnectingLine(drawingContext, node, nodePositionX, nodePositionY);
                RenderNodeSingle(drawingContext, node, nodePositionX, nodePositionY);
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

        private static Brush GetSeverityBrush(float severity)
        {
            uint severityIndex = (uint)severity; 
            
            if ( severityIndex < SeverityBrushes.Length)
            {
                if (SeverityBrushes[severityIndex] == null)
                {
                    Color severityColor = Common.Colors.GetSeverityColor(severityIndex);
                    SeverityBrushes[severityIndex] = new SolidColorBrush(Color.FromArgb(SeverityBrushOpacity, severityColor.R, severityColor.G, severityColor.B));
                }

                return SeverityBrushes[severityIndex];
            }

            return Common.Colors.OtherBrush;
        }

        private void RenderNodeProfilerChunk(DrawingContext drawingContext, CompileValue value, double posX, double posY, Pen pen)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if ( value != null)
            {
                double profilerWidth = NodeWidth - 10;

                drawingContext.DrawRectangle(this.Background, pen, new Rect(posX + 5, posY + NodeBaseHeight, profilerWidth, NodeProfilerHeight));            

                double filledRatio = Math.Max(0,(value.Severity - 1)/4);
                drawingContext.DrawRectangle(GetSeverityBrush(value.Severity), transparentPen, new Rect(posX + 5, posY + NodeBaseHeight, profilerWidth * filledRatio, NodeProfilerHeight));
            }
        }

        private void RenderNodeSingle(DrawingContext drawingContext, RequirementGraphNode node, double posX, double posY)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Brush strengthBRush = Common.Colors.GetRequirementStrengthBrush(node.Value.Strength);
            Pen strengthPen = node.Value.Strength == ParserEnums.LinkStrength.None ? dashedPen : borderPen;

            drawingContext.DrawRoundedRectangle(strengthBRush, strengthPen, new Rect(posX, posY, NodeWidth, NodeBaseHeight), 5, 5);

            //Render text
            var UIText = new FormattedText(node.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 12, Common.Colors.GetCategoryForeground(), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            UIText.MaxTextWidth = Math.Min(NodeWidth, UIText.Width);
            UIText.MaxTextHeight = NodeBaseHeight;

            double textPosX = posX + (NodeWidth - UIText.Width) * 0.5;
            double textPosY = posY + (NodeBaseHeight - UIText.Height) * 0.5;

            drawingContext.DrawText(UIText, new Point(textPosX, textPosY));

            RenderNodeProfilerChunk(drawingContext, node.ProfilerValue, posX, posY, strengthPen);
        }

        private void RenderOverlayedNodeGraph(DrawingContext drawingContext, RequirementGraphNode node, double posX, double posY, Brush brush, Pen pen)
        {
            drawingContext.DrawRoundedRectangle(brush, pen, new Rect(posX, posY, NodeWidth, NodeBaseHeight), 5, 5);

            if (node.ProfilerValue != null)
            {
                drawingContext.DrawRectangle(brush, pen, new Rect(posX + 5, posY + NodeBaseHeight, NodeWidth - 10, NodeProfilerHeight));
            }
        }

        private void RenderRootNode(DrawingContext drawingContext, Brush brush, Pen pen)
        {
            double paddingUp = CanvasPaddingY + PreAreaHeight;
            double paddingDown = CanvasPaddingY;
            double rootHeight = canvas.Height - ( paddingUp + paddingDown );
            drawingContext.DrawRectangle(brush, pen, new Rect(CanvasPaddingX, paddingUp, RootWidth, rootHeight));
        }

        private void CreateContextualMenu(object node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            AppendContextualMenuValue(contextMenuStrip, node);
            contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition);
        }

        private void AppendContextMenuProfilerValue(System.Windows.Forms.ContextMenuStrip contextMenuStrip, object nodeValue)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (nodeValue == null) 
                return;

            contextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            if (nodeValue is CompileValue)
            {
                var value = nodeValue as CompileValue;

                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Locate Max Timeline", (a, b) => CompilerTimeline.Instance.DisplayTimeline(value.MaxUnit, value)));
                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Locate Max Self Timeline", (a, b) => CompilerTimeline.Instance.DisplayTimeline(value.SelfMaxUnit, value)));                
                contextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Show Includers Graph", (a, b) => Includers.CompilerIncluders.Instance.DisplayIncluders(value)));
            }
            else if (nodeValue is UnitValue)
            {
                var value = nodeValue as UnitValue;

                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Open Timeline", (a, b) => CompilerTimeline.Instance.DisplayTimeline(value)));
            }
        }

        private void AppendContextualMenuValue(System.Windows.Forms.ContextMenuStrip contextMenuStrip, object node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (node is RequirementGraphRoot)
            {
                var value = node as RequirementGraphRoot;

                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Open File", (sender, e) => EditorUtils.OpenFileByName(value.Value.Filename)));
                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Copy Full Path", (a, b) => Clipboard.SetDataObject(value.Value.Filename)));
                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Copy Name", (sender, e) => Clipboard.SetDataObject(EditorUtils.GetFileNameSafe( value.Value.Filename))));

                AppendContextMenuProfilerValue(contextMenuStrip, value.ProfilerValue);

            }
            else if (node is RequirementGraphNode)
            {
                var value = node as RequirementGraphNode;

                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Open File", (sender, e) => EditorUtils.OpenFileByName(value.Value.Name)));
                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Copy Full Path", (a, b) => Clipboard.SetDataObject(value.Value.Name)));
                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Copy Name", (sender, e) => Clipboard.SetDataObject(value.Label)));

                contextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenuStrip.Items.Add(UIHelpers.CreateContextItem("Show Requirements Graph", (sender, e) => ParserData.DisplayRequirements(value.Value.Name)));

                AppendContextMenuProfilerValue(contextMenuStrip, value.ProfilerValue);
            }
        }

    }
}
