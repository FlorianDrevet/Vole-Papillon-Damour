import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HirschsprungComponent } from './hirschsprung.component';

describe('HirschsprungComponent', () => {
  let fixture: ComponentFixture<HirschsprungComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HirschsprungComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should render the updated explanation and Maxence’s rare form', () => {
    fixture = TestBed.createComponent(HirschsprungComponent);
    fixture.detectChanges();

    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('cellules ganglionnaires');
    expect(pageText).toContain('forme étagée');
    expect(pageText).toContain('skip-segment Hirschsprung disease');
    expect(pageText).toContain('comme dans un embouteillage');
    expect(pageText).toContain('vomissements peuvent devenir bilieux');
  });
});
