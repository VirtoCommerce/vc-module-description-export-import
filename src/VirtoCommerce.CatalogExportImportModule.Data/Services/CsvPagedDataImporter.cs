using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using FluentValidation;
using FluentValidation.Results;
using VirtoCommerce.CatalogExportImportModule.Core;
using VirtoCommerce.CatalogExportImportModule.Core.Models;
using VirtoCommerce.CatalogExportImportModule.Core.Services;
using VirtoCommerce.Platform.Core.Assets;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogExportImportModule.Data.Services
{
    public abstract class CsvPagedDataImporter<TImportable> : ICsvPagedDataImporter
         where TImportable : IImportable
    {
        private readonly ICsvImportReporterFactory _importReporterFactory;
        private readonly IImportPagedDataSourceFactory _dataSourceFactory;
        private readonly IValidator<ImportRecord<TImportable>[]> _importRecordsValidator;
        private readonly IBlobUrlResolver _blobUrlResolver;
        private readonly ImportConfigurationFactory _importConfigurationFactory;

        public abstract string DataType { get; }

        protected CsvPagedDataImporter(
            IImportPagedDataSourceFactory dataSourceFactory,
            IValidator<ImportRecord<TImportable>[]> importRecordsValidator,
            ICsvImportReporterFactory importReporterFactory,
            IBlobUrlResolver blobUrlResolver,
            ImportConfigurationFactory importConfigurationFactory)
        {
            _importReporterFactory = importReporterFactory;
            _dataSourceFactory = dataSourceFactory;
            _importRecordsValidator = importRecordsValidator;
            _blobUrlResolver = blobUrlResolver;
            _importConfigurationFactory = importConfigurationFactory;
        }

        protected virtual async Task<ClassMap<TImportable>> GetClassMapAsync(ImportDataRequest request)
        {
            return await Task.FromResult<ClassMap<TImportable>>(null);
        }

        public virtual async Task ImportAsync(ImportDataRequest request, Action<ImportProgressInfo> progressCallback, ICancellationToken cancellationToken)
        {
            ValidateParameters(request, progressCallback, cancellationToken);

            var errorsContext = new ImportErrorsContext();

            var configuration = _importConfigurationFactory.Create();

            var reportFilePath = GetReportFilePath(request.FilePath);
            await using var importReporter = await _importReporterFactory.CreateAsync(reportFilePath, configuration.Delimiter);

            cancellationToken.ThrowIfCancellationRequested();

            var importProgress = new ImportProgressInfo { Description = "Import has started" };

            SetupErrorHandlers(progressCallback, configuration, errorsContext, importProgress);

            using var dataSource = _dataSourceFactory.Create<TImportable>(request.FilePath, ModuleConstants.Settings.PageSize, configuration);

            var classMap = await GetClassMapAsync(request);

            if (classMap != null)
            {
                dataSource.RegisterClassMap(classMap);
            }

            var headerRaw = dataSource.GetHeaderRaw();
            if (!headerRaw.IsNullOrEmpty())
            {
                await importReporter.WriteHeaderAsync(headerRaw);
            }

            importProgress.TotalCount = dataSource.GetTotalCount();
            progressCallback(importProgress);

            const string importDescription = "{0} out of {1} have been imported.";

            try
            {
                importProgress.Description = "Fetching...";
                progressCallback(importProgress);

                while (await dataSource.FetchAsync())
                {
                    await ProcessChunkAsync(request, progressCallback, dataSource, errorsContext, importProgress, importReporter);

                    foreach (var error in errorsContext.Errors.OrderBy(error => error.Row))
                    {
                        await importReporter.WriteAsync(error);
                    }

                    if (importProgress.ProcessedCount != importProgress.TotalCount)
                    {
                        importProgress.Description = string.Format(importDescription, importProgress.ProcessedCount, importProgress.TotalCount);
                        progressCallback(importProgress);
                    }
                }
            }
            catch (Exception e)
            {
                HandleError(progressCallback, importProgress, e.Message);
            }
            finally
            {
                var completedMessage = importProgress.ErrorCount > 0 ? "Import completed with errors" : "Import completed";
                importProgress.Description = $"{completedMessage}: {string.Format(importDescription, importProgress.ProcessedCount, importProgress.TotalCount)}";

                if (importReporter.ReportIsNotEmpty)
                {
                    importProgress.ReportUrl = _blobUrlResolver.GetAbsoluteUrl(reportFilePath);
                }

                progressCallback(importProgress);
            }
        }


        protected abstract Task ProcessChunkAsync(ImportDataRequest request, Action<ImportProgressInfo> progressCallback, IImportPagedDataSource<TImportable> dataSource,
            ImportErrorsContext errorsContext, ImportProgressInfo importProgress, ICsvImportReporter importReporter);

        protected async Task<ValidationResult> ValidateAsync(ValidationContext<ImportRecord<TImportable>[]> validationContext, ImportErrorsContext errorsContext)
        {
            var validationResult = await _importRecordsValidator.ValidateAsync(validationContext);

            var errorsInfos = validationResult.Errors.Select(x => new { Message = x.ErrorMessage, (x.CustomState as ImportValidationState<TImportable>)?.InvalidRecord }).ToArray();

            // We need to order by row number because otherwise records will be written to report in random order
            var errorsGroups = errorsInfos.GroupBy(x => x.InvalidRecord);

            foreach (var group in errorsGroups)
            {
                var record = group.Key;

                var errorMessages = string.Join(" ", group.Select(x => x.Message).ToArray());

                var importError = new ImportError
                {
                    Error = errorMessages,
                    RawRow = record.RawRecord,
                    Row = record.Row
                };

                errorsContext.Errors.Add(importError);
            }

            return validationResult;
        }

        protected static void HandleError(Action<ImportProgressInfo> progressCallback, ImportProgressInfo importProgress, string error = null)
        {
            if (error != null)
            {
                importProgress.Errors.Add(error);
            }

            progressCallback(importProgress);
        }


        private static void HandleBadDataError(Action<ImportProgressInfo> progressCallback, ImportProgressInfo importProgress, CsvContext context, ImportErrorsContext errorsContext)
        {
            var importError = new ImportError
            {
                Error = "This row has invalid data. The data after field with not escaped quote was lost.",
                RawRow = context.Parser.RawRecord,
                Row = context.Parser.Row
            };

            errorsContext.Errors.Add(importError);
            HandleError(progressCallback, importProgress);

            throw new BadDataException(context, "Exception to prevent double BadDataFound call");
        }

        private static void HandleWrongValueError(Action<ImportProgressInfo> progressCallback, ImportProgressInfo importProgress, CsvContext context, ImportErrorsContext errorsContext)
        {
            var invalidFieldName = context.Reader.HeaderRecord[context.Reader.CurrentIndex];
            var importError = new ImportError
            {
                Error = string.Format(ModuleConstants.ValidationMessages[ModuleConstants.ValidationErrors.InvalidValue], invalidFieldName),
                RawRow = context.Parser.RawRecord,
                Row = context.Parser.Row
            };

            errorsContext.Errors.Add(importError);
            HandleError(progressCallback, importProgress);
        }

        private static void HandleRequiredValueError(Action<ImportProgressInfo> progressCallback, ImportProgressInfo importProgress, CsvContext context, ImportErrorsContext errorsContext)
        {
            var fieldName = context.Reader.HeaderRecord[context.Reader.CurrentIndex];
            var requiredFields = CsvImportHelper.GetImportCustomerRequiredColumns<TImportable>();
            var missedValueColumns = new List<string>();

            for (var i = 0; i < context.Reader.HeaderRecord.Length; i++)
            {
                if (requiredFields.Contains(context.Reader.HeaderRecord[i], StringComparer.InvariantCultureIgnoreCase) && context.Parser.Record[i].IsNullOrEmpty())
                {
                    missedValueColumns.Add(context.Reader.HeaderRecord[i]);
                }
            }

            var importError = new ImportError
            {
                Error = $"The required value in column {fieldName} is missing.",
                RawRow = context.Parser.RawRecord,
                Row = context.Parser.Row
            };

            if (missedValueColumns.Count > 1)
            {
                importError.Error = $"The required values in columns: {string.Join(", ", missedValueColumns)} - are missing.";
            }

            errorsContext.Errors.Add(importError);
            HandleError(progressCallback, importProgress);
        }

        private static void HandleMissedColumnError(Action<ImportProgressInfo> progressCallback, ImportProgressInfo importProgress, CsvContext context, ImportErrorsContext errorsContext)
        {
            var headerColumns = context.Reader.HeaderRecord;
            var recordFields = context.Parser.Record;
            var missedColumns = headerColumns.Skip(recordFields.Length).ToArray();
            var error = $"This row has unclosed quote or missed columns: {string.Join(", ", missedColumns)}.";
            var importError = new ImportError
            {
                Error = error,
                RawRow = context.Parser.RawRecord,
                Row = context.Parser.Row
            };

            errorsContext.Errors.Add(importError);
            HandleError(progressCallback, importProgress);
        }

        private static void SetupErrorHandlers(Action<ImportProgressInfo> progressCallback, CsvConfiguration configuration,
            ImportErrorsContext errorsContext, ImportProgressInfo importProgress)
        {
            configuration.ReadingExceptionOccurred = args =>
            {
                var context = args.Exception.Context;

                if (!errorsContext.Errors.Select(error => error.Row).Contains(context.Parser.Row))
                {
                    var fieldSourceValue = context.Reader[context.Reader.CurrentIndex];

                    if (fieldSourceValue == string.Empty)
                    {
                        HandleRequiredValueError(progressCallback, importProgress, context, errorsContext);
                    }
                    else
                    {
                        HandleWrongValueError(progressCallback, importProgress, context, errorsContext);
                    }
                }

                return false;
            };

            configuration.BadDataFound = args => HandleBadDataError(progressCallback, importProgress, args.Context, errorsContext);

            configuration.MissingFieldFound = args => HandleMissedColumnError(progressCallback, importProgress, args.Context, errorsContext);
        }

        protected static string GetReportFilePath(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var fileExtension = Path.GetExtension(fileName);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var reportFileName = $"{fileNameWithoutExtension}_report{fileExtension}";
            var result = filePath.Replace(fileName, reportFileName);

            return result;
        }

        private static void ValidateParameters(ImportDataRequest request, Action<ImportProgressInfo> progressCallback, ICancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (progressCallback == null)
            {
                throw new ArgumentNullException(nameof(progressCallback));
            }

            if (cancellationToken == null)
            {
                throw new ArgumentNullException(nameof(cancellationToken));
            }
        }
    }
}
