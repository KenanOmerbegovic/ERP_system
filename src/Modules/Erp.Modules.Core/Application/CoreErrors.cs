using Erp.SharedKernel.Results;

namespace Erp.Modules.Core.Application;

public static class CoreErrors
{
    public static readonly Error CompanyNotFound =
        Error.NotFound("Company.NotFound", "Company not found.");

    public static readonly Error DuplicateCompanyTaxId =
        Error.Conflict("Company.DuplicateTaxId", "A company with this tax id already exists.");

    public static readonly Error BusinessPartnerNotFound =
        Error.NotFound("BusinessPartner.NotFound", "Business partner not found.");

    public static readonly Error DuplicateBusinessPartnerCode =
        Error.Conflict("BusinessPartner.DuplicateCode", "A business partner with this code already exists.");

    public static readonly Error UnitOfMeasureNotFound =
        Error.NotFound("UnitOfMeasure.NotFound", "Unit of measure not found.");

    public static readonly Error DuplicateUnitOfMeasureCode =
        Error.Conflict("UnitOfMeasure.DuplicateCode", "A unit of measure with this code already exists.");

    public static readonly Error ArticleNotFound =
        Error.NotFound("Article.NotFound", "Article not found.");

    public static readonly Error DuplicateArticleCode =
        Error.Conflict("Article.DuplicateCode", "An article with this code already exists.");

    /// <summary>The unit referenced in the request body does not exist in the company.</summary>
    public static readonly Error ArticleUnitOfMeasureNotFound =
        Error.Validation("Article.UnitOfMeasureNotFound", "The unit of measure does not exist in this company.");
}
