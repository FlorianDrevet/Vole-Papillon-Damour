import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DesignSystemModule } from '@vpd/ui';

import { ActualityComponent } from './actuality.component';
import { AxiosService } from '../../../../shared/services/axios.service';

describe('ActualityComponent', () => {
  let component: ActualityComponent;
  let fixture: ComponentFixture<ActualityComponent>;
  let axiosServiceSpy: jasmine.SpyObj<AxiosService>;

  beforeEach(async () => {
    axiosServiceSpy = jasmine.createSpyObj<AxiosService>('AxiosService', ['request']);

    await TestBed.configureTestingModule({
      declarations: [ActualityComponent],
      imports: [DesignSystemModule],
      providers: [
        { provide: AxiosService, useValue: axiosServiceSpy }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should keep actualities empty when latest actuality request fails', async () => {
    axiosServiceSpy.request.and.returnValue(Promise.reject(new Error('Latest actuality unavailable')));

    fixture = TestBed.createComponent(ActualityComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.actualities()).toEqual([]);
  });

  it('should render three skeleton cards while latest actualities are loading', () => {
    let resolveRequest!: (actualities: unknown[]) => void;
    axiosServiceSpy.request.and.returnValue(new Promise(resolve => {
      resolveRequest = resolve;
    }));

    fixture = TestBed.createComponent(ActualityComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.vpd-loader-skeleton-card').length).toBe(3);

    resolveRequest([]);
  });
});
