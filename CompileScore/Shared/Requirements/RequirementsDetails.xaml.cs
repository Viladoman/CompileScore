using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Documents.DocumentStructures;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Xaml.Schema;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace CompileScore.Requirements
{
    public partial class RequirementsDetails : UserControl
    {
        public class UIBuilderContext
        { 
            public System.Windows.Media.Brush Foreground {  get; set; }
            public System.Windows.Media.Brush Background { get; set; }
        }


        public RequirementsDetails()
        {
            InitializeComponent();
        }

        public static StackPanel InitExpander(UIBuilderContext context, Expander expander, object header )
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

        public static Hyperlink CreateHyperlink( string text, RequestNavigateEventHandler callback)
        {
            var h = new Hyperlink();
            h.Inlines.Add(text);
            h.RequestNavigate += callback;
            return h; 
        }

        public static UIElement BuildSimpleRequirement(UIBuilderContext context,  string name, List<ulong> locations)
        {
            TextBlock block = new TextBlock() { Foreground = context.Foreground, Background = context.Background };

            block.Inlines.Add(CreateHyperlink(name, (sender, e) => Clipboard.SetText("hola.txt") ) );
            block.Inlines.Add($" : Found {locations.Count} at ");

            int locCount = 0;
            foreach (ulong loc in locations)
            {
                if ( locCount > 0 ) block.Inlines.Add(", ");
                uint line = ParserData.DecodeInnerFileLine(loc);
                block.Inlines.Add(CreateHyperlink($"{line}", (sender, e) => Clipboard.SetText("hola.txt")));
                ++locCount;
            }

            return block; 
        }

        public static UIElement BuildCodeRequirement(UIBuilderContext context, ParserCodeRequirement code)
        {
            return BuildSimpleRequirement(context, code.Name, code.UseLocations);
        }

        public static void BuildCodeRequirements(UIBuilderContext context, StackPanel panel, List<ParserCodeRequirement> codes)
        {
            foreach (ParserCodeRequirement code in codes)
            {
                panel.Children.Add(BuildCodeRequirement(context, code));
            }
        }

        public static UIElement BuildGlobals(UIBuilderContext context, List<ParserCodeRequirement>[] global)
        {
            Expander globalsExpander = new Expander();
            StackPanel globalsPanel = InitExpander(context, globalsExpander, "Globals");

            for (int i = 0; i < (int)ParserEnums.GlobalRequirement.Count; ++i)
            {
                if (global[i] == null)
                    continue;

                Expander subExpander = new Expander();
                StackPanel subPanel = InitExpander(context, subExpander, ((ParserEnums.GlobalRequirement)i).ToString());
                BuildCodeRequirements(context,subPanel,global[i]);
                globalsPanel.Children.Add(subExpander);
            }

            return globalsExpander;
        }

        public static UIElement BuildStructure(UIBuilderContext context, ParserStructureRequirement structure)
        {
            Expander mainExpander = new Expander();
            StackPanel mainPanel = InitExpander(context, mainExpander, structure.Name);

            for (int i = 0; i < (int)ParserEnums.StructureSimpleRequirement.Count; ++i)
            {
                if (structure.Simple[i] == null)
                    continue;
                
                mainPanel.Children.Add(BuildSimpleRequirement(context, ((ParserEnums.GlobalRequirement)i).ToString(), structure.Simple[i]));
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

            return mainExpander;
        }

        public static UIElement BuildStructures(UIBuilderContext context, List<ParserStructureRequirement> structures)
        {
            Expander globalsExpander = new Expander();
            StackPanel globalsPanel = InitExpander(context, globalsExpander, "Structures");

            foreach(ParserStructureRequirement structure in structures)
            {
                globalsPanel.Children.Add(BuildStructure(context, structure));
            }

            return globalsExpander;
        }

        public static void BuildTree(UIBuilderContext context, StackPanel panel, ParserFileRequirements file)
        {
            if (file == null)
                return;

            panel.Children.Add(BuildGlobals(context, file.Global));
            panel.Children.Add(BuildStructures(context, file.Structures));
            //TODO ~ ramonv ~ add indirect includes 
        }

        public void SetRequirements(ParserFileRequirements file)
        {
            detailsMainPanel.Children.Clear();

            UIBuilderContext context = new UIBuilderContext() { Foreground = this.Foreground, Background = this.Background };
            BuildTree(context, detailsMainPanel,file);
        }

    }
}
