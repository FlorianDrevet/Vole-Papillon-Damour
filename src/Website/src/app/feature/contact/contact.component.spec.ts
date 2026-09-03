import { NgOptimizedImage } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterLink, provideRouter } from '@angular/router';

import { SharedModule } from '../../shared/shared.module';

import { ContactComponent } from './contact.component';

describe('ContactComponent', () => {
  let component: ContactComponent;
  let fixture: ComponentFixture<ContactComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ContactComponent],
      // Le gabarit s'appuie sur les composants du design system (bouton pilule,
      // encart photo) et sur des liens de navigation : sans eux, Angular ne
      // reconnaît pas les balises et le rendu échoue.
      imports: [SharedModule, NgOptimizedImage, RouterLink],
      providers: [provideRouter([])]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ContactComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should explain the volunteer response time', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('Ce sont des bénévoles qui vous répondent.... alors comptez deux à trois jours pour avoir une réponse. Merci pour votre compréhension.');
  });
});
