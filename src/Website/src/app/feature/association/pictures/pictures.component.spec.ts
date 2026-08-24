import { NgOptimizedImage } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterLink, provideRouter } from '@angular/router';

import { SharedModule } from '../../../shared/shared.module';

import { PicturesComponent } from './pictures.component';

describe('PicturesComponent', () => {
  let component: PicturesComponent;
  let fixture: ComponentFixture<PicturesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [PicturesComponent],
      // Le gabarit s'appuie sur les composants du design system (bouton pilule,
      // encart photo) et sur des liens de navigation : sans eux, Angular ne
      // reconnaît pas les balises et le rendu échoue.
      imports: [SharedModule, NgOptimizedImage, RouterLink],
      providers: [provideRouter([])]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PicturesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
