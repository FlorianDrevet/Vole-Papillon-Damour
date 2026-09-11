using System.Runtime.CompilerServices;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.ActualityAggregate;

public sealed class Actuality : AggregateRoot<ActualityId>
{
    public string Title { get; private set; } = null!;
    public DateTimeOffset Date { get; private set; }
    public string Article { get; private set; } = null!;
    public Uri UrlPrincipalImage { get; private set; } = null!;
    public Uri? FacebookLink { get; private set; } = null!;
    public Uri? InstagramLink { get; private set; } = null!;
    private List<Uri> _images = new();
    public IReadOnlyList<Uri> Images => _images.AsReadOnly();
    public ActualityStatus Status { get; private set; } = ActualityStatus.Published;
    public bool TitleNeedsReview { get; private set; }
    public DateTimeOffset? ImportedAt { get; private set; }

    public Actuality(
        ActualityId id,
        string title,
        string article,
        Uri urlPrincipalImage,
        Uri? facebookLink, 
        Uri? instagramLink,
        List<Uri> images,
        DateTimeOffset date) : base(id)
    {
        Title = title;
        Article = article;
        UrlPrincipalImage = urlPrincipalImage;
        FacebookLink = facebookLink;
        InstagramLink = instagramLink;
        _images = images;
        Date = date;
        Status = ActualityStatus.Published;
        TitleNeedsReview = false;
        ImportedAt = null;
    }

    public static Actuality Create(string title,
        string article,
        Uri urlPrincipalImage,
        Uri? facebookLink, 
        Uri? instagramLink,
        List<Uri> images,
        DateTimeOffset date)
    {
        return new Actuality(ActualityId.CreateUnique(), 
            title, article, urlPrincipalImage,
            facebookLink, instagramLink, images, date);
    }

    public static Actuality CreateImported(
        string title,
        string article,
        Uri urlPrincipalImage,
        Uri? instagramLink,
        List<Uri> images,
        DateTimeOffset date,
        DateTimeOffset importedAt,
        bool titleNeedsReview)
    {
        var actuality = new Actuality(
            ActualityId.CreateUnique(),
            title,
            article,
            urlPrincipalImage,
            facebookLink: null,
            instagramLink,
            images,
            date.ToUniversalTime());

        actuality.Status = ActualityStatus.Draft;
        actuality.TitleNeedsReview = titleNeedsReview;
        actuality.ImportedAt = importedAt.ToUniversalTime();
        return actuality;
    }
    
    public void Update(string title,
        string article,
        Uri urlPrincipalImage,
        Uri? facebookLink,
        Uri? instagramLink,
        List<Uri> images,
        DateTimeOffset date)
    {
        Title = title;
        Article = article;
        UrlPrincipalImage = urlPrincipalImage;
        FacebookLink = facebookLink;
        InstagramLink = instagramLink;
        _images = images;
        Date = date;
        TitleNeedsReview = false;
    }

    public bool Publish()
    {
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Article))
        {
            return false;
        }

        Status = ActualityStatus.Published;
        return true;
    }

    public void MarkTitleReviewed()
    {
        TitleNeedsReview = false;
    }

    public Actuality()
    {
    }
}
