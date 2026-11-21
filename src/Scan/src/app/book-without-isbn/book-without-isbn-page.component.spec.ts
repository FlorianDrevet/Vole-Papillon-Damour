import {NO_ERRORS_SCHEMA} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute} from '@angular/router';
import {RouterTestingModule} from '@angular/router/testing';
import {FormsModule} from '@angular/forms';

import {BookWithoutIsbnService} from './book-without-isbn.service';
import {BookWithoutIsbnPageComponent} from './book-without-isbn-page.component';

describe('BookWithoutIsbnPageComponent', () => {
  let routeSnapshot: {data: Record<string, unknown>};
  let fixture: ComponentFixture<BookWithoutIsbnPageComponent>;

  beforeEach(async () => {
    routeSnapshot = {data: {kind: 'ordinary'}};

    await TestBed.configureTestingModule({
      declarations: [BookWithoutIsbnPageComponent],
      imports: [CommonModule, FormsModule, RouterTestingModule],
      providers: [
        {provide: ActivatedRoute, useValue: {snapshot: routeSnapshot}},
        {provide: BookWithoutIsbnService, useValue: {}},
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(BookWithoutIsbnPageComponent);
  });

  it('shows a single ordinary-book form without a type switch', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-zone="E2-bascule-type"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-zone="E2-formulaire-ordinaire"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.book-without-isbn-panel')).not.toBeNull();
    expect(getComputedStyle(fixture.nativeElement.querySelector('.book-without-isbn-screen')).overflowY)
      .toBe('auto');
  });

  it('uses the rare-book form from the dedicated rare route without showing a type switch', () => {
    routeSnapshot.data = {kind: 'rare'};
    const rareFixture = TestBed.createComponent(BookWithoutIsbnPageComponent);
    rareFixture.detectChanges();

    expect(rareFixture.nativeElement.querySelector('[data-zone="E2-bascule-type"]')).toBeNull();
    expect(rareFixture.nativeElement.querySelector('[data-zone="E2-formulaire-rare"]')).not.toBeNull();
    expect(rareFixture.nativeElement.textContent).toContain('Prix ferme');
    rareFixture.destroy();
  });
});
