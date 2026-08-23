import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';

import { LegalPageComponent } from './legal-page.component';
import { LEGAL_PAGE_PATHS } from './legal-page-paths';

async function renderComponent(legalPagePath: string): Promise<ComponentFixture<LegalPageComponent>> {
  await TestBed.configureTestingModule({
    imports: [LegalPageComponent],
    providers: [
      {
        provide: ActivatedRoute,
        useValue: {
          snapshot: {
            data: {
              legalPagePath
            }
          }
        }
      }
    ]
  }).compileComponents();

  const fixture = TestBed.createComponent(LegalPageComponent);
  fixture.detectChanges();
  return fixture;
}

describe('LegalPageComponent', () => {
  it('should render the legal notice content for the legal notice route', async () => {
    const fixture = await renderComponent(LEGAL_PAGE_PATHS.mentionsLegales);
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('Mentions légales');
    expect(content).toContain("Vole Papillon d'Amour");
    expect(content).toContain('volepapillondamour@sfr.fr');
    expect(content).toContain('[A COMPLETER - responsable de publication]');
  });

  it('should mention Microsoft Clarity on the cookies page', async () => {
    const fixture = await renderComponent(LEGAL_PAGE_PATHS.politiqueCookies);
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('Microsoft Clarity');
    expect(content).toContain('nwy66l4uol');
    expect(content).toContain('Gérer les cookies');
  });
});