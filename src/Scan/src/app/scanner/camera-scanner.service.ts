import {BrowserMultiFormatReader} from '@zxing/browser';
import {BarcodeFormat, DecodeHintType} from '@zxing/library';
import {Inject, Injectable, InjectionToken} from '@angular/core';

export interface CameraScannerReader {
  decodeFromConstraints: BrowserMultiFormatReader['decodeFromConstraints'];
  decodeFromImageUrl: BrowserMultiFormatReader['decodeFromImageUrl'];
  decodeFromCanvas: BrowserMultiFormatReader['decodeFromCanvas'];
}

export type CameraScannerReaderFactory = (
  hints: Map<DecodeHintType, any>,
) => CameraScannerReader;

export interface CameraScannerEngine {
  start(
    container: HTMLElement,
    onDetected: (rawValue: string) => void,
    onCameraError?: (error: unknown) => void,
  ): Promise<void>;
  resume(): void;
  refocus(): Promise<boolean>;
  stop(): Promise<void>;
  scanFile(imageFile: File): Promise<string>;
}

export type CameraScannerEngineFactory = () => CameraScannerEngine;

export const CAMERA_SCANNER_ENGINE_FACTORY = new InjectionToken<CameraScannerEngineFactory>(
  'CAMERA_SCANNER_ENGINE_FACTORY',
  {
    providedIn: 'root',
    factory: () => (): CameraScannerEngine => new ZxingCameraScannerEngine(
      hints => new BrowserMultiFormatReader(hints, {
        delayBetweenScanAttempts: 200,
        delayBetweenScanSuccess: 1000,
      }),
    ),
  },
);

export interface CameraScannerHandle {
  resume(): void;
  refocus(): Promise<boolean>;
  stop(): Promise<void>;
}

type CameraFocusMode = 'single-shot' | 'continuous';

interface CameraFocusCapabilities {
  focusMode?: readonly string[];
}

interface CameraFocusConstraintSet extends MediaTrackConstraintSet {
  focusMode?: CameraFocusMode;
}

const CAMERA_CONSTRAINTS: MediaTrackConstraints = {
  facingMode: 'environment',
};

const CAMERA_SCAN_FORMATS: BarcodeFormat[] = [
  BarcodeFormat.QR_CODE,
  BarcodeFormat.EAN_13,
  BarcodeFormat.EAN_8,
  BarcodeFormat.UPC_A,
  BarcodeFormat.UPC_E,
];

const MAX_PHOTO_DECODE_DIMENSION = 2000;

interface PhotoDecodeRegion {
  left: number;
  top: number;
  width: number;
  height: number;
  scale: number;
  threshold: number | null;
}

const PHOTO_DECODE_REGIONS: PhotoDecodeRegion[] = [
  {left: 0, top: .15, width: 1, height: .7, scale: 1, threshold: null},
  {left: 0, top: .3, width: 1, height: .7, scale: 1, threshold: null},
  {left: .1, top: .15, width: .8, height: .7, scale: 1, threshold: null},
  {left: 0, top: .15, width: 1, height: .7, scale: 1, threshold: 160},
  {left: 0, top: .3, width: 1, height: .7, scale: 1, threshold: 160},
  {left: .1, top: .15, width: .8, height: .7, scale: 1, threshold: 160},
  {left: 0, top: 0, width: 1, height: 1, scale: .5, threshold: null},
  {left: .1, top: .15, width: .8, height: .7, scale: .5, threshold: 160},
];

type CameraScannerControls = Awaited<ReturnType<CameraScannerReader['decodeFromConstraints']>>;

function createDecoderHints(): Map<DecodeHintType, any> {
  return new Map<DecodeHintType, any>([
    [DecodeHintType.POSSIBLE_FORMATS, [...CAMERA_SCAN_FORMATS]],
    [DecodeHintType.TRY_HARDER, true],
  ]);
}

function loadImage(imageUrl: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('La photo ne peut pas être chargée.'));
    image.src = imageUrl;
  });
}

function applyLuminanceThreshold(
  context: CanvasRenderingContext2D,
  width: number,
  height: number,
  threshold: number,
): void {
  const imageData = context.getImageData(0, 0, width, height);
  const {data} = imageData;

  for (let index = 0; index < data.length; index += 4) {
    const luminance = .299 * data[index] + .587 * data[index + 1] + .114 * data[index + 2];
    const value = luminance < threshold ? 0 : 255;
    data[index] = value;
    data[index + 1] = value;
    data[index + 2] = value;
    data[index + 3] = 255;
  }

  context.putImageData(imageData, 0, 0);
}

function* createPhotoDecodeCanvases(image: HTMLImageElement): Generator<HTMLCanvasElement> {
  const sourceWidth = image.naturalWidth || image.width;
  const sourceHeight = image.naturalHeight || image.height;
  if (!sourceWidth || !sourceHeight) {
    throw new Error('La photo ne contient aucune image exploitable.');
  }

  const baseScale = Math.min(1, MAX_PHOTO_DECODE_DIMENSION / Math.max(sourceWidth, sourceHeight));

  for (const region of PHOTO_DECODE_REGIONS) {
    const sourceLeft = Math.round(sourceWidth * region.left);
    const sourceTop = Math.round(sourceHeight * region.top);
    const regionWidth = Math.max(1, Math.round(sourceWidth * region.width));
    const regionHeight = Math.max(1, Math.round(sourceHeight * region.height));
    const scale = baseScale * region.scale;
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(regionWidth * scale));
    canvas.height = Math.max(1, Math.round(regionHeight * scale));

    const context = canvas.getContext('2d');
    if (!context) {
      throw new Error('Le navigateur ne permet pas d’analyser cette photo.');
    }

    context.drawImage(
      image,
      sourceLeft,
      sourceTop,
      regionWidth,
      regionHeight,
      0,
      0,
      canvas.width,
      canvas.height,
    );

    if (region.threshold !== null) {
      applyLuminanceThreshold(context, canvas.width, canvas.height, region.threshold);
    }

    yield canvas;
  }
}

export class ZxingCameraScannerEngine implements CameraScannerEngine {
  private controls: CameraScannerControls | null = null;
  private video: HTMLVideoElement | null = null;
  private cameraTrack: MediaStreamTrack | null = null;
  private trackEndedListener: EventListener | null = null;
  private container: HTMLElement | null = null;
  private onDetected: ((rawValue: string) => void) | null = null;
  private onCameraError: ((error: unknown) => void) | null = null;
  private startToken = 0;
  private active = false;
  private recovering = false;

  constructor(private readonly readerFactory: CameraScannerReaderFactory) {}

  async start(
    container: HTMLElement,
    onDetected: (rawValue: string) => void,
    onCameraError: (error: unknown) => void = () => undefined,
  ): Promise<void> {
    await this.stop();
    const startToken = ++this.startToken;
    this.container = container;
    this.onDetected = onDetected;
    this.onCameraError = onCameraError;
    this.active = true;
    await this.openCamera(startToken);
  }

  private async openCamera(startToken: number): Promise<void> {
    const container = this.container;
    if (!container || startToken !== this.startToken) {
      return;
    }

    const reader = this.readerFactory(createDecoderHints());
    const video = document.createElement('video');
    video.className = 'camera-video';
    video.autoplay = true;
    video.muted = true;
    video.playsInline = true;
    video.setAttribute('aria-hidden', 'true');
    container.replaceChildren(video);
    this.video = video;

    try {
      const controls = await reader.decodeFromConstraints(
        {video: CAMERA_CONSTRAINTS},
        video,
        (result, _error) => {
          if (startToken !== this.startToken || !this.active || !result) {
            return;
          }

          const rawValue = result.getText().trim();
          if (!rawValue) {
            return;
          }

          this.active = false;
          this.onDetected?.(rawValue);
        },
      );

      if (startToken !== this.startToken || this.video !== video) {
        controls.stop();
        return;
      }

      this.controls = controls;
      this.listenForTrackEnd(video, startToken);
    } catch (error: unknown) {
      if (startToken !== this.startToken) {
        return;
      }

      this.releaseCamera();
      throw error;
    }
  }

  private listenForTrackEnd(video: HTMLVideoElement, startToken: number): void {
    const track = (video.srcObject as MediaStream | null)?.getVideoTracks?.()[0];
    if (!track) {
      return;
    }

    const onTrackEnded: EventListener = () => {
      void this.recoverAfterTrackEnd(video, track, startToken);
    };
    this.cameraTrack = track;
    this.trackEndedListener = onTrackEnded;
    track.addEventListener('ended', onTrackEnded, {once: true});

    if (track.readyState === 'ended') {
      if (this.recovering) {
        this.failCameraRecovery(new Error('La caméra s’est interrompue après sa reconnexion.'));
      } else {
        onTrackEnded(new Event('ended'));
      }
    }
  }

  private failCameraRecovery(error: unknown): void {
    this.active = false;
    this.releaseCamera();
    this.onCameraError?.(error);
  }

  private async recoverAfterTrackEnd(
    video: HTMLVideoElement,
    track: MediaStreamTrack,
    startToken: number,
  ): Promise<void> {
    if (
      startToken !== this.startToken ||
      this.video !== video ||
      this.cameraTrack !== track ||
      this.recovering
    ) {
      return;
    }

    this.recovering = true;
    const recoveryToken = ++this.startToken;
    this.releaseCamera();

    try {
      await this.openCamera(recoveryToken);
    } catch (error: unknown) {
      if (recoveryToken !== this.startToken) {
        return;
      }

      this.failCameraRecovery(error);
    } finally {
      if (recoveryToken === this.startToken) {
        this.recovering = false;
      }
    }
  }

  private releaseCamera(): void {
    if (this.cameraTrack && this.trackEndedListener) {
      this.cameraTrack.removeEventListener('ended', this.trackEndedListener);
    }
    this.cameraTrack = null;
    this.trackEndedListener = null;

    const controls = this.controls;
    this.controls = null;
    controls?.stop();

    const video = this.video;
    this.video = null;
    if (video) {
      video.pause();
      video.srcObject = null;
      video.remove();
    }
  }

  resume(): void {
    if (this.container && (this.controls && this.video || this.recovering)) {
      this.active = true;
    }
  }

  async refocus(): Promise<boolean> {
    const stream = this.video?.srcObject as MediaStream | null;
    const track = stream?.getVideoTracks?.()[0];
    if (!track?.applyConstraints) {
      return false;
    }

    let supportedModes: readonly string[] | null = null;
    try {
      const capabilities = track.getCapabilities?.() as CameraFocusCapabilities | undefined;
      if (Array.isArray(capabilities?.focusMode)) {
        supportedModes = capabilities.focusMode;
      }
    } catch {
      // Some browsers expose getCapabilities but reject it for the active track.
    }

    if (supportedModes?.length === 0) {
      return false;
    }

    const modes: CameraFocusMode[] = supportedModes
      ? (['single-shot', 'continuous'] as const).filter(
        (mode): mode is CameraFocusMode => supportedModes.includes(mode),
      )
      : ['continuous'];

    let applied = false;
    for (const mode of modes) {
      try {
        await track.applyConstraints({
          advanced: [{focusMode: mode} as CameraFocusConstraintSet],
        });
        applied = true;
      } catch {
        // Focus constraints are optional; leave the camera's native autofocus active.
      }
    }

    return applied;
  }

  async stop(): Promise<void> {
    this.startToken += 1;
    this.active = false;
    this.recovering = false;
    this.container = null;
    this.onDetected = null;
    this.onCameraError = null;
    this.releaseCamera();
  }

  async scanFile(imageFile: File): Promise<string> {
    const reader = this.readerFactory(createDecoderHints());
    let imageUrl: string | null = null;
    let lastError: unknown;

    try {
      imageUrl = URL.createObjectURL(imageFile);
      try {
        const result = await reader.decodeFromImageUrl(imageUrl);
        return result.getText();
      } catch (error: unknown) {
        lastError = error;
      }

      const image = await loadImage(imageUrl);
      for (const canvas of createPhotoDecodeCanvases(image)) {
        try {
          return reader.decodeFromCanvas(canvas).getText();
        } catch (error: unknown) {
          lastError = error;
        }
      }

      throw lastError ?? new Error('Aucun code-barres lisible n’a été trouvé.');
    } finally {
      if (imageUrl) {
        URL.revokeObjectURL(imageUrl);
      }
    }
  }
}

@Injectable({providedIn: 'root'})
export class CameraScannerService {
  constructor(
    @Inject(CAMERA_SCANNER_ENGINE_FACTORY)
    private readonly engineFactory: CameraScannerEngineFactory,
  ) {}

  async start(
    container: HTMLElement,
    onDetected: (rawValue: string) => void,
    onCameraError: (error: Error) => void = () => undefined,
  ): Promise<CameraScannerHandle> {
    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error('La caméra nécessite une page HTTPS et un navigateur autorisant son accès.');
    }

    const engine = this.engineFactory();
    let active = true;
    let acceptingDetections = true;
    let stopPromise: Promise<void> | null = null;

    const stop = (): Promise<void> => {
      if (!stopPromise) {
        stopPromise = (async (): Promise<void> => {
          active = false;
          acceptingDetections = false;
          try {
            await engine.stop();
          } catch {
            // The decoder may already have stopped after a successful match.
          }
        })();
      }
      return stopPromise;
    };

    const resume = (): void => {
      if (!active || stopPromise) {
        return;
      }

      acceptingDetections = true;
      engine.resume();
    };

    const reportCameraError = (error: unknown): void => {
      if (!active || stopPromise) {
        return;
      }

      active = false;
      acceptingDetections = false;
      onCameraError(this.toCameraError(error));
      void stop();
    };

    const refocus = async (): Promise<boolean> => {
      if (!active || stopPromise) {
        return false;
      }

      return await engine.refocus();
    };

    try {
      await engine.start(container, (decodedText: string) => {
        if (!active || !acceptingDetections) {
          return;
        }

        const rawValue = decodedText.trim();
        if (!rawValue) {
          return;
        }

        acceptingDetections = false;
        onDetected(rawValue);
      }, reportCameraError);
    } catch (error: unknown) {
      await stop();
      throw this.toCameraError(error);
    }

    return {resume, refocus, stop};
  }

  async scanFile(imageFile: File): Promise<string> {
    const engine = this.engineFactory();
    return await engine.scanFile(imageFile);
  }

  private toCameraError(error: unknown): Error {
    const cameraError = error as {name?: unknown; message?: unknown} | null;
    switch (cameraError?.name) {
      case 'NotAllowedError':
        return new Error('Autorisez l’accès à la caméra dans les réglages du navigateur, puis réessayez.');
      case 'NotFoundError':
        return new Error('Aucune caméra utilisable n’a été trouvée sur cet appareil.');
      case 'NotReadableError':
        return new Error('La caméra est déjà utilisée par une autre application.');
      default:
        return error instanceof Error
          ? error
          : new Error(typeof cameraError?.message === 'string'
            ? cameraError.message
            : 'La caméra ne peut pas être activée.');
    }
  }
}
