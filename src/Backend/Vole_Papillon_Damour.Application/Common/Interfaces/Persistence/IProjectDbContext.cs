using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IProjectDbContext
{
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    DbSet<Product> Products { get; }
    DbSet<User> Users { get; }
    DbSet<AssoEvents> AssoEvents { get; }
    DbSet<Order> Orders { get; }
    DbSet<Book> Books { get; }
    DbSet<BookAnnouncement> BookAnnouncements { get; }
    DbSet<BookMovement> BookMovements { get; }
    DbSet<ScanSession> ScanSessions { get; }
    DbSet<AssociationSettings> AssociationSettings { get; }
    DbSet<Watchlist> Watchlists { get; }
    DbSet<WatchlistItem> WatchlistItems { get; }
    DbSet<UserAlertHistory> UserAlertHistories { get; }
}
