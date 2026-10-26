using MolecularSurfaces.Molecules.Proteins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Sequence = MolecularSurfaces.Molecules.Proteins.Sequence;

namespace MolecularSurfaces.Molecules
{
    public struct ProteinBuild
    {
        public Dictionary<char, Sequence> Chain;
        public Residue[] Residues;
        public ProteinAtom[] Atoms;
        public Dictionary<ResidueKey, int> ResidueLookup;
        public ProteinFile File;
    }

    public static class ProteinBuilder
    {
        public static Protein Build(string name, string fileContents, ProteinFileType fileType)
        {
            ProteinBuild _build;
            switch (fileType)
            {
                case ProteinFileType.PQR:
                    _build = BuildPQR(fileContents); break;
                case ProteinFileType.PDB:
                default:
                        _build = BuildPDB(fileContents); break;
            }

            return new Protein(name, _build.File, _build.Chain, _build.ResidueLookup, _build.Atoms, _build.Residues);
        }

        #region PDB
        internal class ResidueBuilder
        {
            public int Id;
            public int ChainSequenceId;
            public List<int> Atoms;
            public ResidueType Type;
            public ResidueCategory Category;
        }


        static Dictionary<char, BuildChain> _Build;
        static int _atomId = 0;
        static int _residueId = 0;
        static int _lineNumber = 0;
        static ProteinBuild BuildPDB(string fileContents)
        {
            using var _reader = new StringReader(fileContents);

            _Build = new Dictionary<char, BuildChain>();
            _atomId = 0;
            _residueId = 0;

            List<Atom> _Atoms = new List<Atom>();
            Atom _lastAtom;

            Dictionary<ResidueKey, ResidueBuilder> _Residuebuilds = new Dictionary<ResidueKey, ResidueBuilder>();

            _lineNumber = 0;
            string _line = "";
            var _fileLines = new List<string>();
            while ((_line = _reader.ReadLine()) != null)
            {
                _fileLines.Add(_line);

                var _recordName = ParseRecordName(_line);
                var _recordType = MapRecordType(_recordName);

                _lineNumber++;

                switch (_recordType)
                {
                    case PdbRecordType.ATOM:
                        ParseAtom(_line, _lineNumber, false, ref _Atoms);
                        break;
                    case PdbRecordType.HETATM:
                        ParseAtom(_line, _lineNumber, true, ref _Atoms);
                        break;
                    case PdbRecordType.MODEL:
                        ParseModel(_line, _lineNumber);
                        break;
                    case PdbRecordType.ENDMDL:
                        ParseEndModel(_line, _lineNumber);
                        break;
                    case PdbRecordType.TER:
                        ParseTer(_line, _lineNumber);
                        break;
                    case PdbRecordType.HEADER:
                        ParseHeader(_line, _lineNumber);
                        break;
                    case PdbRecordType.TITLE:
                        ParseTitle(_line, _lineNumber);
                        break;
                    case PdbRecordType.REMARK:
                        ParseRemark(_line, _lineNumber);
                        break;
                    case PdbRecordType.CONECT:
                        ParseConect(_line, _lineNumber);
                        break;
                    case PdbRecordType.END:
                        ParseEnd(_line, _lineNumber);
                        break;
                    case PdbRecordType.SEQRES:
                        ParseSeqRes(_line, _lineNumber);
                        break;
                    default:
                        ParseUnknown(_line, _lineNumber, _recordName);
                        break;
                }
            }

            var _lookup = new Dictionary<ResidueKey, int>();
            foreach (var _chain in _Build)
            {
                _lookup.AddRange(_chain.Value.ResidueLookup);
            }

            return new ProteinBuild
            {
                Chain = _Build.Select(s => s.Value.RenderSequence()).ToDictionary(x => x.ChainId),
                Residues = _Build.SelectMany(s => s.Value.Residues).Select(s => s.Value.Render()).ToArray(),
                Atoms = _Build.SelectMany(s => s.Value.Residues).SelectMany(s => s.Value.Atoms).Select(a => a.Render()).ToArray(),
                ResidueLookup = _lookup,
                File = new ProteinFile(ProteinFileType.PDB, _fileLines.ToArray())
            };
        }

        private static void ParseAtom(string rawData, int lineNumber, bool isHetAtom, ref List<Atom> _Atoms)
        {
            var _chainId = rawData[21];
            var _residueSequence = int.TryParse(rawData.Substring(22, 4), out var _sequence) ? _sequence : 0;
            var _insertCode = rawData[26];

            var _residueKey = new ResidueKey(_chainId, _residueSequence, _insertCode);

            _Build.TryAdd(_chainId, new BuildChain(_chainId));

            if (_Build[_chainId].Append(_residueId, new BuildResidue(_residueId, _residueKey)))
                _residueId++;

            _Build[_chainId].Residues[_residueKey].Append(new BuildAtom(_atomId++, isHetAtom, rawData, _lineNumber));
        }

        private static void ParseModel(string rawData, int lineNumber) { }
        private static void ParseEndModel(string rawData, int lineNumber) { }
        private static void ParseTer(string rawData, int lineNumber) { }
        private static void ParseHeader(string rawData, int lineNumber) { }
        private static void ParseTitle(string rawData, int lineNumber) { }
        private static void ParseRemark(string rawData, int lineNumber) { }
        private static void ParseConect(string rawData, int lineNumber) { }
        private static void ParseEnd(string rawData, int lineNumber) { }
        private static void ParseSeqRes(string rawData, int lineNumber) { }
        private static void ParseUnknown(string rawData, int lineNumber, string recordName) { }

        private static string ParseRecordName(string rawData)
        {
            if (rawData == null) return "      ";
            if (rawData.Length >= 6) return rawData.Substring(0, 6);
            return rawData.PadRight(6, ' ').Substring(0, 6);
        }

        private static PdbRecordType MapRecordType(string recordName)
        {
            switch (recordName)
            {
                case "ATOM  ": return PdbRecordType.ATOM;
                case "HETATM": return PdbRecordType.HETATM;
                case "MODEL ": return PdbRecordType.MODEL;
                case "ENDMDL": return PdbRecordType.ENDMDL;
                case "TER   ": return PdbRecordType.TER;
                case "HEADER": return PdbRecordType.HEADER;
                case "TITLE ": return PdbRecordType.TITLE;
                case "REMARK": return PdbRecordType.REMARK;
                case "CONECT": return PdbRecordType.CONECT;
                case "END   ": return PdbRecordType.END;
                case "SEQRES": return PdbRecordType.SEQRES;
                default: return PdbRecordType.UNKNOWN;
            }
        }

        private enum PdbRecordType
        {
            UNKNOWN = 0,
            ATOM,
            HETATM,
            MODEL,
            ENDMDL,
            TER,
            HEADER,
            TITLE,
            REMARK,
            CONECT,
            END,
            SEQRES
        }

        #endregion - PDB

        #region PQR

        static ProteinBuild BuildPQR(string fileContents)
        {
            using var _reader = new StringReader(fileContents);

            _Build = new Dictionary<char, BuildChain>();
            _atomId = 0;
            _residueId = 0;

            List<ProteinAtom> _Atoms = new List<ProteinAtom>();
            ProteinAtom _lastAtom;

            Dictionary<ResidueKey, ResidueBuilder> _Residuebuilds = new Dictionary<ResidueKey, ResidueBuilder>();

            _lineNumber = 0;
            string _line = "";
            while ((_line = _reader.ReadLine()) != null)
            {
                _lineNumber++;
                var _recordName = ParseRecordName(_line);
                var _recordType = MapRecordType(_recordName);

                switch (_recordType)
                {
                    case PdbRecordType.ATOM:
                        ParseAtomPQR(_line, _lineNumber, false, ref _Atoms);
                        break;
                    default:
                        break;
                }
            }

            return new ProteinBuild
            {
                Chain = new Dictionary<char, Sequence>(),
                Residues = new Residue[1]
                {
                    new Residue(0, new ResidueKey('A',0,' '), 
                    _Atoms.Select(s => s.Id).ToArray(), ResidueType.Unknown, ResidueCategory.Unknown)
                },
                Atoms = _Atoms.ToArray(),
                ResidueLookup = new Dictionary<ResidueKey, int>(),
            };
        }

        private static void ParseAtomPQR(string rawData, int lineNumber, bool isHetAtom, ref List<ProteinAtom> _Atoms)
        {
            var _values = rawData.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            _Atoms.Add(new ProteinAtom
            (
                new Atom(
                    ElementParser.Parse(_values[2].Trim()).ToString(), 
                    ElementParser.Parse(_values[2].Trim()),
                    new Vector3(
                        (float.TryParse(_values[5], out var _x) ? _x : 0),
                        (float.TryParse(_values[6], out var _y) ? _y : 0),
                        (float.TryParse(_values[7], out var _z) ? _z : 0)
                    ),
                    _atomId++),
                lineNumber,       
                false,
                float.TryParse(_values[9], out var _radius) ? _radius / 2f : 0
            ));
        }

        #endregion
    }

    internal class BuildChain
    {
        public char Id;
        public Dictionary<ResidueKey, BuildResidue> Residues;
        public Dictionary<ResidueKey, int> ResidueLookup;

        public BuildChain(char id)
        {
            Id = id;
            Residues = new Dictionary<ResidueKey, BuildResidue>();
            ResidueLookup = new Dictionary<ResidueKey, int>();
        }

        public bool Append(int index, BuildResidue residue)
        {
            if (Residues.TryAdd(residue.Key, residue))
            {
                ResidueLookup.TryAdd(residue.Key, residue.Index);
                return true;
            }
            return false;
        }

        public Sequence RenderSequence()
        {
            return new Sequence(ResidueLookup, Residues.Values.ToDictionary(d => d.Index, x => x.Type));
        }
    }

    internal class BuildResidue
    {
        public int Index;
        public ResidueKey Key;
        public List<BuildAtom> Atoms;
        public ResidueType Type;

        public BuildResidue(int index, ResidueKey key)
        {
            Index = index;
            Key = key;
            Type = ResidueType.Unknown;
            Atoms = new List<BuildAtom>();
        }

        public void Append(BuildAtom atom)
        {
            if (Type == ResidueType.Unknown)
                Type = ResidueParser.Parse(atom.RawData.Substring(17, 3).Trim());

            Atoms.Add(atom);
        }

        public Residue Render()
        {
            return new Residue(Index, Key, Atoms.Select(s => s.Index).ToArray(), Type, Type.GetCategory());
        }
    }

    internal class BuildAtom
    {
        public int Index;
        public bool IsHetAtom;
        public string RawData;
        public int FileLineRef;

        public BuildAtom(int index, bool isHetAtom, string rawData, int fileLineRef)
        {
            Index = index;
            IsHetAtom = isHetAtom;
            RawData = rawData;
            FileLineRef = fileLineRef;
        }

        public ProteinAtom Render()
        {
            return new ProteinAtom(
                new Atom(
                    RawData.Substring(13, 3).Trim().ToUpper(), 
                    ElementParser.Parse(RawData.Substring(76, 2).Trim().ToLower()), new Vector3(
                        (float.TryParse(RawData.Substring(30, 8), out var _x) ? _x : 0),
                        (float.TryParse(RawData.Substring(38, 8), out var _y) ? _y : 0),
                        (float.TryParse(RawData.Substring(46, 8), out var _z) ? _z : 0)
                        ), 
                    Index),
                FileLineRef,
                IsHetAtom
                );
        }
    }
}
