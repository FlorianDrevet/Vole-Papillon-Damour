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

function attachFakeCameraTrack(video: HTMLVideoElement, track: EventTarget): void {
  let source: MediaStream | null = {
    getVideoTracks: () => [track as unknown as MediaStreamTrack],
  } as unknown as MediaStream;
  Object.defineProperty(video, 'srcObject', {
    configurable: true,
    get: () => source,
    set: (value: MediaStream | null) => {
      source = value;
    },
  });
}

describe('CameraScannerService', () => {
  let engine: jasmine.SpyObj<CameraScannerEngine>;
  let createEngine: jasmine.Spy<CameraScannerEngineFactory>;
  let service: CameraScannerService;
  let container: HTMLDivElement;

  beforeEach(() => {
    engine = jasmine.createSpyObj<CameraScannerEngine>('CameraScannerEngine', [
      'start',
      'resume',
      'refocus',
      'stop',
      'scanFile',
    ]);
    engine.start.and.returnValue(Promise.resolve());
    engine.refocus.and.returnValue(Promise.resolve(true));
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
    expect(engine.start as jasmine.Spy).toHaveBeenCalledOnceWith(
      container,
      jasmine.any(Function),
      jasmine.any(Function),
    );

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

  it('resumes a paused scan without requesting a new camera engine', async () => {
    const onDetected = jasmine.createSpy('onDetected');
    const handle = await service.start(container, onDetected);
    const successCallback = engine.start.calls.mostRecent().args[1] as unknown as (rawValue: string) => void;

    successCallback('9782070363735');
    handle.resume();
    successCallback('9780306406157');
    await handle.stop();

    expect(createEngine).toHaveBeenCalledOnceWith();
    expect(engine.resume).toHaveBeenCalledOnceWith();
    expect(onDetected).toHaveBeenCalledTimes(2);
  });

  it('refocuses the active camera without creating a new camera engine', async () => {
    const handle = await service.start(container, jasmine.createSpy('onDetected'));

    const didRefocus = await handle.refocus();

    expect(didRefocus).toBeTrue();
    expect(createEngine).toHaveBeenCalledOnceWith();
    expect(engine.refocus).toHaveBeenCalledOnceWith();
  });

  it('decodes a photo selected on an iPhone', async () => {
    const photo = new File(['barcode'], 'book.jpg', {type: 'image/jpeg'});

    const value = await service.scanFile(photo);

    expect(createEngine as jasmine.Spy).toHaveBeenCalledOnceWith();
    expect(engine.scanFile).toHaveBeenCalledOnceWith(photo);
    expect(value).toBe('9782070363735');
  });

  it('uses a browser-neutral message when camera permission is denied', async () => {
    engine.start.and.returnValue(Promise.reject({name: 'NotAllowedError'}));

    await expectAsync(service.start(container, jasmine.createSpy('onDetected')))
      .toBeRejectedWithError('Autorisez l’accès à la caméra dans les réglages du navigateur, puis réessayez.');
  });

  it('surfaces a recoverable error reported after the camera has started', async () => {
    const onCameraError = jasmine.createSpy('onCameraError');
    const onDetected = jasmine.createSpy('onDetected');
    const handle = await service.start(container, onDetected, onCameraError);
    const reportCameraError = engine.start.calls.mostRecent().args[2] as (error: unknown) => void;
    const decodeValue = engine.start.calls.mostRecent().args[1] as (rawValue: string) => void;

    reportCameraError({name: 'NotAllowedError'});
    decodeValue('9782070363735');

    expect(onCameraError).toHaveBeenCalledOnceWith(jasmine.objectContaining({
      message: 'Autorisez l’accès à la caméra dans les réglages du navigateur, puis réessayez.',
    }));
    expect(onDetected).not.toHaveBeenCalled();

    await handle.stop();
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

  it('keeps the stream when the first barcode is decoded before startup resolves', async () => {
    const result = jasmine.createSpyObj<Result>('Result', ['getText']);
    result.getText.and.returnValue('9782070363735');
    reader.decodeFromConstraints.and.callFake(async (_constraints, _preview, callback) => {
      callback(result, undefined, controls);
      return controls;
    });
    const onDetected = jasmine.createSpy('onDetected');
    const engine = new ZxingCameraScannerEngine(createReader);

    await engine.start(container, onDetected);

    expect(onDetected).toHaveBeenCalledOnceWith('9782070363735');
    expect(controls.stop).not.toHaveBeenCalled();

    await engine.stop();
    expect(controls.stop).toHaveBeenCalledOnceWith();
  });

  it('pauses after the first decoded value without stopping the active camera', async () => {
    const onDetected = jasmine.createSpy('onDetected');
    const result = jasmine.createSpyObj<Result>('Result', ['getText']);
    result.getText.and.returnValue('9782070363735');
    const engine = new ZxingCameraScannerEngine(createReader);

    await engine.start(container, onDetected);
    const decodeCallback = reader.decodeFromConstraints.calls.mostRecent().args[2];
    decodeCallback(result, undefined, controls);
    decodeCallback(result, undefined, controls);

    expect(onDetected).toHaveBeenCalledOnceWith('9782070363735');
    expect(controls.stop).not.toHaveBeenCalled();

    await engine.stop();
    expect(controls.stop).toHaveBeenCalledOnceWith();
  });

  it('resumes decoding on the existing camera stream', async () => {
    const onDetected = jasmine.createSpy('onDetected');
    const firstResult = jasmine.createSpyObj<Result>('Result', ['getText']);
    firstResult.getText.and.returnValue('9782070363735');
    const secondResult = jasmine.createSpyObj<Result>('Result', ['getText']);
    secondResult.getText.and.returnValue('9780306406157');
    const engine = new ZxingCameraScannerEngine(createReader);

    await engine.start(container, onDetected);
    const decodeCallback = reader.decodeFromConstraints.calls.mostRecent().args[2];
    decodeCallback(firstResult, undefined, controls);
    engine.resume();
    decodeCallback(secondResult, undefined, controls);

    expect(onDetected).toHaveBeenCalledTimes(2);
    expect(controls.stop).not.toHaveBeenCalled();
  });

  it('reacquires a new camera stream when the active video track ends', async () => {
    const tracks: EventTarget[] = [];
    const decodeCallbacks: Array<(result: Result | undefined) => void> = [];
    reader.decodeFromConstraints.and.callFake(async (_constraints, preview, callback) => {
      const track = new EventTarget();
      tracks.push(track);
      attachFakeCameraTrack(preview as HTMLVideoElement, track);
      decodeCallbacks.push(result => callback(result, undefined, controls));
      return controls;
    });
    const onDetected = jasmine.createSpy('onDetected');
    const engine = new ZxingCameraScannerEngine(createReader);

    await engine.start(container, onDetected);
    const firstPreview = container.querySelector('video');
    tracks[0].dispatchEvent(new Event('ended'));
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(reader.decodeFromConstraints).toHaveBeenCalledTimes(2);
    expect(container.querySelector('video')).not.toBe(firstPreview);

    const result = jasmine.createSpyObj<Result>('Result', ['getText']);
    result.getText.and.returnValue('9780306406157');
    decodeCallbacks[1](result);
    expect(onDetected).toHaveBeenCalledOnceWith('9780306406157');

    await engine.stop();
  });

  it('preserves the paused-after-detection state when reacquiring an ended track', async () => {
    const tracks: EventTarget[] = [];
    const decodeCallbacks: Array<(result: Result | undefined) => void> = [];
    reader.decodeFromConstraints.and.callFake(async (_constraints, preview, callback) => {
      const track = new EventTarget();
      tracks.push(track);
      attachFakeCameraTrack(preview as HTMLVideoElement, track);
      decodeCallbacks.push(result => callback(result, undefined, controls));
      return controls;
    });
    const onDetected = jasmine.createSpy('onDetected');
    const engine = new ZxingCameraScannerEngine(createReader);
    const firstResult = jasmine.createSpyObj<Result>('Result', ['getText']);
    firstResult.getText.and.returnValue('9782070363735');

    await engine.start(container, onDetected);
    decodeCallbacks[0](firstResult);
    tracks[0].dispatchEvent(new Event('ended'));
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(reader.decodeFromConstraints).toHaveBeenCalledTimes(2);
    const secondResult = jasmine.createSpyObj<Result>('Result', ['getText']);
    secondResult.getText.and.returnValue('9780306406157');
    decodeCallbacks[1](secondResult);
    expect(onDetected).toHaveBeenCalledOnceWith('9782070363735');

    engine.resume();
    decodeCallbacks[1](secondResult);
    expect(onDetected).toHaveBeenCalledTimes(2);

    await engine.stop();
  });

  it('does not reacquire a camera after a voluntary stop', async () => {
    const track = new EventTarget();
    reader.decodeFromConstraints.and.callFake(async (_constraints, preview) => {
      attachFakeCameraTrack(preview as HTMLVideoElement, track);
      return controls;
    });
    const engine = new ZxingCameraScannerEngine(createReader);

    await engine.start(container, jasmine.createSpy('onDetected'));
    await engine.stop();
    track.dispatchEvent(new Event('ended'));
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(reader.decodeFromConstraints).toHaveBeenCalledOnceWith(
      {video: {facingMode: 'environment'}},
      jasmine.any(HTMLVideoElement),
      jasmine.any(Function),
    );
  });

  it('reports a recoverable error when reacquiring an ended track fails', async () => {
    const track = new EventTarget();
    let cameraStartCount = 0;
    reader.decodeFromConstraints.and.callFake(async (_constraints, preview) => {
      cameraStartCount += 1;
      if (cameraStartCount > 1) {
        throw {name: 'NotAllowedError'};
      }

      attachFakeCameraTrack(preview as HTMLVideoElement, track);
      return controls;
    });
    const onCameraError = jasmine.createSpy('onCameraError');
    const engine = new ZxingCameraScannerEngine(createReader);

    await engine.start(container, jasmine.createSpy('onDetected'), onCameraError);
    track.dispatchEvent(new Event('ended'));
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(cameraStartCount).toBe(2);
    expect(onCameraError).toHaveBeenCalledOnceWith({name: 'NotAllowedError'});
    expect(container.querySelector('video')).toBeNull();

    await engine.stop();
  });

  it('reports the ended track as a recoverable error without looping when the new stream is already ended', async () => {
    const tracks: EventTarget[] = [];
    let cameraStartCount = 0;
    reader.decodeFromConstraints.and.callFake(async (_constraints, preview) => {
      cameraStartCount += 1;
      const track = new EventTarget();
      if (cameraStartCount > 1) {
        Object.defineProperty(track, 'readyState', {value: 'ended'});
      }
      tracks.push(track);
      attachFakeCameraTrack(preview as HTMLVideoElement, track);
      return controls;
    });
    const onCameraError = jasmine.createSpy('onCameraError');
    const engine = new ZxingCameraScannerEngine(createReader);

    await engine.start(container, jasmine.createSpy('onDetected'), onCameraError);
    tracks[0].dispatchEvent(new Event('ended'));
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(cameraStartCount).toBe(2);
    expect(onCameraError).toHaveBeenCalledOnceWith(jasmine.objectContaining({
      message: jasmine.stringMatching(/caméra/i),
    }));
    expect(container.querySelector('video')).toBeNull();

    await engine.stop();
  });

  it('asks the active video track to refocus when the browser exposes focus modes', async () => {
    const track = {
      getCapabilities: jasmine.createSpy('getCapabilities').and.returnValue({
        focusMode: ['single-shot', 'continuous'],
      }),
      applyConstraints: jasmine.createSpy('applyConstraints').and.returnValue(Promise.resolve()),
    };
    const engine = new ZxingCameraScannerEngine(createReader);
    await engine.start(container, jasmine.createSpy('onDetected'));
    const video = container.querySelector('video') as HTMLVideoElement;
    Object.defineProperty(video, 'srcObject', {
      configurable: true,
      value: {getVideoTracks: () => [track]},
    });

    const didRefocus = await engine.refocus();

    expect(didRefocus).toBeTrue();
    expect(track.applyConstraints.calls.allArgs()).toEqual([
      [{advanced: [{focusMode: 'single-shot'}]}],
      [{advanced: [{focusMode: 'continuous'}]}],
    ]);
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

  it('creates photo regions lazily and stops after the first successful region', async () => {
    const result = jasmine.createSpyObj<Result>('Result', ['getText']);
    result.getText.and.returnValue('9782070363735');
    reader.decodeFromImageUrl.and.returnValue(Promise.reject(new Error('not found')));
    reader.decodeFromCanvas.and.returnValue(result);
    spyOn(URL, 'createObjectURL').and.returnValue(
      `data:image/svg+xml,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1800"></svg>')}`,
    );
    spyOn(URL, 'revokeObjectURL');
    const createElement = spyOn(document, 'createElement').and.callThrough();
    const engine = new ZxingCameraScannerEngine(createReader);

    const value = await engine.scanFile(new File(['barcode'], 'book.jpg', {type: 'image/jpeg'}));

    expect(value).toBe('9782070363735');
    expect(reader.decodeFromCanvas).toHaveBeenCalledOnceWith(jasmine.any(HTMLCanvasElement));
    expect(createElement.calls.allArgs().filter(([tagName]) => tagName === 'canvas')).toHaveSize(1);
  });
});
