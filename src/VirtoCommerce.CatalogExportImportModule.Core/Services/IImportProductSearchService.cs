using System.Threading.Tasks;
using VirtoCommerce.CatalogExportImportModule.Core.Models;
using VirtoCommerce.CatalogModule.Core.Model.Search;

namespace VirtoCommerce.CatalogExportImportModule.Core.Services
{
    public interface IImportProductSearchService
    {
        Task<ProductSearchResult> SearchAsync(ImportProductSearchCriteria criteria);
    }
}
