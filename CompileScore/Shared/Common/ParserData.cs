using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace CompileScore
{
    namespace ParserEnums
    {
        //important keep this enumerations in sync with the Parser code
        public enum GlobalRequirement
        {
            MacroExpansion = 0,
            FreeFunctionCall,
            FreeVariable,

            EnumInstance,
            EnumConstant,

            ForwardDeclaration,
            TypeDefinition,

            Count
        }

        public enum StructureSimpleRequirement
        {
            Instance = 0,
            Reference,
            Allocation,
            Inheritance,
            MemberField,
            FunctionArgument,
            FunctionReturn,

            Count
        }

        public enum StructureNamedRequirement
        {
            MethodCall = 0,
            FieldAccess,

            Count
        };
    }

    public class ParserCodeRequirement
    {
        public string Name { set; get; } = "???";

        public ulong DefinitionLocation { set; get; } = 0;

        public List<ulong> UseLocations { set; get; }
    }

    public class ParserStructureRequirement
    {
        public string Name { set; get; } = "???";

        public ulong DefinitionLocation { set; get; } = 0;

        public List<ulong>[]                 Simple { set; get; } = new List<ulong>[(int)ParserEnums.StructureSimpleRequirement.Count];
        public List<ParserCodeRequirement>[] Named { set; get; } = new List<ParserCodeRequirement>[(int)ParserEnums.StructureNamedRequirement.Count];
    }


    public class ParserFileRequirements
    {
        public string Name { set; get; }
        public List<ParserCodeRequirement>[] Global { set; get; } = new List<ParserCodeRequirement>[(int)ParserEnums.GlobalRequirement.Count];
        public List<ParserStructureRequirement> Structures { set; get; }
        public List<ParserFileRequirements> Includes { set; get; }
    }

    public class ParserUnit
    {
        public string Filename { set; get; }
        public List<ParserFileRequirements> Files { set; get; }
        public List<ParserFileRequirements> DirectIncludes { set; get; }
        public Dictionary<string, ParserFileRequirements> FilesMap { set; get; }

    }

    public sealed class ParserData
    {
        private static readonly Lazy<ParserData> lazy = new Lazy<ParserData>(() => new ParserData());
        public static ParserData Instance { get { return lazy.Value; } }

        public const uint VERSION = 2;

        private Dictionary<string, ParserUnit> Units = new Dictionary<string, ParserUnit>();

        public void LoadUnitFile(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //TODO ~ ramonv ~ fork here ( but first figure out how to avoid another request to stomp the tmpresult file while doing this )
            ParserUnit parserUnit = ReadUnitFile(fullPath);
            LinkUnit(parserUnit);
            //TODO ~ NotifyUnitLoaded
        }

        public ParserFileRequirements GetFileRequirements(string mainPath, string filename)
        {
            if (Units.ContainsKey(mainPath))
            {
                ParserUnit unit = Units[mainPath];
                if (unit.FilesMap.ContainsKey(filename))
                {
                    return unit.FilesMap[filename];
                }
            }

            return null;
        }

        private void LinkUnit(ParserUnit parserUnit)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Add this to the instance dictionary
            if (parserUnit.Filename == null)
            {
                Parser.Log("Unable to figure out the source file path from the parser results.");
                return;
            }

            Units[parserUnit.Filename.ToLower()] = parserUnit;
        }
        private static bool CheckVersion(uint version)
        {
            if (version != VERSION)
            {
                _ = OutputLog.ErrorGlobalAsync("Trying to load an unsupported file Version! Expected version " + VERSION + " - Found " + version + " - The Parser tool is out of sync.", OutputLog.PaneInstance.Parser);
                return false;
            }
            return true;
        }

        private static ParserUnit ReadUnitFile(string fullPath)
        {
            ParserUnit chunk = new ParserUnit();
            if (File.Exists(fullPath))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    // Read version
                    uint version = reader.ReadUInt32();
                    if (CheckVersion(version))
                    {
                        //Read Main data
                        chunk.Filename = reader.ReadString();
                        chunk.Filename = chunk.Filename.Length == 0 ? null : chunk.Filename;

                        //Read Files 
                        ReadFilesRequirements(reader, version, chunk);

                        //Read includes
                        ReadIncludes(reader, version, chunk);
                    }
                }

                fileStream.Close();

                watch.Stop();
                const long TicksPerMicrosecond = (TimeSpan.TicksPerMillisecond / 1000);
                ulong microseconds = (ulong)(watch.ElapsedTicks / TicksPerMicrosecond);
                _ = OutputLog.LogGlobalAsync("Parse result file processed in " + Common.UIConverters.GetTimeStr(microseconds), OutputLog.PaneInstance.Parser);
            }

            return chunk;
        }

        private static ulong EncodeInnerFileLocation(uint line, uint col)
        {
            return ((ulong)line << 32) | (ulong)col;
        }

        private static uint DecodeInnerFileLine(ulong location)
        {
            return (uint)(location >> 32);
        }

        private static uint DecodeInnerFileColumn(ulong location)
        {
            return (uint)(location & 0xffff);
        }

        private static void ReadCodeRequirements(BinaryReader reader, uint version, List<ParserCodeRequirement> list)
        {
            ParserCodeRequirement entry = new ParserCodeRequirement();
            entry.Name = reader.ReadString();

            uint defLine = reader.ReadUInt32();
            uint defCol = reader.ReadUInt32();
            entry.DefinitionLocation = EncodeInnerFileLocation(defLine, defCol);

            uint useLength = reader.ReadUInt32();
            if (useLength > 0)
            {
                entry.UseLocations = new List<ulong>((int)useLength);
                for (uint reqIndex = 0; reqIndex < useLength; ++reqIndex)
                {
                    uint useLine = reader.ReadUInt32();
                    uint useCol = reader.ReadUInt32();
                    entry.UseLocations.Add(EncodeInnerFileLocation(useLine, useCol));
                }
            }

            list.Add(entry);
        }

        private static void ReadStructureRequirement(BinaryReader reader, uint version, List<ParserStructureRequirement> list)
        {
            ParserStructureRequirement entry = new ParserStructureRequirement();
            entry.Name = reader.ReadString();

            uint defLine = reader.ReadUInt32();
            uint defCol = reader.ReadUInt32();
            entry.DefinitionLocation = EncodeInnerFileLocation(defLine, defCol);

            for (int i = 0; i < (int)ParserEnums.StructureSimpleRequirement.Count; ++i)
            {
                uint reqLength = reader.ReadUInt32();
                if (reqLength > 0)
                {
                    entry.Simple[i] = new List<ulong>((int)reqLength);
                    for (uint reqIndex = 0; reqIndex < reqLength; ++reqIndex)
                    {
                        uint useLine = reader.ReadUInt32();
                        uint useCol = reader.ReadUInt32();
                        entry.Simple[i].Add(EncodeInnerFileLocation(useLine, useCol));
                    }
                }
            }

            for (int i = 0; i < (int)ParserEnums.StructureNamedRequirement.Count; ++i)
            {
                uint reqLength = reader.ReadUInt32();
                if (reqLength > 0)
                {
                    entry.Named[i] = new List<ParserCodeRequirement>((int)reqLength);
                    for (uint reqIndex = 0; reqIndex < reqLength; ++reqIndex)
                    {
                        ReadCodeRequirements(reader, version, entry.Named[i]);
                    }
                }
            }

            list.Add(entry);
        }

        private static void ReadFileRequirement(BinaryReader reader, uint version, ParserUnit unit)
        {
            ParserFileRequirements entry = new ParserFileRequirements();
            entry.Name = reader.ReadString();

            for (int i = 0; i < (int)ParserEnums.GlobalRequirement.Count; ++i)
            {
                uint reqLength = reader.ReadUInt32();
                if (reqLength > 0)
                {
                    entry.Global[i] = new List<ParserCodeRequirement>((int)reqLength);
                    for (uint reqIndex = 0; reqIndex < reqLength; ++reqIndex)
                    {
                        ReadCodeRequirements(reader, version, entry.Global[i]);
                    }
                }

            }

            uint structsLength = reader.ReadUInt32();
            if (structsLength > 0)
            {
                entry.Structures = new List<ParserStructureRequirement>((int)structsLength);
                for (uint i = 0; i < structsLength; ++i)
                {
                    ReadStructureRequirement(reader, version, entry.Structures);
                }
            }

            unit.Files.Add(entry);

            string fileName = EditorUtils.GetFileNameSafe(entry.Name);
            if (fileName != null)
            {
                unit.FilesMap[fileName.ToLower()] = entry;
            }
        }

        private static void ReadFilesRequirements(BinaryReader reader, uint version, ParserUnit unit)
        {
            uint filesLength = reader.ReadUInt32();
            unit.Files = new List<ParserFileRequirements>((int)filesLength);
            unit.FilesMap = new Dictionary<string, ParserFileRequirements>();
            for (uint i = 0; i < filesLength; ++i)
            {
                ReadFileRequirement(reader, version, unit);
            } 
        }

        private static void ReadIncludes(BinaryReader reader, uint version, ParserUnit unit)
        {
            int numFiles = unit.Files.Count;

            uint directIncludesLength = reader.ReadUInt32();
            unit.DirectIncludes = new List<ParserFileRequirements>((int)directIncludesLength);
            for (uint i = 0; i < directIncludesLength; ++i)
            {
                uint index = reader.ReadUInt32();
                if (index < numFiles)
                {
                    unit.DirectIncludes.Add(unit.Files[(int)index]);
                }
            }

            uint indirectIncludesLength = reader.ReadUInt32();
            for (uint i = 0; i < indirectIncludesLength; ++i)
            {
                uint includerIndex = reader.ReadUInt32();
                uint includeeIndex = reader.ReadUInt32();
                if (includerIndex < numFiles && includeeIndex < numFiles)
                {
                    ParserFileRequirements includerFile = unit.Files[(int)includerIndex];

                    if (includerFile.Includes == null)
                    {
                        includerFile.Includes = new List<ParserFileRequirements>();
                    }

                    includerFile.Includes.Add(unit.Files[(int)includeeIndex]);
                }
            }

        }

    }
}
