import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';

import {ScanStatusBarComponent} from './scan-status-bar.component';

describe('ScanStatusBarComponent', () => {
  let fixture: ComponentFixture<ScanStatusBarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ScanStatusBarComponent],
      imports: [CommonModule],
    }).compileComponents();

    fixture = TestBed.createComponent(ScanStatusBarComponent);
  });

  it('renders a compact actionable strip instead of the legacy synchronization panel', () => {
    const bar = fixture.componentInstance;
    bar.offline = true;
    bar.actionRequired = true;
    bar.detail = '3 livres restent sur cet appareil.';
    fixture.detectChanges();

    const strip = fixture.nativeElement.querySelector('.status-strip') as HTMLElement | null;
    expect(strip).not.toBeNull();
    expect(strip?.textContent).toContain('(hors connexion)');
    expect(strip?.textContent).toContain('(action à faire)');
    expect(strip?.textContent).toContain('3 livres restent sur cet appareil.');
    expect(fixture.nativeElement.querySelector('.status-action')).toBeNull();
    expect(fixture.nativeElement.querySelector('.status-catalog')).toBeNull();
  });

  it('emits a status request when the compact strip is opened', () => {
    const bar = fixture.componentInstance;
    bar.offline = false;
    bar.actionRequired = true;
    bar.detail = 'La dernière synchronisation doit être vérifiée.';
    let requested = false;
    bar.statusRequested.subscribe(() => requested = true);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.status-strip') as HTMLButtonElement).click();

    expect(requested).toBeTrue();
  });
});
