using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;

namespace CompileScore.Includers
{
    class IncludersValue
    {
        public bool Visited { set; get; } = false;

        public List<uint> Includes { get; set; }
        public List<uint> Units { get; set; }
    }

    class CompilerIncluders
    {
        const uint durationMultiplier = 1000;
        const uint basePadding = 20;

        private static readonly Lazy<CompilerIncluders> lazy = new Lazy<CompilerIncluders>(() => new CompilerIncluders());

        private CompileScorePackage Package { get; set; }

        public void Initialize(CompileScorePackage package)
        {
            Package = package;
        }

        public static CompilerIncluders Instance { get { return lazy.Value; } }

        public Timeline.TimelineNode LoadInclude(uint index)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            List<IncludersValue> includers = LoadIncluderValues();
            if (includers == null)
            {
                return null;
            }

            Timeline.TimelineNode root = BuildGraphRecursive(includers, index);
            InitializeTree(root);
            return root;
        }

        private List<IncludersValue> LoadIncluderValues()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string fullPath = CompilerData.Instance.GetScoreFullPath() + ".incl";

            List<IncludersValue> ret = null;

            if (File.Exists(fullPath))
            {
                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    uint thisVersion = reader.ReadUInt32();

                    if (CompilerData.CheckVersion(thisVersion))
                    {
                        ret = ReadIncluderValues(reader);
                    }
                }

                fileStream.Close();
            }
    
            return ret;
        }

        private List<IncludersValue> ReadIncluderValues(BinaryReader reader)
        {
            List<IncludersValue> list = new List<IncludersValue>();

            uint count = reader.ReadUInt32();
            for (uint i=0;i<count;++i)
            {
                list.Add(ReadIncluderValue(reader));
            }

            return list;
        }

        private IncludersValue ReadIncluderValue(BinaryReader reader)
        {
            IncludersValue ret = new IncludersValue();

            ret.Includes = ReadIndexList(reader);
            ret.Units = ReadIndexList(reader);

            return ret;
        }

        private List<uint> ReadIndexList(BinaryReader reader)
        {
            uint count = reader.ReadUInt32();
            if (count == 0)
            {
                return null;
            }

            List<uint> ret = new List<uint>();
            for (uint i = 0; i < count; ++i)
            {
                ret.Add(reader.ReadUInt32());
            }

            return ret;
        }

        private Timeline.TimelineNode BuildGraphRecursive(List<IncludersValue> includers, uint index)
        {
            if (index > includers.Count)
            {
                return null;
            }

            IncludersValue value = includers[(int)index];

            //Only add each element once to avoid cycles
            if (value.Visited)
            {
                return null;
            }
            value.Visited = true;

            CompileValue compileValue = CompilerData.Instance.GetValue(CompilerData.CompileCategory.Include, (int)index);
            Timeline.TimelineNode node = new Timeline.TimelineNode(null, 0, 0, CompilerData.CompileCategory.Include, compileValue);

            if (value.Includes != null)
            {
                for (int i=0;i<value.Includes.Count;++i)
                {
                    //Build all children 
                    Timeline.TimelineNode child = BuildGraphRecursive(includers, value.Includes[i]);
                    if (child != null)
                    {
                        node.Duration += child.Duration;
                        node.AddChild(child);
                    }
                }
            }

            if (value.Units != null)
            {
                for (int i=0;i<value.Units.Count;++i)
                {
                    Timeline.TimelineNode child = CreateUnitNode(value.Units[i]);
                    node.Duration += child.Duration;
                    node.AddChild(child);
                }
            }

            if (value.Includes == null && value.Units == null)
            {
                Timeline.TimelineNode child = new Timeline.TimelineNode("-- Dead End --", 0, durationMultiplier, 0, CompilerData.CompileCategory.DebugType);
                node.Duration += child.Duration;
                node.AddChild(child);
            }

            //fix up node
            node.Label = compileValue == null ? "-- Unknown --" : compileValue.Name + " ( " + (node.Duration/durationMultiplier) + " )";    

            return node;
        }

        private Timeline.TimelineNode CreateUnitNode(uint index)
        {
            UnitValue unit = CompilerData.Instance.GetUnitByIndex(index);
            string label = unit == null? "-- Unknown -- " : unit.Name; 
            return new Timeline.TimelineNode(label, 0, durationMultiplier, CompilerData.CompileCategory.ExecuteCompiler, unit);
        }

        private void InitializeTree(Timeline.TimelineNode node)
        {
            node.Start = basePadding;
            InitializeNodeRecursive(node);
            node.Start = 0;
            node.Duration += basePadding * 2;
        }

        private void InitializeNodeRecursive(Timeline.TimelineNode node, uint baseDepth = 0)
        {
            node.DepthLevel = node.Parent == null ? baseDepth : node.Parent.DepthLevel + 1;
            node.MaxDepthLevel = node.DepthLevel;
            node.UIColor = Common.Colors.GetCategoryBackground(node.Category);

            //reorder children by duration
            uint offset = node.Start;
            node.Children.Sort((a,b)=> a.Duration == b.Duration? 0 : (a.Duration > b.Duration? -1 : 1));

            foreach (Timeline.TimelineNode child in node.Children)
            {
                child.Start = offset;
                offset += child.Duration;
                InitializeNodeRecursive(child, baseDepth);
                node.MaxDepthLevel = Math.Max(node.MaxDepthLevel, child.MaxDepthLevel);
            }
        }

        public void DisplayIncluders(CompileValue value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IncludersWindow window = FocusIncludersWindow();
            window.SetIncluders(value);
        }

        public IncludersWindow FocusIncludersWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            IncludersWindow window = Package.FindToolWindow(typeof(IncludersWindow), 0, true) as IncludersWindow;
            if ((null == window) || (null == window.GetFrame()))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            window.ProxyShow();

            return window;
        }
    }
}
