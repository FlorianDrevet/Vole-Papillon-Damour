import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DailyLifeChapterHeaderComponent } from '../daily-life-chapter-header/daily-life-chapter-header.component';

import { HospitalCareComponent } from './hospital-care.component';

describe('HospitalCareComponent', () => {
  let fixture: ComponentFixture<HospitalCareComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HospitalCareComponent, DailyLifeChapterHeaderComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(HospitalCareComponent);
    fixture.detectChanges();
  });

  it('should render the updated childhood-to-adulthood hospital narrative', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain("Les soins hospitaliers : de l'enfance à l'âge adulte");
    expect(pageText).toContain("Et aujourd'hui ?");
    expect(pageText).toContain("Le passage à l'âge adulte : apprendre à faire confiance à de nouvelles équipes");
    expect(pageText).toContain('syndrome de Wolff-Parkinson-White');
    expect(pageText).toContain('quelle équipe chirurgicale prendrait en charge une anatomie aussi particulière ?');
  });

  it('should use the shared chapter header', () => {
    expect(fixture.nativeElement.querySelector('app-daily-life-chapter-header')).not.toBeNull();
  });
});
