import {ComponentFixture, TestBed} from '@angular/core/testing';
import {Meta} from '@angular/platform-browser';
import {RouterModule} from '@angular/router';

import {CatalogNotFoundPageComponent} from './catalog-not-found-page.component';

describe('CatalogNotFoundPageComponent', () => {
  let fixture: ComponentFixture<CatalogNotFoundPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [CatalogNotFoundPageComponent],
      imports: [RouterModule.forRoot([])],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogNotFoundPageComponent);
    fixture.detectChanges();
  });

  it('renders a real 404 page and prevents indexing', () => {
    expect(fixture.nativeElement.querySelector('h1')?.textContent)
      .toContain('Cette page n’existe pas');
    expect(TestBed.inject(Meta).getTag('name="robots"')?.content)
      .toBe('noindex, nofollow');
  });
});
