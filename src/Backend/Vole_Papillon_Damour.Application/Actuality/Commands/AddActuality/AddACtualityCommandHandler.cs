using System.Web;
using ErrorOr;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Identity.UI.Services;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.AddActuality;

public class AddACtualityCommandHandler(IActualityRepository actualityRepository, IMapper mapper, 
    IBlobService blobService, IEmailService emailService)
    : IRequestHandler<AddACtualityCommand, ErrorOr<ActualityResult>>
{
    public async Task<ErrorOr<ActualityResult>> Handle(AddACtualityCommand command, CancellationToken cancellationToken)
    {
        var urlPrincipalImage = command.PrincipalImageUri;
        if (urlPrincipalImage is null)
        {
            urlPrincipalImage = await blobService.UploadActualityImagesAsync(command.PrincipalImage!.FileName,
                command.PrincipalImage.OpenReadStream());
        }

        List<Uri> imagesUri = command.ImagesUrls ?? new();
        foreach (var image in command.Images ?? [])
        {
            var uri = await blobService.UploadActualityImagesAsync(image.FileName, image.OpenReadStream());
            imagesUri.Add(uri);
        }
        
        var actuality = Domain.ActualityAggregate.Actuality.Create(
            command.Title,
            command.Article,
            urlPrincipalImage!,
            command.FacebookLink,
            command.InstagramLink,
            imagesUri,
            command.Date
        );
        actuality = await actualityRepository.AddAsync(actuality);
        
        await emailService.SendEmailToMailingListAsync("Nouvelle actualité",
            $"<!DOCTYPE html>\n<html lang=\"fr\">\n<head>\n  <meta charset=\"UTF-8\">\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n  <title>Template Email</title>\n  <style>\n    body {{\n      margin: 0;\n      padding: 0;\n      font-family: Arial, sans-serif;\n      background-color: #f4f4f4;\n    }}\n    .container {{\n      width: 100%;\n      max-width: 600px;\n      margin: auto;\n      background: #ffffff;\n      border-radius: 8px;\n      overflow: hidden;\n      box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);\n    }}\n    .header {{\n      background: #012f5f;\n      color: #ffffff;\n      padding: 20px;\n      text-align: center;\n      display: flex;\n      align-items: center;\n      flex-direction: row;\n    }}\n    .content {{\n      padding: 20px;\n    }}\n    .footer {{\n      background: #f4f4f4;\n      text-align: center;\n      padding: 10px;\n      font-size: 12px;\n      color: #888888;\n    }}\n    .header img {{\n      max-width: 50px; /* Ajustez la taille de l'image */\n      margin-right: 15px; /* Espacement entre l'image et le titre */\n    }}\n    @media only screen and (max-width: 600px) {{\n      .container {{\n        width: 100% !important;\n      }}\n      .header, .content, .footer {{\n        padding: 15px;\n      }}\n    }}\n    .title-actualite {{\n      display: flex;\n      align-items: center;\n      margin-bottom: 20px;\n    }}\n    .title-actualite h2 {{\n      color: #012f5f; /* Couleur du titre */\n    }}\n    .title-actualite img {{\n      margin-left: 10px;\n      width: 30px; /* Ajustez la taille de l'icône */\n    }}\n    .actualite-container {{\n      border: 4px solid #012f5f; /* Couleur bordure */\n      border-radius: 15px;\n      padding: 15px;\n      background: #ffffff;\n    }}\n    .actualite-title {{\n      display: flex;\n      align-items: center;\n      margin-bottom: 10px;\n    }}\n    .actualite-title .border-line {{\n      background: #012f5f; /* Couleur de la ligne */\n      width: 10px;\n      height: 25px;\n      margin-right: 10px;\n    }}\n    .actualite-title h2 {{\n      color: #012f5f; /* Couleur du titre */\n      font-size: 24px; /* Taille du texte */\n    }}\n    .actualite-image {{\n      border-radius: 15px;\n      width: 100%;\n      height: auto;\n      margin: 10px 0;\n    }}\n    .article-box {{\n      font-size: 18px;\n      color: #333333; /* Couleur du texte */\n      margin-bottom: 20px;\n      text-align: justify;\n    }}\n    .footer-container {{\n      display: flex;\n      justify-content: space-between;\n      align-items: center;\n    }}\n    .footer-container a {{\n      color: #012f5f; /* Couleur du lien */\n      font-size: 18px; /* Taille du texte du lien */\n      text-decoration: none;\n    }}\n    .footer-container .date-container {{\n      display: flex;\n      align-items: center;\n    }}\n    .footer-container img {{\n      width: 35px;\n      height: 35px;\n      margin-right: 10px;\n    }}\n    .footer-container h3 {{\n      color: #012f5f; /* Couleur de la date */\n      font-size: 18px; /* Taille de la date */\n    }}\n  </style>\n</head>\n<body>\n<div class=\"container\">\n  <div class=\"header\">\n    <img src=\"https://volepapillondamourdata.blob.core.windows.net/images-mails/vpd_logo.svg\" alt=\"Papillon logo\">\n    <h1>Vole Papillon D'Amour</h1>\n  </div>\n  <div class=\"content\">\n    <div class=\"title-actualite\">\n      <h2>Nouvelle Actualité !</h2>\n      <img src=\"https://volepapillondamourdata.blob.core.windows.net/images-mails/newspaper-icon.svg\" alt=\"Icon de journal\">\n    </div>\n    <div class=\"actualite-container\">\n      <div class=\"actualite-title\">\n        <div class=\"border-line\"></div>\n        <h2>{HttpUtility.HtmlEncode(actuality.Title)}</h2>\n      </div>\n      <img src=\"{HttpUtility.HtmlEncode(actuality.UrlPrincipalImage)}\" alt=\"Loto Halloween\" class=\"actualite-image\">\n      <p class=\"article-box\">{HttpUtility.HtmlEncode(actuality.Article)}</p>\n      <div class=\"footer-container\">\n        <a href=\"https://volepapillondamour/actualite/{HttpUtility.HtmlEncode(actuality.Id.Value)}\">Lire la suite ></a>\n        <div class=\"date-container\">\n          <img src=\"https://volepapillondamourdata.blob.core.windows.net/images-mails/calendar-icon.svg\" alt=\"Calendar Icon\">\n          <h3>{actuality.Date:dd/MM}</h3>\n        </div>\n      </div>\n    </div>\n  </div>\n    <div class=\"footer\">\n      <p>Association Vole Papillon D'amour</p>\n      <p>Adresse : 46 route de Saint Marcellin, Saint Just Saint Rambert 42170, France</p>\n      <p>Téléphone : 06 19 36 54 45</p>\n      <p>Email : volepapillondamour@sfr.fr</p>\n      <p><a href=\"https://www.volepapillondamour.fr\">www.volepapillondamour.fr</a></p>\n      <p><a href=\"https://www.volepapillondamour.fr/mail-desabonnement\">Se désabonner</a></p>\n      <p>\u00a9 2025 Association Vole Papillon D'amour. Tous droits réservés.</p>\n    </div>\n</div>\n</body>\n</html>\n"
            , cancellationToken);
        
        return mapper.Map<ActualityResult>(actuality);
    }
}