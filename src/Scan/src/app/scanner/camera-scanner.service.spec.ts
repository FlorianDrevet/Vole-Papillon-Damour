import {TestBed} from '@angular/core/testing';

import {
  CAMERA_SCANNER_ENGINE_FACTORY,
  CameraScannerEngine,
  CameraScannerEngineFactory,
  CameraScannerService,
} from './camera-scanner.service';

describe('CameraScannerService', () => {
  let engine: jasmine.SpyObj<CameraScannerEngine>;
  let createEngine: jasmine.Spy<CameraScannerEngineFactory>;
  let service: CameraScannerService;
  let container: HTMLDivElement;

  beforeEach(() => {
    engine = jasmine.createSpyObj<CameraScannerEngine>('CameraScannerEngine', [
      'start',
      'stop',
      'scanFile',
      'clear',
    ]);
    engine.start.and.returnValue(Promise.resolve(null));
    engine.stop.and.returnValue(Promise.resolve());
    engine.scanFile.and.returnValue(Promise.resolve('9782070363735'));
    createEngine = jasmine.createSpy('createEngine').and.returnValue(engine);

    TestBed.configureTestingModule({
      providers: [
        CameraScannerService,
        {
          provide: CAMERA_SCANNER_ENGINE_FACTORY,
          useValue: createEngine,
        },
      ],
    });

    service = TestBed.inject(CameraScannerService);
    container = document.createElement('div');
    container.id = 'camera-test-container';
    document.body.appendChild(container);
  });

  afterEach(() => {
    container.remove();
  });

  it('starts the ZXing-backed engine with the rear camera constraints', async () => {
    const onDetected = jasmine.createSpy('onDetected');

    const handle = await service.start(container, onDetected);

    expect(createEngine).toHaveBeenCalledOnceWith(container.id);
    expect(engine.start).toHaveBeenCalledOnceWith(
      {facingMode: 'environment'},
      jasmine.objectContaining({
        fps: 10,
        qrbox: {width: 280, height: 120},
      }),
      jasmine.any(Function),
      jasmine.any(Function),
    );

    await handle.stop();

    expect(engine.stop).toHaveBeenCalledOnceWith();
    expect(engine.clear).toHaveBeenCalledOnceWith();
  });

  it('forwards a decoded camera value once', async () => {
    const onDetected = jasmine.createSpy('onDetected');
    const handle = await service.start(container, onDetected);
    const successCallback = engine.start.calls.mostRecent().args[2];

    successCallback(' 9782070363735 ', undefined as never);
    successCallback('9782070363735', undefined as never);
    await handle.stop();

    expect(onDetected).toHaveBeenCalledOnceWith('9782070363735');
  });

  it('decodes a photo selected on an iPhone', async () => {
    const photo = new File(['barcode'], 'book.jpg', {type: 'image/jpeg'});

    const value = await service.scanFile(container, photo);

    expect(createEngine).toHaveBeenCalledOnceWith(container.id);
    expect(engine.scanFile).toHaveBeenCalledOnceWith(photo, false);
    expect(engine.clear).toHaveBeenCalledOnceWith();
    expect(value).toBe('9782070363735');
  });
});
