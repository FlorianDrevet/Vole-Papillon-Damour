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

  it('should keep lotoCard null when next bingo request fails', async () => {
    axiosServiceSpy.request.and.returnValue(Promise.reject(new Error('AssoEvent not found for next bingo')));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.lotoCard()).toBeNull();
  });
});