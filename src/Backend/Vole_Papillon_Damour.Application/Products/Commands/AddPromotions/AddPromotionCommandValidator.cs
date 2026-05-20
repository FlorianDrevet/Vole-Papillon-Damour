using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

namespace Vole_Papillon_Damour.Application.Products.Commands.AddPromotions;

public class AddPromotionValidator : AbstractValidator<AddPromotionCommand>
{
    private readonly IProjectDbContext _projectDbContext;
    public AddPromotionValidator(IProjectDbContext projectDbContext)
    {
        _projectDbContext = projectDbContext;
        
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.Promotion)
            .NotEmpty();
    }
}
