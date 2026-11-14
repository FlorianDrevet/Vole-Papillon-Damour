using CommunityToolkit.Mvvm.ComponentModel;
using ShopAppVpd.Dtos;
using ShopAppVpd.Interfaces;
using ShopAppVpd.Services;
using ShopAppVpd.ViewModels;

namespace ShopAppVpd;

public partial class MainPage : ContentPage
{
    private readonly IAppUpdateService _appUpdateService;

    public MainPage(MainViewModel vm, IAppUpdateService appUpdateService)
    {
        InitializeComponent();
        BindingContext = vm;
        _appUpdateService = appUpdateService;
    }
    
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (ProductCollection?.ItemsLayout is GridItemsLayout grid)
        {
            double cardMinWidth = 225;
            double spacing = 15;
            double totalMargin = 30;
            
            double availableWidth = width - totalMargin;
            
            int span = Math.Max(1, (int)((availableWidth + spacing) / (cardMinWidth + spacing)));

            if (grid.Span != span)
                grid.Span = span;
        }
    }

    private async void OnUpdateAppClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        button.IsEnabled = false;
        button.Text = "Vérification…";

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            var update = await _appUpdateService.GetAvailableUpdateAsync(cancellation.Token);
            if (update is null)
            {
                await DisplayAlertAsync("Mise à jour", "La caisse est déjà à jour.", "OK");
                return;
            }

            var installConfirmed = await DisplayAlertAsync(
                "Mise à jour disponible",
                $"La version {update.VersionName} est disponible. Télécharger le fichier puis ouvrir l’installation Android ?",
                "Mettre à jour",
                "Plus tard");
            if (!installConfirmed)
                return;

            if (!_appUpdateService.CanInstallPackages)
            {
                var openSettings = await DisplayAlertAsync(
                    "Autoriser l’installation",
                    "Android doit autoriser Caisse à installer cette mise à jour. Ouvrir les paramètres maintenant ? Après activation, appuyez à nouveau sur « Mettre à jour ».",
                    "Ouvrir les paramètres",
                    "Annuler");
                if (openSettings)
                    _appUpdateService.OpenInstallPermissionSettings();

                return;
            }

            var progress = new Progress<double>(fraction =>
            {
                button.Text = $"Téléchargement {fraction:P0}";
            });
            button.Text = "Téléchargement…";

            await _appUpdateService.DownloadAndInstallAsync(update, progress, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            await DisplayAlertAsync("Mise à jour", "Le téléchargement a expiré. Vérifiez la connexion puis réessayez.", "Fermer");
        }
        catch (HttpRequestException)
        {
            await DisplayAlertAsync("Mise à jour", "La mise à jour est inaccessible. Vérifiez la connexion puis réessayez.", "Fermer");
        }
        catch (InvalidDataException)
        {
            await DisplayAlertAsync(
                "Mise à jour impossible",
                "Le fichier reçu n’a pas pu être vérifié. Réessayez plus tard ou téléchargez l’application depuis le BackOffice.",
                "Fermer");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            await DisplayAlertAsync(
                "Mise à jour impossible",
                "Caisse n’a pas pu préparer l’installation. Réessayez ou téléchargez l’application depuis le BackOffice.",
                "Fermer");
        }
        finally
        {
            button.Text = "Mettre à jour";
            button.IsEnabled = true;
        }
    }
}
