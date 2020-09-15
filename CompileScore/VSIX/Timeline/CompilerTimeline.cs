using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace CompileScore.Timeline
{
    public class TimelineNode
    {
        public TimelineNode(string label, uint start, uint duration, CompilerData.CompileCategory category, object compileValue = null)
        {
            Label = label;
            Start = start;
            Duration = duration;
            Children = new List<TimelineNode>();
            Value = compileValue;
            Category = category;
        }

        public string Label { set; get; }
        public CompilerData.CompileCategory Category { get; }

        public object Value { set; get; }       

        public uint Start { get; }
        public uint Duration { get; }
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

        const int TIMELINES_PER_FILE = 100;
        const int TIMELINE_FILE_NUM_DIGITS = 4;
        private CompileScorePackage Package { get; set; }

        public void Initialize(CompileScorePackage package)
        {
            Package = package;
        }

        public static CompilerTimeline Instance { get { return lazy.Value; } }
        public TimelineNode LoadTimeline(FullUnitValue unit)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (unit == null) { return null; }

            uint timelineId = unit.Index;

            //compute full path
            uint timelineFileNum = timelineId / TIMELINES_PER_FILE;
            uint timelineInFileNum = timelineId % TIMELINES_PER_FILE;
            string fullPath = CompilerData.Instance.GetScoreFullPath() + ".t" + timelineFileNum.ToString().PadLeft(TIMELINE_FILE_NUM_DIGITS, '0');

            TimelineNode root = null;

            if (File.Exists(fullPath))
            {
                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    uint thisVersion = reader.ReadUInt32();

                    if (thisVersion == CompilerData.VERSION)
                    {
                        for (uint i = 0; i < timelineInFileNum && !ReachedEndOfStream(reader); ++i)
                        {
                            SkipTimeline(reader);
                        }

                        if (!ReachedEndOfStream(reader))
                        {
                            root = BuildTimelineTree(reader);
                            FinializeRoot(root, unit);
                        }
                    }
                    else
                    {
                        OutputLog.Error("Version mismatch! Expected " + CompilerData.VERSION + " - Found " + thisVersion + " - Please export again with matching Data Exporter");
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
            uint numEvents = reader.ReadUInt32();
            const uint nodeSize = 13; //4+4+4+1
            reader.ReadBytes((int)(numEvents * nodeSize));
        }

        private TimelineNode LoadNode(BinaryReader reader)
        {
            uint start = reader.ReadUInt32();
            uint duration = reader.ReadUInt32();
            uint eventId = reader.ReadUInt32();
            CompilerData.CompileCategory category = (CompilerData.CompileCategory)reader.ReadByte();
            CompileValue value = CompilerData.Instance.GetValue(category,(int)eventId);

            string label = value != null ? value.Name : Common.UIConverters.ToSentenceCase(category.ToString()); 
            label += " ( "+ Common.UIConverters.GetTimeStr(duration) +" )" ;

            return new TimelineNode(label, start, duration, category, value);
        }

        private TimelineNode BuildTimelineTree(BinaryReader reader)
        {
            TimelineNode root = null;
           
            //THe nodes are sorted by start time 
            uint numEvents = reader.ReadUInt32();

            if (numEvents > 0)
            {
                TimelineNode parent = null;

                for (uint i=0u;i<numEvents;++i)
                {
                    TimelineNode newNode = LoadNode(reader);

                    //Find parent node 
                    while (parent != null && newNode.Start >= (parent.Start+ parent.Duration) ){ parent = parent.Parent; }

                    if (parent == null) root = newNode; 
                    else parent.AddChild(newNode);

                    parent = newNode;
                }
            }

            return root; 
        }

        private void FinializeRoot(TimelineNode root, FullUnitValue unit)
        {
            if (root.Category == CompilerData.CompileCategory.ExecuteCompiler)
            {
                root.Value = unit;
                root.Label = unit.Name + " ( " + Common.UIConverters.GetTimeStr(root.Duration) + " )";
            }
        }

        public void DisplayTimeline(FullUnitValue unit, CompileValue value = null)
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
