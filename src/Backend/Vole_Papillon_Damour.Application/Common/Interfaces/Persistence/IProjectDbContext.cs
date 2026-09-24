using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;

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
    DbSet<EmailBounceEvent> EmailBounceEvents { get; }
    DbSet<RareBook> RareBooks { get; }
    DbSet<RareBookPhoto> RareBookPhotos { get; }
    DbSet<RareBookTombstone> RareBookTombstones => throw new NotSupportedException();
    DbSet<MemberSelectionItem> MemberSelectionItems => throw new NotSupportedException();
    DbSet<MemberCard> MemberCards => throw new NotSupportedException();
    DbSet<CheckoutPassage> CheckoutPassages => throw new NotSupportedException();
    DbSet<CheckoutPassageLine> CheckoutPassageLines => throw new NotSupportedException();
}
