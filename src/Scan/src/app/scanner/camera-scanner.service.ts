import {BrowserMultiFormatReader} from '@zxing/browser';
import {BarcodeFormat, DecodeHintType} from '@zxing/library';
import {Inject, Injectable, InjectionToken} from '@angular/core';

export interface CameraScannerReader {
  decodeFromConstraints: BrowserMultiFormatReader['decodeFromConstraints'];
  decodeFromImageUrl: BrowserMultiFormatReader['decodeFromImageUrl'];
}

export type CameraScannerReaderFactory = (
  hints: Map<DecodeHintType, any>,
) => CameraScannerReader;

export interface CameraScannerEngine {
  start(container: HTMLElement, onDetected: (rawValue: string) => void): Promise<void>;
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
  stop(): Promise<void>;
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

type CameraScannerControls = Awaited<ReturnType<CameraScannerReader['decodeFromConstraints']>>;

function createDecoderHints(): Map<DecodeHintType, any> {
  return new Map<DecodeHintType, any>([
    [DecodeHintType.POSSIBLE_FORMATS, [...CAMERA_SCAN_FORMATS]],
    [DecodeHintType.TRY_HARDER, true],
  ]);
}

export class ZxingCameraScannerEngine implements CameraScannerEngine {
  private controls: CameraScannerControls | null = null;
  private video: HTMLVideoElement | null = null;
  private active = false;

  constructor(private readonly readerFactory: CameraScannerReaderFactory) {}

  async start(
    container: HTMLElement,
    onDetected: (rawValue: string) => void,
  ): Promise<void> {
    await this.stop();

    const reader = this.readerFactory(createDecoderHints());
    const video = document.createElement('video');
    video.className = 'camera-video';
    video.autoplay = true;
    video.muted = true;
    video.playsInline = true;
    video.setAttribute('aria-hidden', 'true');
    container.replaceChildren(video);

    this.video = video;
    this.active = true;

    try {
      const controls = await reader.decodeFromConstraints(
        {video: CAMERA_CONSTRAINTS},
        video,
        (result, _error, callbackControls) => {
          if (!this.active || !result) {
            return;
          }

          const rawValue = result.getText().trim();
          if (!rawValue) {
            return;
          }

          this.active = false;
          callbackControls.stop();
          onDetected(rawValue);
        },
      );

      this.controls = controls;
      if (!this.active) {
        controls.stop();
      }
    } catch (error: unknown) {
      await this.stop();
      throw error;
    }
  }

  async stop(): Promise<void> {
    this.active = false;

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

  async scanFile(imageFile: File): Promise<string> {
    const reader = this.readerFactory(createDecoderHints());
    let imageUrl: string | null = null;

    try {
      imageUrl = URL.createObjectURL(imageFile);
      const result = await reader.decodeFromImageUrl(imageUrl);
      return result.getText();
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
  ): Promise<CameraScannerHandle> {
    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error('La caméra nécessite une page HTTPS et un navigateur autorisant son accès.');
    }

    const engine = this.engineFactory();
    let active = true;
    let stopPromise: Promise<void> | null = null;

    const stop = (): Promise<void> => {
      if (!stopPromise) {
        stopPromise = (async (): Promise<void> => {
          active = false;
          try {
            await engine.stop();
          } catch {
            // The decoder may already have stopped after a successful match.
          }
        })();
      }
      return stopPromise;
    };

    try {
      await engine.start(container, (decodedText: string) => {
        if (!active) {
          return;
        }

        const rawValue = decodedText.trim();
        if (!rawValue) {
          return;
        }

        onDetected(rawValue);
        void stop();
      });
    } catch (error: unknown) {
      await stop();
      throw this.toCameraError(error);
    }

    return {stop};
  }

  async scanFile(imageFile: File): Promise<string> {
    const engine = this.engineFactory();
    return await engine.scanFile(imageFile);
  }

  private toCameraError(error: unknown): Error {
    const cameraError = error as {name?: unknown; message?: unknown} | null;
    switch (cameraError?.name) {
      case 'NotAllowedError':
        return new Error('Autorisez l’accès à la caméra dans Safari, puis réessayez.');
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
