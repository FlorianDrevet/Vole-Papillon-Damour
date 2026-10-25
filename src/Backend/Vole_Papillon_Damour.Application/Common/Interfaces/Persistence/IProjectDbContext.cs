using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IProjectDbContext
{
    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    DbSet<Product> Products { get; }
    DbSet<User> Users { get; }
    DbSet<MemberGenreFavorite> MemberGenreFavorites => throw new NotSupportedException();
    DbSet<AssoEvents> AssoEvents { get; }
    DbSet<Order> Orders { get; }
    DbSet<Book> Books { get; }
    DbSet<BookIsbndbRecord> BookIsbndbRecords => throw new NotSupportedException();
    DbSet<IsbndbQuotaState> IsbndbQuotaStates => throw new NotSupportedException();
    DbSet<IsbndbDailyUsage> IsbndbDailyUsages => throw new NotSupportedException();
    DbSet<IsbndbCatchUpRun> IsbndbCatchUpRuns => throw new NotSupportedException();
    DbSet<BookGenre> BookGenres => throw new NotSupportedException();
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
    DbSet<BookNotFoundReport> BookNotFoundReports => throw new NotSupportedException();
    DbSet<BookSimilarityProfile> BookSimilarityProfiles => throw new NotSupportedException();
    DbSet<BookRecommendationJob> BookRecommendationJobs => throw new NotSupportedException();
    DbSet<BookNeighbor> BookNeighbors => throw new NotSupportedException();
    DbSet<RecommendationGeneration> RecommendationGenerations => throw new NotSupportedException();
    DbSet<MemberRecommendationPreference> MemberRecommendationPreferences => throw new NotSupportedException();
    DbSet<ManualRequester> ManualRequesters => throw new NotSupportedException();
    DbSet<ManualBookRequest> ManualBookRequests => throw new NotSupportedException();
}
