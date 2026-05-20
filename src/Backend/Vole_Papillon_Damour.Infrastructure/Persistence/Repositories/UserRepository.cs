using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Repositories;

public class UserRepository(ProjectDbContext vpdDbContext) : IUserRepository
{
    public User? GetUserByEmail(string email)
    {
        return vpdDbContext.Users
            .FirstOrDefault(user => user.Email == email);
    }

    public void AddUser(User user)
    {
        vpdDbContext.Add(user);
        vpdDbContext.SaveChanges();
    }

    public User? GetUserById(UserId requestUserId)
    {
        return vpdDbContext.Users
            .FirstOrDefault(user => user.Id == requestUserId);
    }

    public void UpdateUser(User user)
    {
        vpdDbContext.Update(user);
        vpdDbContext.SaveChanges();
    }

    public List<User> GetAllUsers()
    {
        return vpdDbContext.Users.ToList();
    }
}