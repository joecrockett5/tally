import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Sheet } from '../components/Sheet';

export interface ScanResult {
  code: string;
  /** e.g. "ean_13", "upc_e" */
  format: string | null;
}

interface Props {
  onDetected: (result: ScanResult) => void;
  onClose: () => void;
}

type CameraState = 'starting' | 'scanning' | 'insecure' | 'unsupported' | 'denied' | 'error';

const FORMATS = ['ean_13', 'ean_8', 'upc_a', 'upc_e'];

interface BarcodeDetectorLike {
  detect(source: CanvasImageSource): Promise<{ rawValue: string; format: string }[]>;
}

declare global {
  interface Window {
    BarcodeDetector?: {
      new (options: { formats: string[] }): BarcodeDetectorLike;
      getSupportedFormats(): Promise<string[]>;
    };
  }
}

async function nativeDetector(): Promise<BarcodeDetectorLike | null> {
  if (!window.BarcodeDetector) return null;
  try {
    const supported = await window.BarcodeDetector.getSupportedFormats();
    const formats = FORMATS.filter((f) => supported.includes(f));
    return formats.length ? new window.BarcodeDetector({ formats }) : null;
  } catch {
    return null;
  }
}

/**
 * Camera barcode scanner. Uses the native BarcodeDetector where available and
 * falls back to ZXing (iOS Safari has no BarcodeDetector). The camera stream is
 * stopped as soon as a code is read or the scanner closes.
 */
export function Scanner({ onDetected, onClose }: Props) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [state, setState] = useState<CameraState>('starting');
  const [torchAvailable, setTorchAvailable] = useState(false);
  const [torchOn, setTorchOn] = useState(false);
  const [manual, setManual] = useState('');
  const detectedRef = useRef(false);
  const onDetectedRef = useRef(onDetected);
  onDetectedRef.current = onDetected;

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let zxingControls: { stop: () => void } | undefined;

    const stop = () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
      zxingControls?.stop();
      streamRef.current?.getTracks().forEach((t) => t.stop());
      streamRef.current = null;
    };

    const found = (code: string, format: string | null) => {
      if (detectedRef.current) return;
      detectedRef.current = true;
      stop();
      navigator.vibrate?.(60);
      onDetectedRef.current({ code, format });
    };

    (async () => {
      if (!window.isSecureContext) return setState('insecure');
      if (!navigator.mediaDevices?.getUserMedia) return setState('unsupported');

      let stream: MediaStream;
      try {
        stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
          audio: false,
        });
      } catch (err) {
        if (!cancelled) setState((err as Error).name === 'NotAllowedError' ? 'denied' : 'error');
        return;
      }
      if (cancelled) {
        stream.getTracks().forEach((t) => t.stop());
        return;
      }
      streamRef.current = stream;
      const track = stream.getVideoTracks()[0];
      const caps = (track.getCapabilities?.() ?? {}) as MediaTrackCapabilities & { torch?: boolean };
      setTorchAvailable(!!caps.torch);

      const video = videoRef.current!;
      video.srcObject = stream;
      await video.play().catch(() => undefined);
      setState('scanning');

      const detector = await nativeDetector();
      if (detector) {
        const tick = async () => {
          if (cancelled) return;
          try {
            if (video.readyState >= 2) {
              const codes = await detector.detect(video);
              if (codes.length) return found(codes[0].rawValue, codes[0].format);
            }
          } catch {
            /* frame not ready */
          }
          timer = setTimeout(tick, 150);
        };
        tick();
      } else {
        const { BrowserMultiFormatOneDReader, BarcodeFormat } = await import('@zxing/browser');
        if (cancelled) return;
        const reader = new BrowserMultiFormatOneDReader();
        const names: Partial<Record<number, string>> = {
          [BarcodeFormat.EAN_13]: 'ean_13',
          [BarcodeFormat.EAN_8]: 'ean_8',
          [BarcodeFormat.UPC_A]: 'upc_a',
          [BarcodeFormat.UPC_E]: 'upc_e',
        };
        zxingControls = await reader.decodeFromStream(stream, video, (result) => {
          if (result) {
            const format = names[result.getBarcodeFormat()];
            if (format) found(result.getText(), format);
          }
        });
        if (cancelled) zxingControls.stop();
      }
    })();

    return stop;
  }, []);

  const toggleTorch = async () => {
    const track = streamRef.current?.getVideoTracks()[0];
    if (!track) return;
    const next = !torchOn;
    try {
      await track.applyConstraints({ advanced: [{ torch: next } as MediaTrackConstraintSet] });
      setTorchOn(next);
    } catch {
      setTorchAvailable(false);
    }
  };

  const submitManual = (e: FormEvent) => {
    e.preventDefault();
    const code = manual.replace(/\D/g, '');
    if (code.length >= 8) {
      streamRef.current?.getTracks().forEach((t) => t.stop());
      onDetected({ code, format: null });
    }
  };

  return (
    <Sheet title="Scan a barcode" onClose={onClose}>
      <div className="stack">
        {(state === 'starting' || state === 'scanning') && (
          <div className="scanner-view">
            <video ref={videoRef} playsInline muted aria-label="Camera preview" />
            <div className="scanner-guide" aria-hidden="true" />
            {state === 'starting' && <p className="scanner-status">Starting camera…</p>}
            {torchAvailable && (
              <button type="button" className="button button-small scanner-torch" onClick={toggleTorch} aria-pressed={torchOn}>
                {torchOn ? 'Torch off' : 'Torch on'}
              </button>
            )}
          </div>
        )}
        {state === 'insecure' && (
          <p className="note note-warn">
            The camera only works when Tally is opened over <strong>HTTPS</strong>. Open it through your secure address
            (set up in Nginx Proxy Manager), or type the barcode below.
          </p>
        )}
        {state === 'unsupported' && <p className="note note-warn">This browser can't use the camera. Type the barcode below.</p>}
        {state === 'denied' && (
          <p className="note note-warn">
            Camera access was blocked. Allow camera access for this site in your browser settings, or type the barcode below.
          </p>
        )}
        {state === 'error' && <p className="note note-warn">The camera couldn't be started. Type the barcode below.</p>}

        <form className="row" onSubmit={submitManual}>
          <label className="field grow">
            <span>Or enter the barcode</span>
            <input
              inputMode="numeric"
              pattern="[0-9 ]*"
              autoComplete="off"
              placeholder="e.g. 5000159484695"
              value={manual}
              onChange={(e) => setManual(e.target.value)}
            />
          </label>
          <button className="button button-primary scanner-go" disabled={manual.replace(/\D/g, '').length < 8}>
            Look up
          </button>
        </form>
      </div>
    </Sheet>
  );
}
