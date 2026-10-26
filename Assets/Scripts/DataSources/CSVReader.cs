using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MolecularSurfaces.DataSources
{
    public static class CSVReader
    {
        public static CSVFile Load(string directory, string fileName)
        {
            var _lines = File.ReadAllLines(Path.Combine(LocalCache.Root, directory, fileName + ".csv"));

            if (_lines.Length == 0) return new CSVFile();

            var _columns = _lines[0].Split(',').Select(s => s.Trim()).ToArray();

            var _rows = new List<CSVRow>();
            for(int x = 1; x < _lines.Length; x++)
            {
                var _rowData = _lines[x].Split(',').Select(s => s.Trim()).ToArray();
                var _data = new Dictionary<string, string>();
                for(int _r = 0; _r < _columns.Length; _r++)
                {
                    _data.Add(_columns[_r].Trim(), _rowData[_r]);
                }

                _rows.Add(new CSVRow
                {
                    Index = x - 1,
                    Data = _data
                });
            }

            return new CSVFile
            {
                Columns = _columns,
                Rows = _rows.ToArray()
            };
        }
    }

    public class CSVFile
    {
        public string[] Columns;
        public CSVRow[] Rows;
    }

    public struct CSVRow
    {
        public int Index;
        public Dictionary<string, string> Data;
    }
}
