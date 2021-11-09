using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.CatalogExportImportModule.Core
{
    public static class ModuleConstants
    {
        public static class DataTypes
        {
            public const string EditorialReview = nameof(EditorialReview);

            public const string PhysicalProduct = "PhysicalProduct";
        }
        public static class ProductTypes
        {
            public const string Physical = "Physical";
        }

        public static class PushNotificationsTitles
        {
            public static readonly IReadOnlyDictionary<string, string> Export = new Dictionary<string, string>()
            {
                {DataTypes.EditorialReview, "Product descriptions export"},
                {DataTypes.PhysicalProduct, "Physical products export"},
            };
            public static readonly IReadOnlyDictionary<string, string> Import = new Dictionary<string, string>()
            {
                {DataTypes.EditorialReview, "Product descriptions import"},
                {DataTypes.PhysicalProduct, "Physical products import"},
            };
        }

        public static readonly IReadOnlyDictionary<string, string> ExportFileNamePrefixes = new Dictionary<string, string>()
        {
            {DataTypes.EditorialReview, "Descriptions"},
            {DataTypes.PhysicalProduct, "Physical_products"},
        };

        public const int ElasticMaxTake = 10000;

        public const int KByte = 1024;

        public const int MByte = 1024 * KByte;

        public static class ValidationContextData
        {
            public const string CatalogId = nameof(CatalogId);

            public const string ExistedCategories = nameof(ExistedCategories);

            public const string ExistedProducts = nameof(ExistedProducts);

            public const string Skus = nameof(Skus);

            public const string DuplicatedImportReview = nameof(DuplicatedImportReview);

            public const string AvailablePackageTypes = nameof(AvailablePackageTypes);

            public const string AvailableMeasureUnits = nameof(AvailableMeasureUnits);

            public const string AvailableWeightUnits = nameof(AvailableWeightUnits);

            public const string AvailableTaxTypes = nameof(AvailableTaxTypes);

            public const string AvailableLanguages = nameof(AvailableLanguages);

            public const string AvailableReviewTypes = nameof(AvailableReviewTypes);

            public const string ExistedReviews = nameof(ExistedReviews);

            public const string ExistedMainProducts = nameof(ExistedMainProducts);

        }

        public static class ValidationErrors
        {
            public const string DuplicateError = "Duplicate";

            public const string FileNotExisted = "file-not-existed";

            public const string NoData = "no-data";

            public const string ExceedingFileMaxSize = "exceeding-file-max-size";

            public const string WrongDelimiter = "wrong-delimiter";

            public const string ExceedingLineLimits = "exceeding-line-limits";

            public const string MissingRequiredColumns = "missing-required-columns";

            public const string MissingRequiredValues = "missing-required-values";

            public const string ExceedingMaxLength = "exceeding-max-length";

            public const string ArrayValuesExceedingMaxLength = "array-values-exceeding-max-length";

            public const string InvalidValue = "invalid-value";

            public const string NotUniqueValue = "not-unique-value";

            public const string ReviewExistsInSystem = "review-exists-in-system";

            public const string NotUniqueMultiValue = "not-unique-multi-value";

            public const string MainProductIsNotExists = "main-product-is-not-exists";

            public const string CycleSelfReference = "cycle-self-reference";

            public const string MainProductIsVariation = "main-product-is-variation";
        }

        public static readonly IReadOnlyDictionary<string, string> ValidationMessages = new Dictionary<string, string>
        {
            { ValidationErrors.MissingRequiredValues, "The required value in column '{0}' is missing." },
            { ValidationErrors.ExceedingMaxLength, "Value in column '{0}' may have maximum {1} characters." },
            { ValidationErrors.ArrayValuesExceedingMaxLength, "Every value in column '{0}' may have maximum {1} characters. The number of values is unlimited." },
            { ValidationErrors.InvalidValue, "This row has invalid value in the column '{0}'." },
            { ValidationErrors.NotUniqueValue, "Value in column '{0}' should be unique." },
            { ValidationErrors.NotUniqueMultiValue, "Values in column '{0}' should be unique for the item." },
            { ValidationErrors.MainProductIsNotExists, "The main product does not exists." },
            { ValidationErrors.CycleSelfReference, "The main product id is the same as product. It means self cycle reference." },
            { ValidationErrors.MainProductIsVariation, "The main product is variation. You should not import variations for variations." },
        };

        public static class Features
        {
            public const string CatalogExportImport = "CatalogExportImport";
        }

        public static class Security
        {
            public static class Permissions
            {
                public const string ImportAccess = "product:catalog:import";

                public const string ExportAccess = "product:catalog:export";

                public static string[] AllPermissions { get; } = { ImportAccess, ExportAccess };
            }
        }

        public static class Settings
        {
            public const int PageSize = 50;

            public static class General
            {
                public static SettingDescriptor ImportLimitOfLines { get; } = new SettingDescriptor
                {
                    Name = "CatalogExportImport.Import.LimitOfLines",
                    GroupName = "CatalogExportImport|Import",
                    ValueType = SettingValueType.PositiveInteger,
                    IsHidden = true,
                    DefaultValue = 10000
                };

                public static SettingDescriptor ImportFileMaxSize { get; } = new SettingDescriptor
                {
                    Name = "CatalogExportImport.Import.FileMaxSize",
                    GroupName = "CatalogExportImport|Import",
                    ValueType = SettingValueType.PositiveInteger,
                    IsHidden = true,
                    DefaultValue = 1 // MB
                };

                public static SettingDescriptor ExportLimitOfLines { get; } = new SettingDescriptor
                {
                    Name = "CatalogExportImport.Export.LimitOfLines",
                    GroupName = "CatalogExportImport|Export",
                    ValueType = SettingValueType.PositiveInteger,
                    IsHidden = true,
                    DefaultValue = 10000
                };

                public static IEnumerable<SettingDescriptor> AllSettings
                {
                    get
                    {
                        return new List<SettingDescriptor>
                        {
                            ImportLimitOfLines,
                            ImportFileMaxSize,
                            ExportLimitOfLines
                        };
                    }
                }
            }
        }
    }
}
