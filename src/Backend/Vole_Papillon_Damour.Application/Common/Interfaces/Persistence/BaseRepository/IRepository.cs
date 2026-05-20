using System.Linq.Expressions;
using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence.BaseRepository;

public interface IRepository<T>
{
    Task<T?> GetByIdAsync(ValueObject id);
    Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes);
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(ValueObject id);
}