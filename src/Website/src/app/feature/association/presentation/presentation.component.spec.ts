import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PresentationComponent } from './presentation.component';

describe('PresentationComponent', () => {
  let fixture: ComponentFixture<PresentationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [PresentationComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(PresentationComponent);
    fixture.detectChanges();
  });

  it('should keep the legal identity while removing the board and public documents sections', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('Association loi 1901 · depuis 2010');
    expect(pageText).toContain('cartons de livres');
    expect(pageText).not.toContain('Association familiale');
    expect(pageText).not.toContain('Le bureau');
    expect(pageText).not.toContain('Tout est public');
  });

  it('should use the association photo as the presentation hero image', () => {
    const heroImage = fixture.nativeElement.querySelector('img') as HTMLImageElement;
    const imageSource = heroImage.getAttribute('ngsrc') ?? heroImage.getAttribute('ng-reflect-ng-src') ?? heroImage.src;

    expect(imageSource).toContain('images/Association/asso.jpg');
    expect(heroImage.alt).toContain("Bénévoles et familles réunis");
  });
});
