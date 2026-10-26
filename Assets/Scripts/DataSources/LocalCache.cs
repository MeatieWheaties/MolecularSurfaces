using MolecularSurfaces.Molecules;
using MolecularSurfaces.Molecules.Proteins;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MolecularSurfaces.DataSources
{
    public static class LocalCache
    {
        public static string Root = "Assets\\Data\\";
        public static string DefaultCachePath = "Assets\\Data\\Cache\\";

        public static string[] GetFilesWithExtension(string subPath, string extension)
        {
            // Allow "pdb" or ".pdb"
            extension = extension.TrimStart('.');

            return Directory.GetFiles(Root + subPath, $"*.{extension}");
        }

        public static Protein[] LoadProteins(string subPath, ProteinFileType type)
        {
            string extension = GetExtension(type);
            var files = GetFilesWithExtension(subPath, extension);
            return files.Select(file => LoadProtein(subPath, Path.GetFileName(file), type)).Where(protein => protein != null).ToArray();
        }

        public static Protein LoadProtein(string fileName, ProteinFileType type)
        {
            string fileContent = LoadFile(DefaultCachePath, fileName, type);
            if (string.IsNullOrEmpty(fileContent))
                return null;
            return ProteinBuilder.Build(fileName, fileContent, type);
        }

        public static Protein LoadProtein(string subPath, string fileName, ProteinFileType type)
        {
            string fileContent = LoadFile(Root + subPath, fileName, type);
            if (string.IsNullOrEmpty(fileContent))
                return null;
            return ProteinBuilder.Build(fileName, fileContent, type);
        }

        private static string LoadFile(string folderPath, string fileName, ProteinFileType type)
        {
            string extension = GetExtension(type);

            if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                fileName += extension;

            string fullPath = Path.Combine(folderPath, fileName);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"File not found: {fullPath}");
                return string.Empty;
            }

            return File.ReadAllText(fullPath);
        }

        private static string GetExtension(ProteinFileType type)
        {
            return type switch
            {
                ProteinFileType.PDB => ".pdb",
                ProteinFileType.mmCIF => ".cif",
                ProteinFileType.PQR => ".pqr",
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }
    }
}
