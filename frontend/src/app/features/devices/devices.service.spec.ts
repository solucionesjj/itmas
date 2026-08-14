import { TestBed } from '@angular/core/testing';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { DevicesService } from './devices.service';
import { CreateDeviceResponse, RotateDeviceKeyResponse } from './device.model';

describe('DevicesService', () => {
  let service: DevicesService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/devices`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(DevicesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('create() POSTs to /devices and returns the one-time apiKey response', () => {
    const response: CreateDeviceResponse = {
      deviceId: 'device-1',
      hostname: 'PC-001',
      category: 'collaborator',
      apiKey: 'device-1.super-secret'
    };

    service
      .create({ hostname: 'PC-001', category: 'collaborator' })
      .subscribe((result) => {
        expect(result).toEqual(response);
      });

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      hostname: 'PC-001',
      category: 'collaborator'
    });
    req.flush(response);
  });

  it('rotateKey() POSTs to /devices/:id/rotate-key and returns the new apiKey', () => {
    const response: RotateDeviceKeyResponse = {
      deviceId: 'device-1',
      apiKey: 'device-1.new-secret'
    };

    service.rotateKey('device-1').subscribe((result) => {
      expect(result).toEqual(response);
    });

    const req = httpMock.expectOne(`${baseUrl}/device-1/rotate-key`);
    expect(req.request.method).toBe('POST');
    req.flush(response);
  });

  it('list() sends category/hostname/osName/page/limit as query params', () => {
    service
      .list({ category: 'infrastructure', hostname: 'srv', osName: 'linux', page: 2, limit: 10 })
      .subscribe();

    const req = httpMock.expectOne(
      (r) =>
        r.url === baseUrl &&
        r.params.get('category') === 'infrastructure' &&
        r.params.get('hostname') === 'srv' &&
        r.params.get('osName') === 'linux' &&
        r.params.get('page') === '2' &&
        r.params.get('limit') === '10'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 2, limit: 10 });
  });
});
