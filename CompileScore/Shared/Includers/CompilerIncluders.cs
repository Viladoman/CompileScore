using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;

namespace CompileScore.Includers
{
    class IncludersInclValue
    {
        public uint Index { get; set; }
        public ulong Accumulated { get; set; }
        public uint Count { get; set; }
        public uint Max { get; set; }
        public uint MaxId { get; set; }
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

    class CompilerIncluders
    {
        public const uint durationMultiplier = 1000;
        const uint basePadding = 20;
        private CompileScorePackage Package { get; set; }

        private static readonly Lazy<CompilerIncluders> lazy = new Lazy<CompilerIncluders>(() => new CompilerIncluders());
        public static CompilerIncluders Instance { get { return lazy.Value; } }

        private List<IncludersValue> CachedIncludes { get; set; }

        public event Notify IncludersDataLoaded;

        public void Initialize(CompileScorePackage package)
        {
            Package = package;

            CompilerData.Instance.ScoreDataChanged += OnScoreDataChanged;
        }

        public Timeline.TimelineNode LoadInclude(uint index)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (CachedIncludes == null)
            {
                return null;
            }

            Timeline.TimelineNode root = BuildGraphRecursive(CachedIncludes, index);
            ClearVisited(CachedIncludes);
            InitializeTree(root);
            return root;
        }
        private void TriggerLoadCachedValues()
        {
            string fullPath = CompilerData.Instance.GetScoreFullPath() + ".incl";

            Common.ThreadUtils.Fork(async delegate
            {
                List<IncludersValue> list = LoadIncluderValues(fullPath);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                CachedIncludes = list;
                IncludersDataLoaded?.Invoke();
            });
        }

        private void OnScoreDataChanged()
        {
            //drop the cached includers list and reload them
            CachedIncludes = null;
            TriggerLoadCachedValues();
        }

        private static List<IncludersValue> LoadIncluderValues(string fullPath)
        {
            List<IncludersValue> ret = null;

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

        private static List<IncludersValue> ReadIncluderValues(BinaryReader reader, uint version)
        {
            List<IncludersValue> list = new List<IncludersValue>();

            uint count = reader.ReadUInt32();
            for (uint i=0;i<count;++i)
            {
                list.Add(ReadIncluderValue(reader, version));
            }

            return list;
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

            if ( CachedIncludes == null || includerIndex < 0 || includeeIndex < 0 )
                return null;

            foreach ( IncludersUnitValue value in CachedIncludes[includeeIndex].Units )
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

            if (CachedIncludes == null || includerIndex < 0 || includeeIndex < 0)
                return null;

            foreach ( IncludersInclValue value in CachedIncludes[includeeIndex].Includes)
            {
                if (value.Index == includerIndex)
                {
                    return value;
                }
            }

            return null;
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
                    Timeline.TimelineNode child = BuildGraphRecursive(includers, value.Includes[i].Index);
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
                    Timeline.TimelineNode child = CreateUnitNode(value.Units[i].Index);
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
