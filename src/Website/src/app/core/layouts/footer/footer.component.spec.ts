import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { RouterLink, provideRouter } from '@angular/router';

import { LegalPageComponent } from '../../../feature/legal/legal-page.component';
import { LEGAL_PAGE_PATHS } from '../../../feature/legal/legal-page-paths';
import { FooterComponent } from './footer.component';

describe('FooterComponent', () => {
  let component: FooterComponent;
  let fixture: ComponentFixture<FooterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [FooterComponent],
      imports: [
        LegalPageComponent,
        RouterLink
      ],
      providers: [
        provideRouter([
          {
            path: LEGAL_PAGE_PATHS.mentionsLegales,
            component: LegalPageComponent,
            data: {
              legalFooterLabel: 'Mentions légales'
            }
          },
          {
            path: LEGAL_PAGE_PATHS.politiqueConfidentialite,
            component: LegalPageComponent,
            data: {
              legalFooterLabel: 'Confidentialité'
            }
          },
          {
            path: LEGAL_PAGE_PATHS.politiqueCookies,
            component: LegalPageComponent,
            data: {
              legalFooterLabel: 'Cookies'
            }
          },
          {
            path: LEGAL_PAGE_PATHS.accessibilite,
            component: LegalPageComponent,
            data: {
              legalFooterLabel: 'Accessibilité',
              legalFooterStatus: 'non conforme'
            }
          }
        ])
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(FooterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should render the legal links declared in the router configuration', () => {
    // Le pied de page contient aussi le plan du site : on ne regarde que le bloc
    // des liens légaux, seul à être construit depuis la configuration du routeur.
    const legalLinks = fixture.debugElement
      .query(By.css('[data-testid="legal-links"]'))
      .queryAll(By.directive(RouterLink))
      .map((linkDebugElement) => ({
        text: linkDebugElement.nativeElement.textContent.replace(/\s+/g, ' ').trim(),
        url: linkDebugElement.injector.get(RouterLink).urlTree?.toString()
      }))
      .filter((link) => link.url?.startsWith('/'));

    expect(component).toBeTruthy();
    expect(legalLinks).toEqual([
      {
        text: 'Mentions légales',
        url: `/${LEGAL_PAGE_PATHS.mentionsLegales}`
      },
      {
        text: 'Confidentialité',
        url: `/${LEGAL_PAGE_PATHS.politiqueConfidentialite}`
      },
      {
        text: 'Cookies',
        url: `/${LEGAL_PAGE_PATHS.politiqueCookies}`
      },
      {
        text: 'Accessibilité : non conforme',
        url: `/${LEGAL_PAGE_PATHS.accessibilite}`
      }
    ]);
  });
});