import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MaxenceIntroComponent } from './maxence-intro.component';

describe('MaxenceIntroComponent', () => {
  let fixture: ComponentFixture<MaxenceIntroComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [MaxenceIntroComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should render the updated Maxence introduction copy', () => {
    fixture = TestBed.createComponent(MaxenceIntroComponent);
    fixture.detectChanges();

    const paragraphText = fixture.nativeElement.querySelector('p')?.textContent
      ?.replace(/\s+/g, ' ')
      .trim();

    expect(paragraphText).toBe(
      'Une mutation cellulaire jamais décrite ailleurs dans le monde, 7 maladies dont plusieurs rares, un quotidien rythmé par les soins, examens, septicémies, blocs opératoires et hospitalisations. Ses parents ont créé l’association en 2010, afin de financer du matériel adapté, favoriser son autonomie et lui offrir les meilleures conditions de vie, possible. Né autour de Maxence et de son histoire, cette mobilisation a grandi avec les années et permet aujourd’hui d’aider des enfants, adolescents et très jeunes adultes confrontés à la maladie ou au handicap.'
    );
  });

  it('should render the current seven-condition statistic', () => {
    fixture = TestBed.createComponent(MaxenceIntroComponent);
    fixture.detectChanges();

    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('7MALADIES');
    expect(pageText).not.toContain('8MALADIES RARES');
  });
});
