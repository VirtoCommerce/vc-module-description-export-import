using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using VirtoCommerce.DescriptionExportImportModule.Core.Models;
using VirtoCommerce.DescriptionExportImportModule.Core.Services;
using VirtoCommerce.Platform.Core.Assets;

namespace VirtoCommerce.DescriptionExportImportModule.Data.Services
{
    public sealed class ImportPagedDataSource<T> : IImportPagedDataSource<T> where T : IImportable
    {
        private readonly Stream _stream;
        private readonly Configuration _configuration;
        private readonly StreamReader _streamReader;
        private readonly CsvReader _csvReader;
        private int? _totalCount;

        public ImportPagedDataSource(string filePath, IBlobStorageProvider blobStorageProvider, int pageSize,
            Configuration configuration)
        {
            var stream = blobStorageProvider.OpenRead(filePath);

            _stream = stream;
            _streamReader = new StreamReader(stream);

            _configuration = configuration;
            _csvReader = new CsvReader(_streamReader, configuration);

            PageSize = pageSize;
        }

        public int CurrentPageNumber { get; private set; }

        public int PageSize { get; }

        public int GetTotalCount()
        {
            if (_totalCount != null)
            {
                return _totalCount.Value;
            }

            _totalCount = 0;

            var streamPosition = _stream.Position;
            _stream.Seek(0, SeekOrigin.Begin);

            using var streamReader = new StreamReader(_stream, leaveOpen: true);
            using var csvReader = new CsvReader(streamReader, _configuration, true);
            try
            {
                csvReader.Read();
                csvReader.ReadHeader();
                csvReader.ValidateHeader<T>();
            }
            catch (ValidationException)
            {
                _totalCount++;
            }

            while (csvReader.Read())
            {
                _totalCount++;
            }

            _stream.Seek(streamPosition, SeekOrigin.Begin);

            return _totalCount.Value;
        }

        public string GetHeaderRaw()
        {
            var result = string.Empty;

            var streamPosition = _stream.Position;
            _stream.Seek(0, SeekOrigin.Begin);

            using var streamReader = new StreamReader(_stream, leaveOpen: true);
            using var csvReader = new CsvReader(streamReader, _configuration, true);

            try
            {
                csvReader.Read();
                csvReader.ReadHeader();
                csvReader.ValidateHeader<T>();

                result = string.Join(csvReader.Configuration.Delimiter, csvReader.Context.HeaderRecord);

            }
            finally
            {
                _stream.Seek(streamPosition, SeekOrigin.Begin);
            }

            return result;
        }

        public async Task<bool> FetchAsync()
        {
            if (CurrentPageNumber * PageSize >= GetTotalCount())
            {
                Items = Array.Empty<ImportRecord<T>>();
                return false;
            }

            var items = new List<ImportRecord<T>>();

            for (var i = 0; i < PageSize && await _csvReader.ReadAsync(); i++)
            {
                var record = _csvReader.GetRecord<T>();

                if (record != null)
                {
                    var rawRecord = _csvReader.Context.RawRecord;
                    var row = _csvReader.Context.Row;

                    items.Add(new ImportRecord<T> { Row = row, RawRecord = rawRecord, Record = record });
                }
            }

            Items = items.ToArray();

            CurrentPageNumber++;

            return true;
        }

        public ImportRecord<T>[] Items { get; private set; }

        public void Dispose()
        {
            _csvReader.Dispose();
            _streamReader.Dispose();
            _stream.Dispose();
        }

    }
}
