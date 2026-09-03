import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HirschsprungComponent } from './hirschsprung.component';

describe('HirschsprungComponent', () => {
  let fixture: ComponentFixture<HirschsprungComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HirschsprungComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should render the syndromic ERBB3 explanation requested for Maxence', () => {
    fixture = TestBed.createComponent(HirschsprungComponent);
    fixture.detectChanges();

    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('cellules ganglionnaires');
    const sectionTitles = Array.from(
      fixture.nativeElement.querySelectorAll('app-titled-section') as NodeListOf<Element>,
    ).map(section => section.getAttribute('title'));

    expect(fixture.nativeElement.querySelector('h2')?.textContent).toContain('Quand Hirschsprung n’est que la partie visible de la maladie');
    expect(pageText).toContain('mutation du gène ERBB3');
    expect(sectionTitles).toContain("Une maladie qui ne touche pas seulement l'intestin");
    expect(pageText).toContain('maladie multisystémique');
    expect(pageText).toContain('sous forme syndromique liée au gène ERBB3');
    expect(pageText).toContain('comme dans un embouteillage');
    expect(pageText).toContain('vomissements peuvent devenir bilieux');
  });
});
