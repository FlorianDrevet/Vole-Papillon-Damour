import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DailyLifeChapterHeaderComponent } from '../daily-life-chapter-header/daily-life-chapter-header.component';

import { OrgansTransplantComponent } from './organs-transplant.component';

describe('OrgansTransplantComponent', () => {
  let fixture: ComponentFixture<OrgansTransplantComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [OrgansTransplantComponent, DailyLifeChapterHeaderComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(OrgansTransplantComponent);
    fixture.detectChanges();
  });

  it('should use the shared chapter header for the transplant narrative', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(fixture.nativeElement.querySelector('app-daily-life-chapter-header')).not.toBeNull();
    expect(pageText).toContain('La greffe, un espoir éphémère');
  });
});
