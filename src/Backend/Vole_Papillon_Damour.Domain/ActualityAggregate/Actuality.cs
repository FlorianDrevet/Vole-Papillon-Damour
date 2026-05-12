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
    }

    public Actuality()
    {
    }
}