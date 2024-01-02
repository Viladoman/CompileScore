using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

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

        private static StackPanel InitExpander(UIBuilderContext context, Expander expander, object header )
        {
            expander.Header = header;
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

        private static Hyperlink CreateHyperlink( string text, RoutedEventHandler callback)
        {
            var h = new Hyperlink();
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
                StackPanel subPanel = InitExpander(context, subExpander, ((ParserEnums.GlobalRequirement)i).ToString());
                BuildCodeRequirements(context,subPanel,global[i]);
                panel.Children.Add(subExpander);
            }           
        }

        private static UIElement BuildStructure(UIBuilderContext context, ParserStructureRequirement structure)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Expander mainExpander = new Expander();
            StackPanel mainPanel = InitExpander(context, mainExpander, structure.Name);

            for (int i = 0; i < (int)ParserEnums.StructureSimpleRequirement.Count; ++i)
            {
                if (structure.Simple[i] == null)
                    continue;
                
                mainPanel.Children.Add(BuildSimpleRequirement(context, ((ParserEnums.StructureSimpleRequirement)i).ToString(), null, structure.Simple[i]));
            }

            for (int i = 0; i < (int)ParserEnums.StructureNamedRequirement.Count; ++i)
            {
                if (structure.Named[i] == null)
                    continue;

                Expander subExpander = new Expander();
                StackPanel subPanel = InitExpander(context, subExpander, ((ParserEnums.StructureNamedRequirement)i).ToString());
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

        private void BuildProfilerUI(object value, object includerValue)
        {
            headerProfiler.Visibility = Visibility.Collapsed; 

            if (value == null)
                return;
            

            if (value is CompileValue)
            {
                headerProfiler.Visibility = Visibility.Visible;

                //TODO ~ ramonv ~ to be implemented add file name, full path, score, values 
            }
            else if ( value is UnitValue)
            {
                //TODO ~ ramonv ~ to be implemented
            }
        }

        public void SetRequirements(object graphNode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (graphNode == null)
            {
                BuildProfilerUI(null, null);
                BuildRequirementsUI(null);
            }
            else if (graphNode is RequirementGraphNode)
            {
                RequirementGraphNode node = graphNode as RequirementGraphNode;

                BuildProfilerUI(node.ProfilerValue, node.IncluderValue);
                BuildRequirementsUI(node.Value);
            }
            else if (graphNode is RequirementGraphRoot)
            {
                RequirementGraphRoot node = graphNode as RequirementGraphRoot;

                BuildProfilerUI(node.ProfilerValue, null);
                BuildRequirementsUI(null);
            }
        }

    }
}
