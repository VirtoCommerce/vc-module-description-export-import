using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Moq;
using VirtoCommerce.CatalogExportImportModule.Data.Services;
using VirtoCommerce.Platform.Core.Assets;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.CustomerExportImportModule.Tests
{
    public static class TestHelper
    {
        public static IBlobStorageProvider GetBlobStorageProvider(string csv, MemoryStream errorReporterMemoryStream = null)
        {
            errorReporterMemoryStream ??= new MemoryStream();
            var blobStorageProviderMock = new Mock<IBlobStorageProvider>();
            var stream = GetStream(csv);
            blobStorageProviderMock.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(() => stream);
            blobStorageProviderMock.Setup(x => x.OpenWrite(It.IsAny<string>())).Returns(() => errorReporterMemoryStream);
            blobStorageProviderMock.Setup(x => x.GetBlobInfoAsync(It.IsAny<string>()))
                .ReturnsAsync(() => new BlobInfo { Size = stream.Length });
            return blobStorageProviderMock.Object;
        }

        public static ImportPagedDataSourceFactory GetCustomerImportPagedDataSourceFactory(IBlobStorageProvider blobStorageProvider)
        {
            return new ImportPagedDataSourceFactory(blobStorageProvider);
        }

        public static Stream GetStream(string csv)
        {
            var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(csv);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public static string GetCsv(IEnumerable<string> records, string header = null)
        {
            var csv = "";

            if (header != null)
            {
                csv += header + "\r\n";
            }

            return records.Aggregate(csv, (current, record) => current + record + "\r\n");
        }

        public static IEnumerable<PropertyInfo> GetProperties<T>(T obj)
        {
            return obj.GetType()
                .GetTypeInfo()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.Name != nameof(ApplicationUser.SecurityStamp) && p.Name != nameof(ApplicationUser.ConcurrencyStamp))
                .OrderBy(p => p.Name)
                .ToList();
        }

        public static string ToString<T>(T obj)
        {
            var propertiesAndValues = GetProperties(obj).Select(property =>
            {
                var value = property.GetValue(obj);
                return $"{property.Name}: {(value is IEnumerable<object> enumerable ? $"[{string.Join(", ", enumerable.Select(x => x.ToString()))}]" : value)}";
            });
            return $"{{{string.Join(", ", propertiesAndValues)}}}";
        }
    }
}
