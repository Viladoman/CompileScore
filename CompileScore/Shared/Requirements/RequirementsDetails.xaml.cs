using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;


namespace CompileScore.Requirements
{
    public partial class RequirementsDetails : UserControl
    {
        public class UIBuilderContext
        { 
            public System.Windows.Media.Brush Foreground {  get; set; }
            public System.Windows.Media.Brush Background { get; set; }

            public string RootFullPath { get; set; }
            public string FileFullPath { get; set; }
        }

        public string RootFullPath { set; get; }

        public RequirementsDetails()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            InitializeComponent();

            SetRequirements(null);
        }

        private static StackPanel InitExpander(UIBuilderContext context, Expander expander, UIElement headerElement )
        {
            expander.Header = headerElement;
            expander.IsExpanded = true;
            expander.Foreground = context.Foreground;
            expander.Background = context.Background;
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(25, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel panel = new StackPanel();
            Grid.SetColumn(panel, 1);
            grid.Children.Add(panel);
            expander.Content = grid;
            return panel;
        }
        private static StackPanel InitExpander(UIBuilderContext context, Expander expander, string header)
        {
            return InitExpander(context, expander, new TextBlock() { Text = header, FontWeight = FontWeights.Bold });
        }

        private static Hyperlink CreateHyperlink( string text, RoutedEventHandler callback)
        {
            var h = new Hyperlink() { TextDecorations = null };
            h.Inlines.Add(text);
            h.Click += callback;
            return h; 
        }

        private static UIElement BuildSimpleRequirement(UIBuilderContext context, string name, ulong? nameLocation, List<ulong> locations)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            TextBlock block = new TextBlock();

            if (nameLocation.HasValue)
            {
                uint line = ParserData.DecodeInnerFileLine(nameLocation.Value);
                uint column = ParserData.DecodeInnerFileColumn(nameLocation.Value);
                block.Inlines.Add(CreateHyperlink(name, (sender, e) => EditorUtils.OpenFileAtLocation(context.FileFullPath,line,column) ) );
            }
            else
            {
                block.Inlines.Add(name);
            }
            block.Inlines.Add($" : Found {locations.Count} at ");

            int locCount = 0;
            foreach (ulong loc in locations)
            {
                if ( locCount > 0 ) block.Inlines.Add(", ");
                uint line = ParserData.DecodeInnerFileLine(loc);
                uint column = ParserData.DecodeInnerFileColumn(loc);
                block.Inlines.Add(CreateHyperlink($"{line}", (sender, e) => EditorUtils.OpenFileAtLocation(context.RootFullPath, line, column)));
                ++locCount;
            }

            return block; 
        }

        private static UIElement BuildCodeRequirement(UIBuilderContext context, ParserCodeRequirement code)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return BuildSimpleRequirement(context, code.Name, code.DefinitionLocation, code.UseLocations);
        }

        private static void BuildCodeRequirements(UIBuilderContext context, StackPanel panel, List<ParserCodeRequirement> codes)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (ParserCodeRequirement code in codes)
            {
                panel.Children.Add(BuildCodeRequirement(context, code));
            }
        }

        private static void BuildGlobals(StackPanel panel, UIBuilderContext context, List<ParserCodeRequirement>[] global)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            for (int i = 0; i < (int)ParserEnums.GlobalRequirement.Count; ++i)
            {
                if (global[i] == null)
                    continue;

                Expander subExpander = new Expander();
                StackPanel subPanel = InitExpander(context, subExpander, Common.RequirementLabel.GetLabel((ParserEnums.GlobalRequirement)i));
                BuildCodeRequirements(context,subPanel,global[i]);
                panel.Children.Add(subExpander);
            }           
        }

        private static UIElement BuildStructure(UIBuilderContext context, ParserStructureRequirement structure)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Expander mainExpander = new Expander();
            TextBlock mainExpanderHeader = new TextBlock() { FontWeight = FontWeights.Bold, FontSize = 12 };
            uint line = ParserData.DecodeInnerFileLine(structure.DefinitionLocation);
            uint column = ParserData.DecodeInnerFileColumn(structure.DefinitionLocation);
            mainExpanderHeader.Inlines.Add(CreateHyperlink(structure.Name, (sender, e) => EditorUtils.OpenFileAtLocation(context.FileFullPath, line, column)));
            StackPanel mainPanel = InitExpander(context, mainExpander, mainExpanderHeader);

            for (int i = 0; i < (int)ParserEnums.StructureSimpleRequirement.Count; ++i)
            {
                if (structure.Simple[i] == null)
                    continue;
                
                mainPanel.Children.Add(BuildSimpleRequirement(context, Common.RequirementLabel.GetLabel((ParserEnums.StructureSimpleRequirement)i), null, structure.Simple[i]));
            }

            for (int i = 0; i < (int)ParserEnums.StructureNamedRequirement.Count; ++i)
            {
                if (structure.Named[i] == null)
                    continue;

                Expander subExpander = new Expander();
                StackPanel subPanel = InitExpander(context, subExpander, Common.RequirementLabel.GetLabel((ParserEnums.StructureNamedRequirement)i));
                BuildCodeRequirements(context, subPanel, structure.Named[i]);
                mainPanel.Children.Add(subExpander);
            }

            return mainPanel.Children.Count == 0 ? null : mainExpander;
        }

        private static void BuildStructures(StackPanel panel, UIBuilderContext context, List<ParserStructureRequirement> structures)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (ParserStructureRequirement structure in structures ?? Enumerable.Empty<ParserStructureRequirement>())
            {
                UIElement structElement = BuildStructure(context, structure);
                if ( structElement != null )
                {
                    panel.Children.Add(structElement);
                }
            }
        }

        private static void BuildIncludes(StackPanel panel, List<ParserFileRequirements> includes)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (ParserFileRequirements file in includes ?? Enumerable.Empty<ParserFileRequirements>())
            {
                TextBlock text = new TextBlock();
                text.Inlines.Add(CreateHyperlink(EditorUtils.GetFileNameSafe(file.Name), (sender, e) => EditorUtils.OpenFileByName(file.Name)));
                text.ToolTip = file.Name;
                panel.Children.Add(text);
            }
        }

        public static string GetInclusionBucketText(object node)
        {
            if (node is RequirementGraphRoot)
            {
                return "Main";
            }

            if (node is RequirementGraphNode)
            {
                RequirementGraphNode graphNode = (RequirementGraphNode) node;
                if (graphNode.Row < 1)
                {
                    return "PreInclude";
                }

                if (graphNode.Column == 0)
                {
                    return "Direct";
                }

                return "Indirect";
            }

            return null;
        }

        private void BuildRequirementsUI(ParserFileRequirements file)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            globalsPanel.Children.Clear();
            structsPanel.Children.Clear();
            includesPanel.Children.Clear();

            headerNothing.Visibility = Visibility.Collapsed;
            headerGlobals.Visibility = Visibility.Collapsed;
            headerStructs.Visibility = Visibility.Collapsed;
            headerIncludes.Visibility = Visibility.Collapsed;

            if (file == null)
            {
                return;
            }

            UIBuilderContext context = new UIBuilderContext()
            {
                Foreground = this.Foreground,
                Background = this.Background,
                FileFullPath = file.Name,
                RootFullPath = RootFullPath
            };

            BuildGlobals(globalsPanel, context, file.Global);
            if (globalsPanel.Children.Count > 0) 
            {
                headerGlobals.Visibility = Visibility.Visible;
            }

            BuildStructures(structsPanel, context, file.Structures);
            if (structsPanel.Children.Count > 0)
            {
                headerStructs.Visibility = Visibility.Visible;
            }

            if (headerGlobals.Visibility == Visibility.Collapsed && headerStructs.Visibility == Visibility.Collapsed)
            {
                headerNothing.Visibility = Visibility.Visible;
            }

            BuildIncludes(includesPanel, file.Includes);
            if (includesPanel.Children.Count > 0)
            {
                headerIncludes.Visibility = Visibility.Visible;
            }
        }

        private void SetScore(object value)
        {
            if (value == null)
                return;

            if (value is CompileValue)
            {
                float severity = (value as CompileValue).Severity;

                score0.SetMoniker(severity > 0 ? Common.MonikerType.ScoreOn : Common.MonikerType.ScoreOff);
                score1.SetMoniker(severity > 1 ? Common.MonikerType.ScoreOn : Common.MonikerType.ScoreOff);
                score2.SetMoniker(severity > 2 ? Common.MonikerType.ScoreOn : Common.MonikerType.ScoreOff);
                score3.SetMoniker(severity > 3 ? Common.MonikerType.ScoreOn : Common.MonikerType.ScoreOff);
                score4.SetMoniker(severity > 4 ? Common.MonikerType.ScoreOn : Common.MonikerType.ScoreOff);
            }

            scoreGrid.Visibility = value is CompileValue ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BuildProfilerUI(object value, object includerValue)
        {
            headerProfiler.Visibility = Visibility.Collapsed; 
            profilerPanel.Visibility = Visibility.Collapsed;

            if (value == null)
                return;

            SetScore(value);
            profilerText.Text = Timeline.TimelineNodeTooltip.GetDetailsText(value, includerValue, EditorUtils.GetFileNameSafe(RootFullPath));
            headerProfiler.Visibility = Visibility.Visible;
            profilerPanel.Visibility = Visibility.Visible;
        }

        public void BuildMainUI(string fileName, string bucketText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            headerMainText.Visibility = Visibility.Collapsed; 

            if (fileName == null)
                return;

            headerMainText.Visibility = Visibility.Visible; 
            headerMainText.Inlines.Clear();
            headerMainText.Inlines.Add(CreateHyperlink(EditorUtils.GetFileNameSafe(fileName), (sender, e) => EditorUtils.OpenFileByName(fileName)));
            
            if ( bucketText != null )
                headerMainText.Inlines.Add($" ({bucketText})");

            headerMainText.ToolTip = fileName;
        }

        public void SetRequirements(object graphNode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (graphNode == null)
            {
                BuildMainUI(null, null);
                BuildProfilerUI(null, null);
                BuildRequirementsUI(null);
            }
            else if (graphNode is RequirementGraphNode)
            {
                RequirementGraphNode node = graphNode as RequirementGraphNode;

                BuildMainUI(node.Value.Name, GetInclusionBucketText(node) );
                BuildProfilerUI(node.ProfilerValue, node.IncluderValue);
                BuildRequirementsUI(node.Value);
            }
            else if (graphNode is RequirementGraphRoot)
            {
                RequirementGraphRoot node = graphNode as RequirementGraphRoot;

                BuildMainUI(node.Value.Filename, GetInclusionBucketText(node) );
                BuildProfilerUI(node.ProfilerValue, null);
                BuildRequirementsUI(null);
            }
        }

    }
}
