using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PixelRoad.Data
{
    public static class SpotCsvParser
    {
        public static List<SpotDefinition> Parse(string csv, float fallbackRadiusMeters)
        {
            List<SpotDefinition> spots = new List<SpotDefinition>();
            if (string.IsNullOrWhiteSpace(csv))
            {
                return spots;
            }

            List<string[]> rows = ParseRows(csv);
            if (rows.Count <= 1)
            {
                return spots;
            }

            Dictionary<string, int> headers = BuildHeaderIndex(rows[0]);
            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string id = Read(row, headers, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                double latitude = ReadDouble(row, headers, "latitude");
                double longitude = ReadDouble(row, headers, "longitude");
                float radius = ReadFloat(row, headers, "radiusMeters", fallbackRadiusMeters);
                spots.Add(new SpotDefinition(
                    id.Trim(),
                    Read(row, headers, "name", id).Trim(),
                    Read(row, headers, "description").Trim(),
                    Read(row, headers, "category", "spot").Trim(),
                    latitude,
                    longitude,
                    radius <= 0f ? fallbackRadiusMeters : radius,
                    Read(row, headers, "icon", "default").Trim(),
                    ReadBool(row, headers, "initialUnlocked")));
            }

            return spots;
        }

        private static Dictionary<string, int> BuildHeaderIndex(string[] headerRow)
        {
            Dictionary<string, int> headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headerRow.Length; i++)
            {
                string header = headerRow[i].Trim();
                if (!headers.ContainsKey(header))
                {
                    headers.Add(header, i);
                }
            }

            return headers;
        }

        private static List<string[]> ParseRows(string csv)
        {
            List<string[]> rows = new List<string[]>();
            List<string> row = new List<string>();
            StringBuilder cell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                }
                else if ((ch == '\n' || ch == '\r') && !inQuotes)
                {
                    if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    {
                        i++;
                    }

                    row.Add(cell.ToString());
                    cell.Length = 0;
                    if (!IsEmptyRow(row))
                    {
                        rows.Add(row.ToArray());
                    }

                    row.Clear();
                }
                else
                {
                    cell.Append(ch);
                }
            }

            row.Add(cell.ToString());
            if (!IsEmptyRow(row))
            {
                rows.Add(row.ToArray());
            }

            return rows;
        }

        private static bool IsEmptyRow(List<string> row)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string Read(string[] row, Dictionary<string, int> headers, string key, string fallback = "")
        {
            if (!headers.TryGetValue(key, out int index) || index < 0 || index >= row.Length)
            {
                return fallback;
            }

            return row[index];
        }

        private static double ReadDouble(string[] row, Dictionary<string, int> headers, string key)
        {
            string value = Read(row, headers, key);
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result);
            return result;
        }

        private static float ReadFloat(string[] row, Dictionary<string, int> headers, string key, float fallback)
        {
            string value = Read(row, headers, key);
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }

            return fallback;
        }

        private static bool ReadBool(string[] row, Dictionary<string, int> headers, string key)
        {
            string value = Read(row, headers, key).Trim();
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("y", StringComparison.OrdinalIgnoreCase);
        }
    }
}
