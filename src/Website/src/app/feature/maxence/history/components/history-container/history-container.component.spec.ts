import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterLink, provideRouter } from '@angular/router';

import { HistoryReaderService } from '../../history-reader.service';

import { HistoryContainerComponent } from './history-container.component';

describe('HistoryContainerComponent', () => {
  let fixture: ComponentFixture<HistoryContainerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HistoryContainerComponent],
      imports: [RouterLink],
      providers: [HistoryReaderService, provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(HistoryContainerComponent);
    fixture.componentRef.setInput('Year', 2005);
    fixture.componentRef.setInput('Title', 'Maxence a 1 an');
    fixture.detectChanges();
  });

  it('should link the end of a history chapter to its matching photo album', () => {
    const link = fixture.nativeElement.querySelector('[data-history-album-link]') as HTMLAnchorElement | null;

    expect(link).not.toBeNull();
    expect(link?.getAttribute('href')).toBe('/association/photos/maxence-2005');
    expect(link?.textContent).toContain('Voir les photos de 2005');
  });
});
