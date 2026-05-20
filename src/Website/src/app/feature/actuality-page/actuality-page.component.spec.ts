import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ActualityPageComponent } from './actuality-page.component';
import { AxiosService } from '../../shared/services/axios.service';

describe('ActualityPageComponent', () => {
  let component: ActualityPageComponent;
  let fixture: ComponentFixture<ActualityPageComponent>;
  let axiosServiceSpy: jasmine.SpyObj<AxiosService>;

  beforeEach(async () => {
    axiosServiceSpy = jasmine.createSpyObj<AxiosService>('AxiosService', ['request']);
    axiosServiceSpy.request.and.returnValue(Promise.resolve([]));

    await TestBed.configureTestingModule({
      declarations: [ActualityPageComponent],
      providers: [
        { provide: AxiosService, useValue: axiosServiceSpy }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(ActualityPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('should not render the mailing list signup call to action', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.textContent).not.toContain("L'actualité par mail !");
  });
});