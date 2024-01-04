using CompileScore.Common;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;

namespace CompileScore
{
    namespace ParserEnums
    {
        public enum LinkStrength
        {
            [Common.BasicUILabel(Label = "None")]
            None = 0, //No dependencies present
            [Common.BasicUILabel(Label = "Minimal")]
            Minimal, // Can be removed by just forward declaring or adding minimal code
            [Common.BasicUILabel(Label = "Weak")]
            Weak,    // there is a real dependency but it can be easily improved if needed
            [Common.BasicUILabel(Label = "Medium")]
            Medium,  // Can be removed but it might need more coding
            [Common.BasicUILabel(Label = "Strong")]
            Strong,  // This dependency is mandatory 
        }

        //important keep this enumerations in sync with the Parser code
        public enum GlobalRequirement
        {
            [Common.RequirementLabel(Strength = LinkStrength.Medium,    Label = "Macro Expansion", Short ="M")]
            MacroExpansion = 0,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,      Label = "Free Function",   Short = "FF")]
            FreeFunctionCall,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,      Label = "Free Variable", Short = "FV")]
            FreeVariable,

            [Common.RequirementLabel(Strength = LinkStrength.Minimal,   Label = "Enumeration", Short = "E")]
            EnumInstance,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,      Label = "Enumeration Constant", Short = "EI")]
            EnumConstant,

            [Common.RequirementLabel(Strength = LinkStrength.Minimal,   Label = "Forward Declaration", Short = "FD")]
            ForwardDeclaration,
            [Common.RequirementLabel(Strength = LinkStrength.Minimal,   Label = "Type Definition", Short = "TD")]
            TypeDefinition,

            Count
        }

        public enum StructureSimpleRequirement
        {
            [Common.RequirementLabel(Strength = LinkStrength.Weak,    Label = "Instance", Short = "I")]
            Instance = 0,
            [Common.RequirementLabel(Strength = LinkStrength.Minimal, Label = "Reference", Short = "&")]
            Reference,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,    Label = "Allocation", Short = "N")]
            Allocation,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,    Label = "Destruction", Short = "D")]
            Destruction,
            [Common.RequirementLabel(Strength = LinkStrength.Strong,  Label = "Inheritance", Short = "I")]
            Inheritance,
            [Common.RequirementLabel(Strength = LinkStrength.Strong,  Label = "Member Field", Short = "F")]
            MemberField,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,    Label = "Function Argument", Short = "A")]
            FunctionArgument,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,    Label = "Function Return", Short = "R")]
            FunctionReturn,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,    Label = "Cast", Short = "C")]
            Cast,

            Count
        }

        public enum StructureNamedRequirement
        {
            [Common.RequirementLabel(Strength = LinkStrength.Medium, Label = "Method Call", Short = "M")]
            MethodCall = 0,
            [Common.RequirementLabel(Strength = LinkStrength.Medium, Label = "Field Access", Short = "F")]
            FieldAccess,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,   Label = "Static Method Call", Short = "SM")]
            StaticCall,
            [Common.RequirementLabel(Strength = LinkStrength.Weak,   Label = "Static Field Access", Short = "SF")]
            StaticAccess,

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

        public ParserEnums.LinkStrength Strength { set; get; } = ParserEnums.LinkStrength.None;

        public int LinkTypeFlags { set; get; } = 0;
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

            ParserUnit parserUnit = ReadUnitFile(fullPath);
            LinkUnit(parserUnit);
        }

        public ParserUnit GetParserUnit(string mainPath)
        {
            if (mainPath != null && Units.ContainsKey(mainPath))
            {
                return Units[mainPath];
            }

            return null;
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
                OutputLog.Log("Unable to figure out the source file path from the parser results.", OutputLog.PaneInstance.Parser);
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

        public static ParserUnit ReadUnitFile(string fullPath)
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
                        ReadFilesRequirements(reader, chunk);

                        //Read includes
                        ReadIncludes(reader, chunk);
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

        public static uint DecodeInnerFileLine(ulong location)
        {
            return (uint)(location >> 32);
        }

        public static uint DecodeInnerFileColumn(ulong location)
        {
            return (uint)(location & 0xffff);
        }

        private static int ConvertToFlag(object value)
        {
            if (value is ParserEnums.GlobalRequirement)
            {
                return 1 << (int)value;
            }
            if (value is ParserEnums.StructureSimpleRequirement)
            {
                return 1 << ((int)ParserEnums.GlobalRequirement.Count + (int)value);
            }
            if (value is ParserEnums.StructureNamedRequirement)
            {
                return 1 << ((int)ParserEnums.GlobalRequirement.Count + (int)ParserEnums.StructureSimpleRequirement.Count + (int)value);
            }
            return 0;
        }

        public static bool HasLinkFlag(ParserFileRequirements file, object value)
        {
            return ( file.LinkTypeFlags & ConvertToFlag(value) ) != 0;
        }

        private static void AddLinkFlag(ParserFileRequirements file, object value)
        {
            file.LinkTypeFlags |= ConvertToFlag(value);

            ParserEnums.LinkStrength strength = ParserEnums.LinkStrength.None;
            if (value is ParserEnums.GlobalRequirement)
            {
                strength = RequirementLabel.GetStrength((ParserEnums.GlobalRequirement)value);
            }
            if (value is ParserEnums.StructureSimpleRequirement)
            {
                strength = RequirementLabel.GetStrength((ParserEnums.StructureSimpleRequirement)value);
            }
            if (value is ParserEnums.StructureNamedRequirement)
            {
                strength = RequirementLabel.GetStrength((ParserEnums.StructureNamedRequirement)value);
            }

            //TODO ~ ramonv ~ if this is within a context of a source file... snap weak and medium together 

            file.Strength = (int)strength > (int)file.Strength ? strength : file.Strength;
        }

        private static void ReadCodeRequirements(BinaryReader reader, List<ParserCodeRequirement> list)
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

        private static void ReadStructureRequirement(BinaryReader reader, ParserFileRequirements file, List<ParserStructureRequirement> list)
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
                    AddLinkFlag(file, (ParserEnums.StructureSimpleRequirement)i);
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
                    AddLinkFlag(file, (ParserEnums.StructureNamedRequirement)i);
                    entry.Named[i] = new List<ParserCodeRequirement>((int)reqLength);
                    for (uint reqIndex = 0; reqIndex < reqLength; ++reqIndex)
                    {
                        ReadCodeRequirements(reader, entry.Named[i]);
                    }
                }
            }

            list.Add(entry);
        }

        private static void ReadFileRequirement(BinaryReader reader, ParserUnit unit)
        {
            ParserFileRequirements entry = new ParserFileRequirements();
            entry.Name = reader.ReadString();

            for (int i = 0; i < (int)ParserEnums.GlobalRequirement.Count; ++i)
            {
                uint reqLength = reader.ReadUInt32();
                if (reqLength > 0)
                {
                    AddLinkFlag(entry, (ParserEnums.GlobalRequirement)i);
                    entry.Global[i] = new List<ParserCodeRequirement>((int)reqLength);
                    for (uint reqIndex = 0; reqIndex < reqLength; ++reqIndex)
                    {
                        ReadCodeRequirements(reader, entry.Global[i]);
                    }
                }

            }

            uint structsLength = reader.ReadUInt32();
            if (structsLength > 0)
            {
                entry.Structures = new List<ParserStructureRequirement>((int)structsLength);
                for (uint i = 0; i < structsLength; ++i)
                {
                    ReadStructureRequirement(reader, entry, entry.Structures);
                }
            }

            unit.Files.Add(entry);

            string fileName = EditorUtils.GetFileNameSafe(entry.Name);
            if (fileName != null)
            {
                unit.FilesMap[fileName.ToLower()] = entry;
            }
        }

        private static void ReadFilesRequirements(BinaryReader reader, ParserUnit unit)
        {
            uint filesLength = reader.ReadUInt32();
            unit.Files = new List<ParserFileRequirements>((int)filesLength);
            unit.FilesMap = new Dictionary<string, ParserFileRequirements>();
            for (uint i = 0; i < filesLength; ++i)
            {
                ReadFileRequirement(reader, unit);
            } 
        }

        private static void ReadIncludes(BinaryReader reader, ParserUnit unit)
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

        public static Requirements.RequirementsWindow FocusRequirementsWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            Requirements.RequirementsWindow window = CompilerData.Instance.Package.FindToolWindow(typeof(Requirements.RequirementsWindow), 0, true) as Requirements.RequirementsWindow;
            if ((null == window) || (null == window.GetFrame()))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            window.ProxyShow();

            return window;
        }

    }
}
