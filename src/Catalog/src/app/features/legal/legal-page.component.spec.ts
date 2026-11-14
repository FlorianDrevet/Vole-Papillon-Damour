import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute} from '@angular/router';

import {LegalPageComponent} from './legal-page.component';

async function renderPage(page: string): Promise<ComponentFixture<LegalPageComponent>> {
  await TestBed.configureTestingModule({
    declarations: [LegalPageComponent],
    providers: [
      {
        provide: ActivatedRoute,
        useValue: {snapshot: {data: {page}}},
      },
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(LegalPageComponent);
  fixture.detectChanges();
  return fixture;
}

describe('LegalPageComponent', () => {
  it('renders catalogue-specific publisher and hosting information', async () => {
    const fixture = await renderPage('legal');
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('Mentions légales');
    expect(content).toContain("Vole Papillon d'Amour");
    expect(content).toContain('W421002487');
    expect(content).toContain('Corinne Drevet');
    expect(content).toContain('Microsoft Azure');
    expect(content).toContain('Azure Container Apps');
  });

  it('explains account, watchlist and alert processing on the privacy page', async () => {
    const fixture = await renderPage('privacy');
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('compte');
    expect(content).toContain('livres suivis');
    expect(content).toContain('alertes');
    expect(content).toContain('Microsoft Entra');
    expect(content).toContain('Azure Communication Services');
    expect(content).toContain('Google Analytics 4');
    expect(content).toContain('Google Maps');
    expect(content).toContain('Ma sélection');
    expect(content).toContain('Carte de compte');
    expect(content).toContain('Mes achats');
    expect(content).toContain('La Vente anonyme reste toujours possible');
    expect(content.toLowerCase()).toContain('aucun prix ni montant n’est affiché');
  });

  it('explains manual requests for people without an account', async () => {
    const fixture = await renderPage('privacy');
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('Demandes saisies pour des personnes sans compte');
    expect(content).toContain('Le consentement de la personne est recueilli oralement');
    expect(content).toContain('trois mois après la clôture de la dernière demande');
    expect(content).toContain('demander l’accès, la rectification ou la suppression');
    expect(content).toContain('volepapillondamour@sfr.fr');
  });

  it('explains how members can access, export or delete their data', async () => {
    const fixture = await renderPage('rights');
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('Vos données et le RGPD');
    expect(content).toContain('Microsoft Entra External ID');
    expect(content).toContain('Portabilité');
    expect(content).toContain('un mois');
    expect(content).toContain('Ne joignez pas de pièce d’identité');
    expect(content).toContain('Ma sélection et les coups de cœur sont supprimés');
    expect(content).toContain('Le lien personnel avec le passage est anonymisé');
    expect(content.toLowerCase()).toContain('les mouvements nécessaires à la traçabilité restent conservés sans identité exploitable');
    expect(content).toContain('Les coups de cœur enregistrés dans votre compte sont des données personnelles');
    expect(content).toContain('supprimés avec le compte et inclus dans l’export de vos données');
    expect(content).toContain('Les coups de cœur anonymes restent dans votre navigateur');
    expect(content).toContain('le serveur reçoit uniquement les ISBN, sans identifiant');
    expect(content).toContain('La scanette affiche ce compteur agrégé, jamais l’identité d’un membre');
    expect(content).toContain('Nous vous proposons des livres de la bourse qui ressemblent à vos coups de cœur');
    expect(content).toContain('Le calcul se fait sur nos serveurs, à partir des notices des livres ; aucune donnée personnelle n’est envoyée à un service extérieur');
    expect(content).toContain('L’e-mail avant la bourse repose sur votre consentement explicite');
    expect(content).toContain('La case est décochée par défaut et le consentement est enregistré avec sa date');
    expect(content).toContain('retirer votre consentement en un clic depuis l’e-mail');
    expect(content).toContain('ou « Compte et données »');

    const requestLink = fixture.nativeElement.querySelector(
      '[data-testid="rights-request"]',
    ) as HTMLAnchorElement | null;
    expect(requestLink?.getAttribute('href')).toContain('mailto:volepapillondamour@sfr.fr');
    expect(fixture.nativeElement.querySelector(
      'a[href="https://www.cnil.fr/fr/plaintes"]',
    )).not.toBeNull();
  });

  it('documents explicit consent and both audience tools on the cookie page', async () => {
    const fixture = await renderPage('cookies');
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('Microsoft Clarity');
    expect(content).toContain('Google Analytics 4');
    expect(content).toContain('Google Maps');
    expect(content).toContain('Ouvrir dans Maps');
    expect(content).toContain('carte interactive');
    expect(content).toContain('consentement dédié');
    expect(content).toContain('consentement explicite');
    expect(content).toContain('Gérer les cookies');
  });

  it('publishes an accessibility status and a contact path', async () => {
    const fixture = await renderPage('accessibility');
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('Accessibilité');
    expect(content).toContain('non conforme');
    expect(content).toContain('volepapillondamour@sfr.fr');
  });
});
