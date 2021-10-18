using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using VirtoCommerce.CatalogExportImportModule.Core.Models;
using Xunit;

namespace VirtoCommerce.CatalogExportImportModule.Tests
{
    public class CsvHelperTests
    {

        [Fact]
        public async Task TestDoubleBadDataFoundCase()
        {
            var errorCount = 0;
            var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                ReadingExceptionOccurred = args => false,
                BadDataFound = args =>
                {
                    ++errorCount;
                },
                Delimiter = ";",
            };

            var header = "Product Name;Product SKU;Product Type;";
            var records = new[] { "Test name;test SKU;;", "Test name 2;test SKU 2;;", "Test name 3;test SKU 3;\"Physical;" };
            var csv = TestHelper.GetCsv(records, header);
            var textReader = new StreamReader(TestHelper.GetStream(csv), leaveOpen: true);

            var csvReader = new CsvReader(textReader, csvConfiguration);

            await csvReader.ReadAsync();
            csvReader.ReadHeader();

            while (await csvReader.ReadAsync())
            {
                csvReader.GetRecord<CsvPhysicalProduct>();

            }

            Assert.Equal(2, errorCount);
        }

        [Fact]
        public async Task EnsureBadDataFoundWasCalledOnceCase()
        {
            var errorCount = 0;
            var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                ReadingExceptionOccurred = args => false,
                BadDataFound = args =>
                {
                    // Add error to error report
                    throw new BadDataException(args.Context, "Exception to prevent double BadDataFount call");
                },
                Delimiter = ";",
            };
            var header = "Product Name;Product SKU;Product Type;";
            var records = new[] { "Test name;\"test SKU\";;", "Test name 2;\"test SKU 2;;", "Test name 3;test SKU 3;;", "Test name 4;\"test SKU 4;;", };
            var csv = TestHelper.GetCsv(records, header);
            var textReader = new StreamReader(TestHelper.GetStream(csv), leaveOpen: true);

            var csvReader = new CsvReader(textReader, csvConfiguration);
            var errorString = string.Empty;
            try
            {
                while (await csvReader.ReadAsync())
                {
                    csvReader.GetRecord<CsvPhysicalProduct>();
                }
            }
            catch (BadDataException e)
            {
                errorString = e.Context.Parser.RawRecord;
                ++errorCount;
            }

            Assert.Equal(1, errorCount);
            Assert.Equal("Test name 2;\"test SKU 2;;\r\nTest name 3;test SKU 3;;\r\nTest name 4;\"test SKU 4;;", errorString.TrimEnd());
        }
    }
}
