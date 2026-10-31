import {NO_ERRORS_SCHEMA} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {MatDialog} from '@angular/material/dialog';

import {CaisseComponent} from './caisse.component';

describe('CaisseComponent', () => {
  let fixture: ComponentFixture<CaisseComponent>;

  beforeEach(async () => {
    const dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    await TestBed.configureTestingModule({
      declarations: [CaisseComponent],
      providers: [{provide: MatDialog, useValue: dialog}],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(CaisseComponent);
    fixture.detectChanges();
  });

  it('shows a labeled APK download link on the product management screen', () => {
    const downloadLink = fixture.nativeElement.querySelector('a[download]') as HTMLAnchorElement | null;

    expect(downloadLink?.getAttribute('href')).toBe('/apks/app-debug.apk');
    expect(downloadLink?.getAttribute('download')).toBe('caisse-app.apk');
    expect(downloadLink?.textContent).toContain('Télécharger l’APK');
  });
});
