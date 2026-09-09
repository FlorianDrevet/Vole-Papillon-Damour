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

  it('should use the updated two-part transplant narrative', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();
    const sectionTitles = Array.from(
      fixture.nativeElement.querySelectorAll('app-titled-section') as NodeListOf<Element>,
    ).map(section => section.getAttribute('title'));

    expect(fixture.nativeElement.querySelector('app-daily-life-chapter-header')).not.toBeNull();
    expect(pageText).toContain('La greffe, deux histoires très différentes');
    expect(pageText).toContain('Quatorze ans plus tard, en juillet 2026');
    expect(pageText).toContain('La greffe multiviscérale est une transplantation particulièrement complexe');
    expect(pageText).toContain('La cornée est la membrane transparente');
    expect(sectionTitles).toContain('Comprendre la greffe multiviscérale');
    expect(sectionTitles).toContain("Qu'est-ce qu'une greffe de cornée ?");
    expect(sectionTitles).not.toContain('Histoire de la greffe intestinale');
  });
});
