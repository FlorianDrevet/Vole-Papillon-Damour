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
    expect(content).toContain('liste de recherche');
    expect(content).toContain('alertes');
    expect(content).toContain('Microsoft Entra');
    expect(content).toContain('Google Analytics 4');
  });

  it('documents explicit consent and both audience tools on the cookie page', async () => {
    const fixture = await renderPage('cookies');
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('Microsoft Clarity');
    expect(content).toContain('Google Analytics 4');
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
