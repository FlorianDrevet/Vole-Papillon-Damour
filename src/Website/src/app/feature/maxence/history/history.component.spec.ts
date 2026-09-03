import { NO_ERRORS_SCHEMA } from '@angular/core';
import { Location } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';

import { HistoryComponent } from './history.component';

describe('HistoryComponent', () => {
  let fixture: ComponentFixture<HistoryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HistoryComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { fragment: of(null) } },
        { provide: Location, useValue: jasmine.createSpyObj<Location>('Location', ['replaceState']) },
        { provide: Router, useValue: jasmine.createSpyObj<Router>('Router', ['createUrlTree']) }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should separate nutrition from the eight medical conditions', () => {
    fixture = TestBed.createComponent(HistoryComponent);
    fixture.detectChanges();

    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('8 maladies, dont 4 rares');
    expect(pageText).toContain('VOIR LES 8 FICHES');
    expect(pageText).toContain('Des souvenirs plein les yeux');
    expect(pageText).toContain('Nutrition');
    expect(pageText).not.toContain('7 maladies, dont 3 rares');
  });
});
