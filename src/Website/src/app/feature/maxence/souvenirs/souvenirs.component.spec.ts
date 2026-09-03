import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SouvenirsComponent } from './souvenirs.component';

describe('SouvenirsComponent', () => {
  let fixture: ComponentFixture<SouvenirsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [SouvenirsComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(SouvenirsComponent);
    fixture.detectChanges();
  });

  it('should render the requested memory introduction', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(pageText).toContain('Des souvenirs plein les yeux');
    expect(pageText).toContain('des journées à Disneyland ou au Parc Astérix');
    expect(pageText).toContain('ces moments de bonheur ont toujours eu une valeur immense');
  });
});
