import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FshdComponent } from './fshd.component';

describe('FshdComponent', () => {
  let fixture: ComponentFixture<FshdComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [FshdComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(FshdComponent);
    fixture.detectChanges();
  });

  it('should render the FSHD medical sheet and Maxence’s scoliosis note', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('La dystrophie facio-scapulo-humérale (FSHD)');
    expect(pageText).toContain('La dystrophie facio-scapulo-humérale, appelée FSH');
    expect(pageText).toContain("Maxence souffre d'une scoliose en plus");
  });
});
