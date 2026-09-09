import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DailyLifeChapterHeaderComponent } from '../daily-life-chapter-header/daily-life-chapter-header.component';

import { SchoolComponent } from './school.component';

describe('SchoolComponent', () => {
  let fixture: ComponentFixture<SchoolComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [SchoolComponent, DailyLifeChapterHeaderComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(SchoolComponent);
    fixture.detectChanges();
  });

  it('should append the college and vocational orientation chapters', () => {
    const sectionTitles = Array.from(
      fixture.nativeElement.querySelectorAll('app-titled-section') as NodeListOf<Element>,
    ).map(section => section.getAttribute('title'));

    expect(sectionTitles).toContain("2016 – L'entrée au collège");
    expect(sectionTitles).toContain('2017 – La 5e et des progrès qui se confirment');
    expect(sectionTitles).toContain('2018 – La 4e, toujours en ULIS');
    expect(sectionTitles).toContain("2019 – « Et maintenant, qu'est-ce que tu veux faire plus tard ? »");
    expect(sectionTitles).toContain('2019-2020 – Une quatrième professionnelle qui change tout');
    expect(sectionTitles).toContain('2020-2021 – Une troisième entre difficultés médicales et brevet des collèges');
    expect(sectionTitles).toContain('2021-2022 – La seconde : gagner en autonomie et trouver sa voie');
    expect(sectionTitles).toContain('2022-2023 – La première : prendre confiance et élargir ses horizons');
    expect(sectionTitles).toContain('2023-2024 – La terminale : le bac… et après ?');
    expect(sectionTitles).toContain('2024-2026 – Le BTS : les années passent et se ressemblent');
    expect(sectionTitles).toContain('Et maintenant, une nouvelle aventure commence…');

    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();
    expect(pageText).toContain('Une nouvelle aventure commence.');
    expect(pageText).toContain('En septembre 2026, Maxence débute son contrat chez Gamm vert de Montbrison');
    expect(pageText).toContain('Maxence est retenu.');
  });

  it('should use the shared chapter header', () => {
    expect(fixture.nativeElement.querySelector('app-daily-life-chapter-header')).not.toBeNull();
  });
});
