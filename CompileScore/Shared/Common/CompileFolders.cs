using System.Collections.Generic;
using System.Data;
using System.IO;

namespace CompileScore
{
    public class CompileFolder
    {
        public string Name { set; get; }
        public int Parent { set; get; } = -1;
        public List<int> Children { set; get; }
        public List<UnitValue> Units { set; get; }
        public List<CompileValue> Includes { set; get; }
    }

    public class CompileFolders
    {
        private List<CompileFolder> Folders { set; get; } = new List<CompileFolder>();
        private Dictionary<UnitValue, CompileFolder>    UnitsDictionary { set; get; } = new Dictionary<UnitValue, CompileFolder>();
        private Dictionary<CompileValue, CompileFolder> IncludesDictionary { set; get; } = new Dictionary<CompileValue, CompileFolder>();

        private string BuildPathNameUp(CompileFolder folder, string rightHand)
        {
            string thisPath = folder.Name.Length > 0 ? folder.Name + '/' + rightHand : rightHand;
            return folder.Parent >= 0 ? BuildPathNameUp(Folders[folder.Parent],thisPath) : thisPath;
        }

        public string GetUnitPath(UnitValue unit)
        {
            if (unit == null || !UnitsDictionary.ContainsKey(unit))
            {
                return null;
            }

            return BuildPathNameUp(UnitsDictionary[unit],unit.Name); 
        }

        public string GetValuePath(CompileValue value)
        {
            if (value == null || !IncludesDictionary.ContainsKey(value))
            {
                return null;
            }

            return BuildPathNameUp(IncludesDictionary[value], value.Name);
        }

        public string GetUnitPathSafe(UnitValue unit)
        {
            string fullPath = GetUnitPath(unit);
            return fullPath == null ? "" : fullPath;
        }

        public string GetValuePathSafe(CompileValue value)
        {
            string fullPath = GetValuePath(value);
            return fullPath == null ? "" : fullPath;
        }

        private CompileFolder GetFolderFromPathRecursive(CompileFolder node, string[] directories, int index)
        {
            if (directories.Length == (index + 1))
            {
                //found the folder
                return node;
            }

            string thisName = directories[index];
            foreach (int childrenIndex in node.Children)
            {
                if (childrenIndex < Folders.Count && Folders[childrenIndex].Name == thisName)
                {
                    return GetFolderFromPathRecursive(Folders[childrenIndex], directories, index + 1);
                }
            }

            return null;
        }

        private CompileFolder GetFolderFromPath(string[] directories)
        {
            if (Folders != null && Folders.Count > 0 && directories.Length > 1)
            {
                return GetFolderFromPathRecursive(Folders[0], directories, 0);
            }
            return null;
        }

        public UnitValue GetUnitByPath(string path)
        {
            if (path != null)
            {
                string[] directories = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                CompileFolder folder = GetFolderFromPath(directories);
                if (folder != null)
                {
                    string filename = directories[directories.Length - 1];

                    foreach (UnitValue unit in folder.Units)
                    {
                        if (unit.Name == filename)
                        {
                            return unit;
                        }
                    }
                }
            }
            return null;
        }

        public CompileValue GetValueByPath(CompilerData.CompileCategory category, string path)
        {
            if (path != null && category == CompilerData.CompileCategory.Include)
            {
                string[] directories = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                CompileFolder folder = GetFolderFromPath(directories);
                if (folder != null)
                {
                    string filename = directories[directories.Length - 1];

                    foreach (CompileValue value in folder.Includes)
                    {
                        if (value.Name == filename)
                        {
                            return value;
                        }
                    }
                }
            }
            return null;
        }

        private void ReadFolder(BinaryReader reader, List<CompileFolder> list, List<UnitValue> units, CompileDataset[] datasets)
        {
            var folder = new CompileFolder();

            folder.Name = reader.ReadString();

            uint countChildren = reader.ReadUInt32();
            if (countChildren >= 0)
            {
                folder.Children = new List<int>();
                for (uint i = 0; i < countChildren; ++i)
                {
                    folder.Children.Add((int)reader.ReadUInt32());
                }
            }

            uint countUnits = reader.ReadUInt32();
            if (countUnits >= 0)
            {
                folder.Units = new List<UnitValue>();
                for (uint i = 0; i < countUnits; ++i)
                {
                    folder.Units.Add(CompilerData.GetUnitByIndex(reader.ReadUInt32(), units));
                }
            }

            uint countIncludes = reader.ReadUInt32();
            if (countIncludes >= 0)
            {
                folder.Includes = new List<CompileValue>();
                for (uint i = 0; i < countIncludes; ++i)
                {
                    folder.Includes.Add(CompilerData.GetValue(CompilerData.CompileCategory.Include, (int)reader.ReadUInt32(), datasets));
                }
            }

            list.Add(folder);
        }

        public void FinalizeFolders()
        {
            for (int parentIndex = 0; parentIndex < Folders.Count; ++parentIndex)
            {
                //Set parent index
                CompileFolder folder = Folders[parentIndex];
                for (int childIndex = 0; childIndex < folder.Children.Count; ++childIndex)
                {
                    Folders[folder.Children[childIndex]].Parent = parentIndex;
                }

                //setup acceleration structures
                foreach (UnitValue unitValue in folder.Units)
                {
                    UnitsDictionary[unitValue] = folder;
                }

                foreach (CompileValue compileValue in folder.Includes)
                {
                    IncludesDictionary[compileValue] = folder;
                }
            }
        }

        public void ReadFolders(BinaryReader reader, List<UnitValue> units, CompileDataset[] datasets)
        {
            //Reset data
            UnitsDictionary.Clear();
            IncludesDictionary.Clear();

            //Read Folders
            uint foldersLength = reader.ReadUInt32();
            Folders = new List<CompileFolder>((int)foldersLength);
            for (uint i = 0; i < foldersLength; ++i)
            {
                ReadFolder(reader, Folders, units, datasets);
            }

            FinalizeFolders();
        }
    }
}
