import {Injectable} from '@angular/core';

interface BarcodeDetection {
  rawValue?: string;
}

interface BarcodeDetectorLike {
  detect(source: HTMLVideoElement): Promise<BarcodeDetection[]>;
}

interface BarcodeDetectorConstructor {
  new (options?: {formats?: string[]}): BarcodeDetectorLike;
}

export interface CameraScannerHandle {
  stop(): void;
}

@Injectable({providedIn: 'root'})
export class CameraScannerService {
  async start(
    video: HTMLVideoElement,
    onDetected: (rawValue: string) => void,
  ): Promise<CameraScannerHandle> {
    const detectorConstructor = (window as Window & {
      BarcodeDetector?: BarcodeDetectorConstructor;
    }).BarcodeDetector;

    if (!detectorConstructor) {
      throw new Error('La lecture caméra n’est pas disponible dans ce navigateur.');
    }

    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error('Le navigateur ne donne pas accès à la caméra.');
    }

    const stream = await navigator.mediaDevices.getUserMedia({
      video: {facingMode: {ideal: 'environment'}},
      audio: false,
    });
    const detector = new detectorConstructor({formats: ['ean_13', 'ean_8']});
    video.srcObject = stream;

    try {
      await video.play();
    } catch (error) {
      stream.getTracks().forEach(track => track.stop());
      video.srcObject = null;
      throw error;
    }

    let active = true;
    let animationFrame: number | null = null;
    const stop = (): void => {
      active = false;
      if (animationFrame !== null) {
        cancelAnimationFrame(animationFrame);
      }
      stream.getTracks().forEach(track => track.stop());
      video.srcObject = null;
    };

    const detectNextFrame = async (): Promise<void> => {
      if (!active) {
        return;
      }

      try {
        const detections = await detector.detect(video);
        const rawValue = detections.find(
          detection => typeof detection.rawValue === 'string' && detection.rawValue.length > 0,
        )?.rawValue;
        if (rawValue) {
          onDetected(rawValue);
          stop();
          return;
        }
      } catch {
        // A transient frame error must not stop the camera session.
      }

      if (active) {
        animationFrame = requestAnimationFrame(() => void detectNextFrame());
      }
    };

    void detectNextFrame();
    return {stop};
  }
}
