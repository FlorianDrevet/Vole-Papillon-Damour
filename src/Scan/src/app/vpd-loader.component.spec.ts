import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';

import {VpdLoaderComponent} from '../../../SharedUi/src/lib/components/vpd-loader/vpd-loader.component';

describe('VpdLoaderComponent', () => {
  let fixture: ComponentFixture<VpdLoaderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [VpdLoaderComponent],
      imports: [CommonModule],
    }).compileComponents();

    fixture = TestBed.createComponent(VpdLoaderComponent);
    fixture.componentInstance.logoSrc = 'assets/papillon_without_back.png';
  });

  it('renders the 1b flight composition with its accessible status copy', () => {
    const loader = fixture.componentInstance;
    loader.variant = 'flight';
    loader.logoSrc = 'assets/papillon_without_back.png';
    loader.title = 'Tri des livres';
    loader.detail = 'Préparation de la session';
    loader.label = 'Préparation de la session de tri';
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-loader="flight"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('img')?.getAttribute('src'))
      .toBe('assets/papillon_without_back.png');
    expect(fixture.nativeElement.textContent).toContain('Tri des livres');
    expect(fixture.nativeElement.textContent).toContain('Préparation de la session');
    expect(fixture.nativeElement.querySelector('[role="progressbar"]')).toBeNull();
  });

  it('keeps skeleton counts bounded and switches to the list layout when requested', () => {
    const loader = fixture.componentInstance;
    loader.variant = 'skeleton';
    loader.count = 20;
    loader.layout = 'list';
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.vpd-loader-skeleton-card').length).toBe(12);
    expect(fixture.nativeElement.querySelector('.vpd-loader-skeletons--list')).not.toBeNull();
  });

  it('clamps determinate progress to a valid progressbar range', () => {
    const loader = fixture.componentInstance;
    loader.variant = 'fill';
    loader.progress = 140;
    loader.label = 'Import des livres';
    fixture.detectChanges();

    const progressbar = fixture.nativeElement.querySelector('[role="progressbar"]') as HTMLElement;
    expect(progressbar.getAttribute('aria-valuenow')).toBe('100');
    expect(fixture.nativeElement.querySelector('.vpd-loader-fill__progress')?.getAttribute('style'))
      .toContain('inset(0%');
  });

  it('exposes the compact ring size for action feedback', () => {
    const loader = fixture.componentInstance;
    loader.variant = 'ring';
    loader.size = 'small';
    loader.label = 'Ajout en cours';
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.vpd-loader-ring--small')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="Ajout en cours"]')).not.toBeNull();
  });
});
