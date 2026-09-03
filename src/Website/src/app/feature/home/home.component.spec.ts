import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HomeComponent } from './home.component';
import { AxiosService } from '../../shared/services/axios.service';

describe('HomeComponent', () => {
  let component: HomeComponent;
  let fixture: ComponentFixture<HomeComponent>;
  let axiosServiceSpy: jasmine.SpyObj<AxiosService>;

  beforeEach(async () => {
    axiosServiceSpy = jasmine.createSpyObj<AxiosService>('AxiosService', ['request']);

    await TestBed.configureTestingModule({
      declarations: [HomeComponent],
      providers: [
        { provide: AxiosService, useValue: axiosServiceSpy }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(HomeComponent);
    component = fixture.componentInstance;
  });

  // Les cartes de l'agenda sont désormais portées par VpdEventSections : leur
  // chargement est couvert par vpd-event-sections.spec.ts.
  it('should create', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
  });

  it('should invite people to give some of their time without mentioning a market', () => {
    fixture.detectChanges();

    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('un peu de votre temps libre');
    expect(pageText).toContain('DONNER UN PEU DE VOTRE TEMPS');
    expect(pageText).not.toContain('DEUX HEURES DE VOTRE TEMPS');
    expect(pageText).not.toContain('marché de Noël');
  });
});
