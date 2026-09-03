import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { RouterLink, provideRouter } from '@angular/router';

import { SharedModule } from '../../../shared/shared.module';

import { DailyLifeComponent } from './daily-life.component';

describe('DailyLifeComponent', () => {
  let fixture: ComponentFixture<DailyLifeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [DailyLifeComponent],
      imports: [SharedModule, RouterLink],
      providers: [provideRouter([])],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(DailyLifeComponent);
    fixture.detectChanges();
  });

  it('should present four daily-life chapters and make the school chapter reachable', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();
    const sectionIds = Array.from(
      fixture.nativeElement.querySelectorAll('article[id]') as NodeListOf<HTMLElement>,
    ).map(section => section.id);
    const indexLinks = Array.from(
      fixture.nativeElement.querySelectorAll('nav a') as NodeListOf<HTMLAnchorElement>,
    ).map(link => link.getAttribute('href'));
    const schoolLink = fixture.debugElement
      .queryAll(By.directive(RouterLink))
      .find(link => link.injector.get(RouterLink).urlTree?.toString() === '/maxence/vie-quotidienne/ecole');

    expect(pageText).toContain('Son quotidien, ses combats');
    expect(pageText).toContain("L'école");
    expect(sectionIds).toEqual(['soins-quotidiens', 'soins-hospitaliers', 'ecole', 'greffe']);
    expect(indexLinks).toEqual([
      '/maxence/vie-quotidienne#soins-quotidiens',
      '/maxence/vie-quotidienne#soins-hospitaliers',
      '/maxence/vie-quotidienne#ecole',
      '/maxence/vie-quotidienne#greffe',
    ]);
    expect(schoolLink).toBeDefined();
  });
});
