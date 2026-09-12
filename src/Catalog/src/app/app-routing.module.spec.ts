import {TestBed} from '@angular/core/testing';
import {Router} from '@angular/router';

import {LegalPageComponent} from './features/legal/legal-page.component';
import {AppRoutingModule} from './app-routing.module';

describe('AppRoutingModule', () => {
  it('publishes a dedicated personal-data rights page', () => {
    TestBed.configureTestingModule({imports: [AppRoutingModule]});

    const route = TestBed.inject(Router).config.find(item => item.path === 'donnees-personnelles');

    expect(route?.component).toBe(LegalPageComponent);
    expect(route?.data?.['page']).toBe('rights');
    expect(route?.title).toBe('Vos données et le RGPD | Vole Papillon d’Amour');
  });
});
