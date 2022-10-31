using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace CompileScore.Timeline
{
    public class TimelineNode
    {
        public TimelineNode(string label, uint start, uint duration, uint selfDuration, CompilerData.CompileCategory category, object compileValue = null)
        {
            Label = label;
            Start = start;
            Duration = duration;
            SelfDuration = selfDuration;
            Children = new List<TimelineNode>();
            Value = compileValue;
            Category = category;
        }

        public string Label { set; get; }
        public CompilerData.CompileCategory Category { get; }

        public object Value { set; get; }       

        public uint Start { set; get; }
        public uint Duration { set; get; }
        public uint SelfDuration { set; get; }
        public uint DepthLevel { set; get; }
        public uint MaxDepthLevel { set; get; }
        public Brush UIColor { set; get; }

        public TimelineNode Parent { set; get; }
        public List<TimelineNode> Children { get; }

        public void AddChild(TimelineNode childNode)
        {
            Children.Add(childNode);
            childNode.Parent = this;
        }

    }

    class CompilerTimeline
    {
        private static readonly Lazy<CompilerTimeline> lazy = new Lazy<CompilerTimeline>(() => new CompilerTimeline());

        const int TIMELINE_FILE_NUM_DIGITS = 4;
        private uint timelinePacking = 100;
        private uint version;

        private CompileScorePackage Package { get; set; }

        public uint TimelinePacking { get { return timelinePacking; } set { timelinePacking = Math.Max(1, value); } }

        public void Initialize(CompileScorePackage package)
        {
            Package = package;
        }

        public static CompilerTimeline Instance { get { return lazy.Value; } }
        public TimelineNode LoadTimeline(UnitValue unit)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (unit == null) { return null; }

            uint timelineId = unit.Index;

            //compute full path
            uint timelineFileNum = timelineId / timelinePacking;
            uint timelineInFileNum = timelineId % timelinePacking;
            string fullPath = CompilerData.Instance.GetScoreFullPath() + ".t" + timelineFileNum.ToString().PadLeft(TIMELINE_FILE_NUM_DIGITS, '0');

            TimelineNode root = null;

            if (File.Exists(fullPath))
            {
                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    uint thisVersion = reader.ReadUInt32();
                    version = thisVersion;

                    if (CompilerData.CheckVersion(thisVersion))
                    {
                        for (uint i = 0; i < timelineInFileNum && !ReachedEndOfStream(reader); ++i)
                        {
                            SkipTimeline(reader);
                        }

                        if (!ReachedEndOfStream(reader))
                        {
                            root = BuildTimelineRoot(reader,unit);
                        }
                    }
                }

                fileStream.Close();
            }

            return root;
        }

        bool ReachedEndOfStream(BinaryReader reader)
        {
            return reader.BaseStream.Position == reader.BaseStream.Length;
        }

        private void SkipTimeline(BinaryReader reader)
        {
            uint numTracks = reader.ReadUInt32();
            for (uint i=0;i<numTracks;++i)
            {
                SkipTimelineTrack(reader);
            }
        }

        private void SkipTimelineTrack(BinaryReader reader)
        {
            uint numEvents = reader.ReadUInt32();
            const uint nodeSize = 13; //4+4+4+1
            reader.ReadBytes((int)(numEvents * nodeSize));
        }

        private TimelineNode LoadNode(BinaryReader reader)
        {
            uint start = reader.ReadUInt32();
            uint duration = reader.ReadUInt32();
            uint selfDuration = 0;
            if (version >= 7)
            {
                selfDuration = reader.ReadUInt32();
            }
            uint eventId = reader.ReadUInt32();
            CompilerData.CompileCategory category = (CompilerData.CompileCategory)reader.ReadByte();
            CompileValue value = CompilerData.Instance.GetValue(category,(int)eventId);

            string label = value != null ? value.Name : Common.UIConverters.ToSentenceCase(category.ToString()); 
            label += " ( "+ Common.UIConverters.GetTimeStr(duration) +" )" ;

            return new TimelineNode(label, start, duration, selfDuration, category, value);
        }

        private void AdjustNodeProperties(TimelineNode node)
        {
            if (node.Children.Count > 0)
            {
                node.Start = node.Children[0].Start;
                foreach(TimelineNode child in node.Children)
                {
                    node.Duration = Math.Max(node.Duration, (child.Start+child.Duration)-node.Start);
                }
            }
        }

        private TimelineNode BuildTimelineTree(BinaryReader reader)
        {
            uint numEvents = reader.ReadUInt32();

            TimelineNode root = new TimelineNode(CompilerData.CompileCategory.Thread.ToString(), 0, 0, 0, CompilerData.CompileCategory.Thread);

            TimelineNode parent = root;

            for (uint i = 0u; i < numEvents; ++i)
            {
                TimelineNode newNode = LoadNode(reader);

                //Find parent node 
                while (parent != root && (newNode.Start >= (parent.Start + parent.Duration))) { parent = parent.Parent; }

                parent.AddChild(newNode);
                parent = newNode.Duration == 0 ? parent : newNode;
            }

            AdjustNodeProperties(root);

            return root;  
        }

        private void InitializeNodeRecursive(TimelineNode node, uint baseDepth = 0)
        {
            node.DepthLevel = node.Parent == null ? baseDepth : node.Parent.DepthLevel + 1;
            node.MaxDepthLevel = node.DepthLevel;
            node.UIColor = Common.Colors.GetCategoryBackground(node.Category);

            foreach (TimelineNode child in node.Children)
            {
                InitializeNodeRecursive(child,baseDepth);
                node.MaxDepthLevel = Math.Max(node.MaxDepthLevel, child.MaxDepthLevel);
            }
        }

        private uint GetSectionDepthLevel(TimelineNode node, uint from, uint to)
        {
            if (node.Start > to || (node.Start + node.Duration) < from)
            {
                //early exit, no overlap
                return 0;
            }
                
            uint level = node.DepthLevel;

            foreach (TimelineNode child in node.Children)
            {
                level = Math.Max(level, GetSectionDepthLevel(child,from,to));
            }

            return level;
        }

        private uint GetBaseDepthLevel(TimelineNode root, TimelineNode input)
        {
            uint maxLevel = 1;
            foreach (TimelineNode child in root.Children)
            {
                maxLevel = Math.Max(maxLevel,GetSectionDepthLevel(child, input.Start, input.Start + input.Duration)+2);
            }

            return maxLevel;
        }

        private TimelineNode BuildTimelineRoot(BinaryReader reader, UnitValue unit)
        {
            uint numTracks = reader.ReadUInt32();

            TimelineNode root = new TimelineNode("", 0, 0, 0, CompilerData.CompileCategory.Timeline);
            InitializeNodeRecursive(root);

            for (uint i=0;i<numTracks;++i)
            {
                TimelineNode tree = BuildTimelineTree(reader);

                if (tree.Children.Count > 0 && tree.Children[0].Category == CompilerData.CompileCategory.ExecuteCompiler)
                {
                    //skip the thread for the main track
                    tree = tree.Children[0];
                    tree.Value = unit;
                }
                
                //Initialize nodes
                InitializeNodeRecursive(tree, GetBaseDepthLevel(root,tree));
                root.MaxDepthLevel = Math.Max(root.MaxDepthLevel, tree.MaxDepthLevel);

                root.AddChild(tree);
            }

            AdjustNodeProperties(root);

            root.Value = unit;
            root.Label = unit.Name + " ( " + Common.UIConverters.GetTimeStr(root.Duration) + " )";

            return root.Children.Count > 0? root : null;
        }

        public void DisplayTimeline(UnitValue unit, CompileValue value = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            TimelineWindow window = FocusTimelineWindow();
            window.SetTimeline(unit,value);
        }

        public TimelineWindow FocusTimelineWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            TimelineWindow window = Package.FindToolWindow(typeof(TimelineWindow), 0, true) as TimelineWindow;
            if ((null == window) || (null == window.GetFrame()))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            window.ProxyShow();

            return window;
        }
    }
}
