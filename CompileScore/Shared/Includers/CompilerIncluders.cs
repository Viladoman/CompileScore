using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;

namespace CompileScore.Includers
{
    public enum IncludersDisplayMode
    {
        Once,
        Full
    }

    class IncludersInclValue
    {
        public uint Index { get; set; }
        public ulong Accumulated { get; set; }
        public uint Count { get; set; }
        public uint Max { get; set; }
        public uint MaxId { get; set; }
        public uint Average { get { return Count > 0 ? (uint)(Accumulated / Count) : 0; } }
    }

    class IncludersUnitValue
    {
        public uint Index { get; set; }
        public uint Duration { get; set; }
    }

    class IncludersValue
    {
        public bool Visited { set; get; } = false;

        public List<IncludersInclValue> Includes { get; set; }
        public List<IncludersUnitValue> Units { get; set; }
    }

    class IncluderTreeLink
    {
        public CompileValue Includee { set; get; }
        public object Includer { set; get; } //CompileValue or UnitValue
        public object Value { set; get; } //This can be either IncludersUnitValue or IncludersInclValue
    }

    class IncludersDataChunk
    {
        public List<IncludersValue> Includers { get; set; } = new List<IncludersValue>();
        public int LinkCount { get; set; } = 0;
    } 

    class CompilerIncluders
    {
        public const uint durationMultiplier = 1000;
        const uint basePadding = 20;

        private static readonly Lazy<CompilerIncluders> lazy = new Lazy<CompilerIncluders>(() => new CompilerIncluders());
        public static CompilerIncluders Instance { get { return lazy.Value; } }

        private IncludersDataChunk IncludersData { get; set; }

        public Timeline.TimelineNode BuildIncludersTree(uint index, IncludersDisplayMode mode)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IncludersData == null)
            {
                return null;
            }

            int nodesCount = 0; 
            Timeline.TimelineNode root = BuildGraphRecursive(IncludersData.Includers, mode, index, ref nodesCount);
            ClearVisited(IncludersData.Includers);
            InitializeTree(root);
            return root;
        }

        public void SetIncluderData(IncludersDataChunk data)
        {
            IncludersData = data;
        }

        public IncludersDataChunk GetIncluderData() 
        { 
            return IncludersData; 
        }

        public static IncludersDataChunk ReadIncludersFromFile(string fullPath)
        {
            IncludersDataChunk ret = null;

            if (File.Exists(fullPath))
            {
                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    uint thisVersion = reader.ReadUInt32();

                    if (CompilerData.CheckVersion(thisVersion))
                    {
                        ret = ReadIncluderValues(reader,thisVersion);
                    }
                }

                fileStream.Close();
            }
    
            return ret;
        }

        public static IncludersDataChunk ReadIncluderValues(BinaryReader reader, uint version)
        {
            IncludersDataChunk chunk = new IncludersDataChunk();

            uint count = reader.ReadUInt32();
            for (uint i=0;i<count;++i)
            {
                IncludersValue newValue = ReadIncluderValue(reader, version);
                chunk.Includers.Add(newValue);
                chunk.LinkCount += newValue.Units == null? 0 : newValue.Units.Count;
                chunk.LinkCount += newValue.Includes == null? 0 : newValue.Includes.Count;
            }

            return chunk;
        }

        private static IncludersValue ReadIncluderValue(BinaryReader reader, uint version)
        {
            IncludersValue ret = new IncludersValue();

            ret.Includes = ReadIncludersInclList(reader, version);
            ret.Units    = ReadIncludersUnitList(reader, version);

            return ret;
        }

        private static List<IncludersInclValue> ReadIncludersInclList(BinaryReader reader, uint version)
        {
            uint count = reader.ReadUInt32();
            if (count == 0)
            {
                return null;
            }

            List<IncludersInclValue> ret = new List<IncludersInclValue>();
            for (uint i = 0; i < count; ++i)
            {
                IncludersInclValue entry = new IncludersInclValue();
                entry.Index       = reader.ReadUInt32();
                if (version >= 11)
                {
                    entry.Accumulated = reader.ReadUInt64();
                    entry.Count       = reader.ReadUInt32();
                    entry.Max         = reader.ReadUInt32();
                    entry.MaxId       = reader.ReadUInt32();
                }
                ret.Add(entry);
            }

            return ret;
        }

        private static List<IncludersUnitValue> ReadIncludersUnitList(BinaryReader reader, uint version)
        {
            uint count = reader.ReadUInt32();
            if (count == 0)
            {
                return null;
            }

            List<IncludersUnitValue> ret = new List<IncludersUnitValue>();
            for (uint i = 0; i < count; ++i)
            {
                IncludersUnitValue entry = new IncludersUnitValue();
                entry.Index = reader.ReadUInt32();
                if (version >= 11)
                {
                    entry.Duration = reader.ReadUInt32();
                }
                ret.Add(entry);
            }

            return ret;
        }

        public IncludersUnitValue GetIncludeUnitValue(int includerIndex, int includeeIndex)
        {
            if ( CompilerData.Instance.GetSession().Version < 11 )
                return null;

            if (IncludersData == null || includerIndex < 0 || includeeIndex < 0 || includeeIndex > IncludersData.Includers.Count || IncludersData.Includers[includeeIndex].Units == null)
                return null;

            foreach ( IncludersUnitValue value in IncludersData.Includers[includeeIndex].Units )
            {
                if ( value.Index == includerIndex)
                {
                    return value; 
                }
            }

            return null;
        }

        public IncludersInclValue GetIncludeInclValue(int includerIndex, int includeeIndex)
        {
            if (CompilerData.Instance.GetSession().Version < 11)
                return null;

            if (IncludersData == null || includerIndex < 0 || includeeIndex < 0 || includeeIndex > IncludersData.Includers.Count || IncludersData.Includers[includeeIndex].Includes == null)
                return null;

            foreach ( IncludersInclValue value in IncludersData.Includers[includeeIndex].Includes)
            {
                if (value.Index == includerIndex)
                {
                    return value;
                }
            }

            return null;
        }

        private Timeline.TimelineNode BuildGraphRecursive(List<IncludersValue> includers, IncludersDisplayMode mode, uint index, ref int nodesCount, CompileValue includee = null, IncludersInclValue includerValue = null)
        {
            const int NODES_MAX = 500000;
            if (index > includers.Count || nodesCount >= NODES_MAX)
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

            IncluderTreeLink nodeValue = new IncluderTreeLink();
            nodeValue.Includer = compileValue;
            nodeValue.Value = CompilerData.Instance.GetSession().Version < 11? null : includerValue;
            nodeValue.Includee = includee;

            Timeline.TimelineNode node = new Timeline.TimelineNode(null, 0, 0, CompilerData.CompileCategory.Include, nodeValue);

            if (value.Includes != null)
            {
                for (int i=0;i<value.Includes.Count && nodesCount < NODES_MAX; ++i)
                {
                    //Build all children 
                    Timeline.TimelineNode child = BuildGraphRecursive(includers, mode, value.Includes[i].Index, ref nodesCount, compileValue, value.Includes[i]);
                    if (child != null)
                    {
                        node.Duration += child.Duration;
                        node.AddChild(child);
                    }
                }
            }

            if (value.Units != null)
            {
                for (int i=0;i<value.Units.Count && nodesCount < NODES_MAX; ++i)
                {
                    Timeline.TimelineNode child = CreateUnitNode(value.Units[i].Index, compileValue, value.Units[i]);
                    node.Duration += child.Duration;
                    node.AddChild(child);
                    ++nodesCount;
                }
            }

            if (value.Includes == null && value.Units == null)
            {
                Timeline.TimelineNode child = new Timeline.TimelineNode("-- Dead End --", 0, durationMultiplier, 0, CompilerData.CompileCategory.Other);
                node.Duration += child.Duration;
                node.AddChild(child);
            }

            //fix up node
            node.Label = compileValue == null ? "-- Unknown --" : compileValue.Name + " ( " + (node.Duration/durationMultiplier) + " )";

            if (mode == IncludersDisplayMode.Full)
            {
                //allow this node to be reused in other stacks
                value.Visited = false;
            }

            return node;
        }

        private Timeline.TimelineNode CreateUnitNode(uint index, CompileValue includee, IncludersUnitValue includerValue )
        {
            UnitValue unit = CompilerData.Instance.GetUnitByIndex(index);

            IncluderTreeLink nodeValue = new IncluderTreeLink();
            nodeValue.Includer = unit;
            nodeValue.Value = CompilerData.Instance.GetSession().Version < 11? null : includerValue;
            nodeValue.Includee = includee;

            string label = unit == null? "-- Unknown -- " : unit.Name; 
            return new Timeline.TimelineNode(label, 0, durationMultiplier, CompilerData.CompileCategory.ExecuteCompiler, nodeValue);
        }

        private void ClearVisited(List<IncludersValue> includers)
        {
            foreach(IncludersValue value in includers) 
            { 
                value.Visited = false; 
            }
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

        public static IncludersWindow FocusIncludersWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            IncludersWindow window = CompilerData.Instance.Package.FindToolWindow(typeof(IncludersWindow), 0, true) as IncludersWindow;
            if ((null == window) || (null == window.GetFrame()))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            window.ProxyShow();

            return window;
        }
    }
}
