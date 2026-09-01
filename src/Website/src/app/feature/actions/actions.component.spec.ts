import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterLink, provideRouter } from '@angular/router';

import { SharedModule } from '../../shared/shared.module';

import { ActionsComponent } from './actions.component';

describe('ActionsComponent', () => {
  let fixture: ComponentFixture<ActionsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ActionsComponent],
      imports: [SharedModule, RouterLink],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(ActionsComponent);
    fixture.detectChanges();
  });

  it('renders the five concrete actions without numeric summary cards', () => {
    const page = fixture.nativeElement as HTMLElement;
    const text = page.textContent?.replace(/\s+/g, ' ').trim() ?? '';

    expect(text).toContain('Voici ce que votre aide et votre générosité rendent possible.');
    expect(text).toContain('Financement de séances d’équithérapie en 2025 pour 12 enfants d’une classe ULIS de Feurs');
    expect(text).toContain('Financement d’ordinateurs portables à de jeunes lycéennes avec le Kiwanis');
    expect(text).toContain('Participation à la récolte de jouets au profit du Père Noël du lundi');
    expect(text).toContain('Financement de nombreux jouets offerts au Père Noël du lundi qui les redistribue au sein des hôpitaux pédiatriques de la Loire');
    expect(text).toContain('Financement de permis de conduire à de jeunes étudiants');
    expect(page.querySelectorAll('app-action-card').length).toBe(5);
    expect(page.querySelectorAll('app-stat-card').length).toBe(0);
  });
});
