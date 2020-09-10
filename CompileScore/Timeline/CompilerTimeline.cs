using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace CompileScore.Timeline
{
    public class TimelineNode
    {
        public TimelineNode(string label, uint start, uint duration, CompilerData.CompileCategory category, CompileValue compileValue = null)
        {
            Label = label;
            Start = start;
            Duration = duration;
            Children = new List<TimelineNode>();
            Value = compileValue;
            Category = category;
        }

        public string Label { get; }
        public CompilerData.CompileCategory Category { get; }

        public CompileValue Value { set; get; }       

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
        const int TIMELINES_PER_FILE = 100;
        const int TIMELINE_FILE_NUM_DIGITS = 4;

        static public TimelineNode LoadTimeline(uint timelineId)
        {
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
                            //TODO ~ ramonv ~ do something with the timelineId
                        }
                    }
                    else
                    {
                        OutputLog.Error("Version mismatch! Expected " + CompilerData.VERSION + " - Found " + thisVersion);
                    }
                }

                fileStream.Close();
            }
            return root;
        }

        static bool ReachedEndOfStream(BinaryReader reader)
        {
            return reader.BaseStream.Position == reader.BaseStream.Length;
        } 

        static private void SkipTimeline(BinaryReader reader)
        {
            uint numEvents = reader.ReadUInt32();
            const uint nodeSize = 13; //4+4+4+1
            reader.ReadBytes((int)(numEvents * nodeSize));
        }

        static private TimelineNode LoadNode(BinaryReader reader)
        {
            uint start = reader.ReadUInt32();
            uint duration = reader.ReadUInt32();
            uint eventId = reader.ReadUInt32();
            CompilerData.CompileCategory category = (CompilerData.CompileCategory)reader.ReadByte();
            CompileValue value = CompilerData.Instance.GetValue(category,(int)eventId);

            string label = value != null ? value.Name : Common.UIConverters.ToSentenceCase(category.ToString()); 
            label += "("+ Common.UIConverters.GetTimeStr(duration) +")" ;

            return new TimelineNode(label, start, duration, category, value);
        }

        static private TimelineNode BuildTimelineTree(BinaryReader reader)
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

    }
}
