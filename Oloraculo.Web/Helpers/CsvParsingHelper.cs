using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Oloraculo.Web.Helpers
{
    public static class CsvParsingHelper
    {
        public static List<TRecord> ReadCsv<TRecord>(string FilePath)
        {
            using var reader = File.OpenText(FilePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
                HeaderValidated = null
            });
            return csv.GetRecords<TRecord>().ToList();
        }
    }
}
