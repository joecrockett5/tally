import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Scanner } from './Scanner';

afterEach(() => vi.unstubAllGlobals());

describe('Scanner', () => {
  it('explains that HTTPS is needed when the page is not a secure context', async () => {
    vi.stubGlobal('isSecureContext', false);
    render(<Scanner onDetected={() => {}} onClose={() => {}} />);
    expect(await screen.findByText(/only works when Tally is opened over/)).toBeInTheDocument();
  });

  it('accepts a typed barcode', async () => {
    vi.stubGlobal('isSecureContext', false);
    const onDetected = vi.fn();
    render(<Scanner onDetected={onDetected} onClose={() => {}} />);
    await userEvent.type(screen.getByLabelText('Or enter the barcode'), '5000 1596 2016 4');
    await userEvent.click(screen.getByRole('button', { name: 'Look up' }));
    expect(onDetected).toHaveBeenCalledWith({ code: '5000159620164', format: null });
  });

  it('stops the camera when closed', async () => {
    const stopTrack = vi.fn();
    const stream = { getTracks: () => [{ stop: stopTrack }], getVideoTracks: () => [{ stop: stopTrack, getCapabilities: () => ({}) }] };
    vi.stubGlobal('isSecureContext', true);
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn().mockResolvedValue(stream) },
    });
    vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue();
    // Native detector that never finds anything.
    vi.stubGlobal('BarcodeDetector', Object.assign(function () { return { detect: async () => [] }; }, { getSupportedFormats: async () => ['ean_13'] }));

    const { unmount } = render(<Scanner onDetected={() => {}} onClose={() => {}} />);
    await screen.findByLabelText('Camera preview');
    await vi.waitFor(() => expect(navigator.mediaDevices.getUserMedia).toHaveBeenCalled());
    await new Promise((r) => setTimeout(r, 20));
    unmount();
    expect(stopTrack).toHaveBeenCalled();
  });
});
