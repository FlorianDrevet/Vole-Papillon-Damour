import {TestBed} from '@angular/core/testing';
import {BarcodeFormat, DecodeHintType, Result} from '@zxing/library';

import {
  CAMERA_SCANNER_ENGINE_FACTORY,
  CameraScannerReader,
  CameraScannerReaderFactory,
  CameraScannerEngine,
  CameraScannerEngineFactory,
  CameraScannerService,
  ZxingCameraScannerEngine,
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
    ]);
    engine.start.and.returnValue(Promise.resolve());
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

  it('starts the camera engine with the scanner container', async () => {
    const onDetected = jasmine.createSpy('onDetected');

    const handle = await service.start(container, onDetected);

    expect(createEngine as jasmine.Spy).toHaveBeenCalledOnceWith();
    expect(engine.start as jasmine.Spy).toHaveBeenCalledOnceWith(container, jasmine.any(Function));

    await handle.stop();

    expect(engine.stop).toHaveBeenCalledOnceWith();
  });

  it('forwards a decoded camera value once', async () => {
    const onDetected = jasmine.createSpy('onDetected');
    const handle = await service.start(container, onDetected);
    const successCallback = engine.start.calls.mostRecent().args[1] as unknown as (rawValue: string) => void;

    successCallback(' 9782070363735 ');
    successCallback('9782070363735');
    await handle.stop();

    expect(onDetected).toHaveBeenCalledOnceWith('9782070363735');
  });

  it('decodes a photo selected on an iPhone', async () => {
    const photo = new File(['barcode'], 'book.jpg', {type: 'image/jpeg'});

    const value = await service.scanFile(photo);

    expect(createEngine as jasmine.Spy).toHaveBeenCalledOnceWith();
    expect(engine.scanFile).toHaveBeenCalledOnceWith(photo);
    expect(value).toBe('9782070363735');
  });
});

describe('ZxingCameraScannerEngine', () => {
  let reader: jasmine.SpyObj<CameraScannerReader>;
  let createReader: jasmine.Spy<CameraScannerReaderFactory>;
  let controls: {stop: jasmine.Spy};
  let container: HTMLDivElement;

  beforeEach(() => {
    reader = jasmine.createSpyObj<CameraScannerReader>('CameraScannerReader', [
      'decodeFromConstraints',
      'decodeFromImageUrl',
      'decodeFromCanvas',
    ]);
    controls = {stop: jasmine.createSpy('stop')};
    reader.decodeFromConstraints.and.returnValue(Promise.resolve(controls));
    reader.decodeFromImageUrl.and.returnValue(Promise.resolve(
      jasmine.createSpyObj<Result>('Result', ['getText']),
    ));
    createReader = jasmine.createSpy('createReader').and.returnValue(reader);
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  afterEach(() => {
    container.remove();
  });

  it('configures try-harder decoding for ISBN and QR formats', async () => {
    const engine = new ZxingCameraScannerEngine(createReader);

    await engine.start(container, jasmine.createSpy('onDetected'));

    const hints = createReader.calls.mostRecent().args[0];
    expect(hints.get(DecodeHintType.TRY_HARDER)).toBeTrue();
    expect(hints.get(DecodeHintType.POSSIBLE_FORMATS)).toEqual([
      BarcodeFormat.QR_CODE,
      BarcodeFormat.EAN_13,
      BarcodeFormat.EAN_8,
      BarcodeFormat.UPC_A,
      BarcodeFormat.UPC_E,
    ]);
    expect(reader.decodeFromConstraints).toHaveBeenCalledOnceWith(
      {video: {facingMode: 'environment'}},
      jasmine.any(HTMLVideoElement),
      jasmine.any(Function),
    );
  });

  it('forwards the first decoded value and stops the active camera', async () => {
    const onDetected = jasmine.createSpy('onDetected');
    const result = jasmine.createSpyObj<Result>('Result', ['getText']);
    result.getText.and.returnValue('9782070363735');
    const engine = new ZxingCameraScannerEngine(createReader);

    await engine.start(container, onDetected);
    const decodeCallback = reader.decodeFromConstraints.calls.mostRecent().args[2];
    decodeCallback(result, undefined, controls);
    decodeCallback(result, undefined, controls);

    expect(onDetected).toHaveBeenCalledOnceWith('9782070363735');
    expect(controls.stop).toHaveBeenCalledOnceWith();
  });

  it('falls back to cropped photo decoding when the full photo cannot be decoded', async () => {
    const result = jasmine.createSpyObj<Result>('Result', ['getText']);
    result.getText.and.returnValue('9782070363735');
    reader.decodeFromImageUrl.and.returnValue(Promise.reject(new Error('not found')));
    reader.decodeFromCanvas.and.returnValue(result);
    spyOn(URL, 'createObjectURL').and.returnValue(
      `data:image/svg+xml,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1800"></svg>')}`,
    );
    spyOn(URL, 'revokeObjectURL');
    const engine = new ZxingCameraScannerEngine(createReader);

    const value = await engine.scanFile(new File(['barcode'], 'book.jpg', {type: 'image/jpeg'}));

    expect(reader.decodeFromCanvas).toHaveBeenCalled();
    expect(value).toBe('9782070363735');
  });

  it('tries a thresholded photo candidate when screen artifacts prevent direct decoding', async () => {
    const result = jasmine.createSpyObj<Result>('Result', ['getText']);
    result.getText.and.returnValue('9782070363735');
    reader.decodeFromImageUrl.and.returnValue(Promise.reject(new Error('not found')));
    reader.decodeFromCanvas.and.callFake(canvas => {
      const pixel = canvas.getContext('2d')?.getImageData(0, 0, 1, 1).data[0];
      if (pixel !== 0 && pixel !== 255) {
        throw new Error('candidate is not thresholded');
      }
      return result;
    });
    spyOn(URL, 'createObjectURL').and.returnValue(
      `data:image/svg+xml,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1800"><rect width="1200" height="1800" fill="rgb(128,128,128)"/></svg>')}`,
    );
    spyOn(URL, 'revokeObjectURL');
    const engine = new ZxingCameraScannerEngine(createReader);

    const value = await engine.scanFile(new File(['barcode'], 'book.jpg', {type: 'image/jpeg'}));

    expect(value).toBe('9782070363735');
  });
});
