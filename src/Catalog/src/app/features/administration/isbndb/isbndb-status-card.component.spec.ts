import {provideZonelessChangeDetection} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';

import {CatalogAdminIsbndbStatus} from '../../../core/catalog.models';
import {IsbndbStatusCardComponent} from './isbndb-status-card.component';

describe('IsbndbStatusCardComponent', () => {
  let fixture: ComponentFixture<IsbndbStatusCardComponent>;

  const status: CatalogAdminIsbndbStatus = {
    dailyLimit: 1000,
    dailyRemaining: 640,
    observedAt: '2026-07-01T10:05:00Z',
    resetsAt: '2026-07-02T00:00:00Z',
    status: 'Operational',
    exhaustedUntil: null,
    usageToday: [
      {usage: 'CatchUp', credits: 120},
      {usage: 'Scan', credits: 180},
      {usage: 'PublicSearch', credits: 40},
      {usage: 'Administration', credits: 20},
    ],
    pendingCatchUp: {total: 50, inStock: 12, beforeMigration: 30},
    absentToComplete: 7,
    lastRun: {
      startedAt: '2026-07-01T00:30:00Z',
      endedAt: '2026-07-01T00:32:00Z',
      filled: 3,
      absent: 2,
      remaining: 45,
      stopReason: 'QuotaExhausted',
    },
    unmatchedSubjects: [{subject: 'Fantasy épique', count: 9}],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [IsbndbStatusCardComponent],
      imports: [RouterModule.forRoot([])],
      providers: [provideZonelessChangeDetection()],
    }).compileComponents();
    fixture = TestBed.createComponent(IsbndbStatusCardComponent);
    fixture.componentRef.setInput('status', status);
  });

  it('shows ISBNdb quota, usage, catch-up details, and unmatched subjects', () => {
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Source des fiches');
    expect(text).toContain('Opérationnel');
    expect(text).toContain('Crédits restants aujourd’hui');
    expect(text).toContain('640 / 1 000');
    expect(text).toContain('Renouvellement cette nuit à 02:00 (heure de Paris)');
    expect(text).toContain('Lu sur la réponse ISBNdb de 12:05');
    expect(text).toContain('Rattrapage nocturne');
    expect(text).toContain('Recherche publique');
    expect(text).toContain('En attente du rattrapage');
    expect(text).toContain('12 en stock · 30 d’avant la migration');
    expect(text).toContain('7 livres hors ISBNdb à compléter');
    expect(text).toContain('Quota épuisé — reprise la nuit prochaine');
    expect(text).toContain('Fantasy épique');
  });

  it('shows exhausted and unavailable quota states with Paris time', () => {
    fixture.componentRef.setInput('status', {
      ...status,
      status: 'Exhausted',
      exhaustedUntil: '2026-07-01T18:45:00Z',
    });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Quota épuisé jusqu’à 20:45');

    fixture.componentRef.setInput('status', {...status, status: 'Unavailable'});
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Service indisponible');
  });
});
