import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DailyLifeChapterHeaderComponent } from './daily-life-chapter-header.component';

describe('DailyLifeChapterHeaderComponent', () => {
  let fixture: ComponentFixture<DailyLifeChapterHeaderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [DailyLifeChapterHeaderComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(DailyLifeChapterHeaderComponent);
    fixture.componentRef.setInput('chapter', '01 · À LA MAISON');
    fixture.componentRef.setInput('title', 'Les soins quotidiens à la maison');
    fixture.componentRef.setInput('intro', 'Un court résumé du chapitre.');
    fixture.detectChanges();
  });

  it('should render the chapter context, introduction and return action', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();
    const backLink = fixture.nativeElement.querySelector('a[routerLink="/maxence/vie-quotidienne"]');

    expect(pageText).toContain('01 · À LA MAISON');
    expect(pageText).toContain('Les soins quotidiens à la maison');
    expect(pageText).toContain('Un court résumé du chapitre.');
    expect(pageText).toContain('Retour à Son quotidien, ses combats');
    expect(backLink).not.toBeNull();
  });
});
