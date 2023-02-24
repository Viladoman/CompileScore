using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CompileScore
{
    public class CompileFolder
    {
        public string Name { set; get; }
        public List<int> Children { set; get; }
        public List<UnitValue> Units { set; get; }
        public List<CompileValue> Includes { set; get; }
    }

    public class CompileFolders
    {
        private List<CompileFolder> Folders { set; get; } = new List<CompileFolder>();

        private string GetUnitPathRecursive(UnitValue unit, CompileFolder node, string fullpath)
        {
            foreach (UnitValue value in node.Units)
            {
                if (value == unit)
                {
                    return fullpath + value.Name;
                }
            }

            foreach (int childrenIndex in node.Children)
            {
                if (childrenIndex < Folders.Count)
                {
                    CompileFolder folder = Folders[childrenIndex];
                    string result = GetUnitPathRecursive(unit, folder, fullpath + folder.Name + '/');
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }
        private string GetIncludePathRecursive(CompileValue value, CompileFolder node, string fullpath)
        {
            foreach (CompileValue thisValue in node.Includes)
            {
                if (thisValue == value)
                {
                    return fullpath + thisValue.Name;
                }
            }

            foreach (int childrenIndex in node.Children)
            {
                if (childrenIndex < Folders.Count)
                {
                    CompileFolder folder = Folders[childrenIndex];
                    string result = GetIncludePathRecursive(value, folder, fullpath + folder.Name + '/');
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        public string GetUnitPath(UnitValue unit)
        {
            if (unit != null && Folders != null && Folders.Count > 0)
            {
                return GetUnitPathRecursive(unit, Folders[0], "");
            }
            return null;
        }

        public string GetValuePath(CompilerData.CompileCategory category, CompileValue value)
        {
            if (value != null && Folders != null && Folders.Count > 0 && category == CompilerData.CompileCategory.Include)
            {
                return GetIncludePathRecursive(value, Folders[0], "");
            }
            return null;
        }

        public string GetUnitPathSafe(UnitValue unit)
        {
            string fullPath = GetUnitPath(unit);
            return fullPath == null ? "" : fullPath;
        }

        public string GetValuePathSafe(CompilerData.CompileCategory category, CompileValue value)
        {
            string fullPath = GetValuePath(category, value);
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

        public void ReadFolders(BinaryReader reader, List<UnitValue> units, CompileDataset[] datasets)
        {
            //Read Folders
            uint foldersLength = reader.ReadUInt32();
            var folderList = new List<CompileFolder>((int)foldersLength);
            for (uint i = 0; i < foldersLength; ++i)
            {
                ReadFolder(reader, folderList, units, datasets);
            }
            Folders = new List<CompileFolder>(folderList);
        }
    }
}
