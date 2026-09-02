import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DailyCareComponent } from './daily-care.component';

describe('DailyCareComponent', () => {
  let fixture: ComponentFixture<DailyCareComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [DailyCareComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should describe the enteral nutrition timeline as 2015 and 2016', () => {
    fixture = TestBed.createComponent(DailyCareComponent);
    fixture.detectChanges();

    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('sachant qu’en 2015 et 2016,');
    expect(pageText).not.toContain('sachant que depuis août 2015');
  });

  it('should render the current parenteral nutrition routine and care details', () => {
    fixture = TestBed.createComponent(DailyCareComponent);
    fixture.detectChanges();

    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('15 h tous les jours pour Maxence');
    expect(pageText).toContain('en fonction de ses besoins et de son poids');
    expect(pageText).toContain('dormir en position semi-assise');
    expect(pageText).toContain('Inipomp 80 mg');
  });
});
