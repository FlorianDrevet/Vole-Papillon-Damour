import {Inject, Injectable, InjectionToken} from '@angular/core';
import {
  Html5Qrcode,
  Html5QrcodeCameraScanConfig,
  Html5QrcodeSupportedFormats,
  QrcodeErrorCallback,
  QrcodeSuccessCallback,
} from 'html5-qrcode';

export interface CameraScannerEngine {
  start(
    cameraIdOrConfig: string | MediaTrackConstraints,
    configuration: Html5QrcodeCameraScanConfig,
    qrCodeSuccessCallback: QrcodeSuccessCallback,
    qrCodeErrorCallback: QrcodeErrorCallback,
  ): Promise<null>;
  stop(): Promise<void>;
  scanFile(imageFile: File, showImage?: boolean): Promise<string>;
  clear(): void;
}

export type CameraScannerEngineFactory = (elementId: string) => CameraScannerEngine;

export const CAMERA_SCANNER_ENGINE_FACTORY = new InjectionToken<CameraScannerEngineFactory>(
  'CAMERA_SCANNER_ENGINE_FACTORY',
  {
    providedIn: 'root',
    factory: () => (elementId: string): CameraScannerEngine => new Html5Qrcode(elementId, {
      verbose: false,
      formatsToSupport: [
        Html5QrcodeSupportedFormats.EAN_13,
        Html5QrcodeSupportedFormats.EAN_8,
      ],
      // Safari exposes BarcodeDetector only on some versions. ZXing is the
      // stable cross-browser decoder used by html5-qrcode for iOS as well.
      useBarCodeDetectorIfSupported: false,
    }),
  },
);

export interface CameraScannerHandle {
  stop(): Promise<void>;
}

const CAMERA_CONSTRAINTS: MediaTrackConstraints = {
  // html5-qrcode accepts a string (or an `exact` object) for this argument.
  // An `ideal` object is rejected before getUserMedia is called, notably on iOS.
  facingMode: 'environment',
};

const CAMERA_SCAN_CONFIG: Html5QrcodeCameraScanConfig = {
  fps: 10,
  qrbox: {width: 280, height: 120},
  aspectRatio: 1.777778,
};

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

    const engine = this.engineFactory(container.id);
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
          try {
            engine.clear();
          } catch {
            // Clearing is best effort when the browser has already removed the video.
          }
        })();
      }
      return stopPromise;
    };

    try {
      await engine.start(
        CAMERA_CONSTRAINTS,
        CAMERA_SCAN_CONFIG,
        (decodedText: string) => {
          if (!active) {
            return;
          }

          const rawValue = decodedText.trim();
          if (!rawValue) {
            return;
          }

          onDetected(rawValue);
          void stop();
        },
        () => {
          // A frame without a valid barcode is expected while scanning.
        },
      );
    } catch (error: unknown) {
      await stop();
      throw this.toCameraError(error);
    }

    return {stop};
  }

  async scanFile(container: HTMLElement, imageFile: File): Promise<string> {
    const engine = this.engineFactory(container.id);
    try {
      return await engine.scanFile(imageFile, false);
    } finally {
      try {
        engine.clear();
      } catch {
        // Keep the file fallback usable even if the decoder has already cleaned up.
      }
    }
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
