import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MaladiesListComponent } from './maladies-list.component';

describe('MaladiesListComponent', () => {
  let fixture: ComponentFixture<MaladiesListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [MaladiesListComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should list seven medical conditions without treating gastrostomy as a disease', () => {
    fixture = TestBed.createComponent(MaladiesListComponent);
    const component = fixture.componentInstance;

    expect(component.diseases.map(disease => disease.name)).toEqual([
      'Maladie de Hirschsprung',
      'P.O.I.C.',
      'Dysplasie ectodermique',
      'Neuropathie',
      'Ostéoporose',
      'Hyperthyroïdie',
      'Wolff-Parkinson-White'
    ]);
  });

  it('should render the rare diseases, ERBB3 cause and nutrition section', () => {
    fixture = TestBed.createComponent(MaladiesListComponent);
    fixture.detectChanges();

    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('3 maladies rares : la maladie de Hirschsprung, la POIC et la dysplasie ectodermique.');
    expect(pageText).toContain('C’est la mutation du gène ERBB3');
    expect(pageText).toContain('La gastrostomie, une forme de nutrition');
  });
});
